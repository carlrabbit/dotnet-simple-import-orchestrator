using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator;
using DotnetSimpleImportOrchestrator.Abstractions;
using DotnetSimpleImportOrchestrator.Csv;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class CsvSourceTests
{
    [Test]
    public async Task SourceFactoryCallsMapperAndCreatesSourceFromTypedConfiguration()
    {
        string directory = CreateTempDirectory();
        try
        {
            CsvConfiguration configuration = new(directory);
            CapturingMapper mapper = new(DefaultOptions(directory));
            CsvFileImportSourceFactory<CsvConfiguration> factory = new(mapper);
            ImportDefinition<CsvConfiguration> definition = Definition("csv", configuration);

            IImportSource source = await factory.CreateAsync(
                definition,
                new ImportSourceFactoryContext<CsvConfiguration>
                {
                    Definition = definition,
                    State = new ImportState(),
                    TimeProvider = TimeProvider.System
                },
                CancellationToken.None);

            await Assert.That(source).IsTypeOf<CsvFileImportSource>();
            await Assert.That(mapper.Called).IsTrue();
            await Assert.That(mapper.SeenConfiguration).IsSameReferenceAs(configuration);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task MissingDirectoryCanReturnNoCandidateOrFail()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        CsvFileImportSource noCandidateSource = new(DefaultOptions(directory) with
        {
            MissingDirectoryBehavior = MissingDirectoryBehavior.TreatAsNoCandidate
        });

        ImportPollResult result = await noCandidateSource.PollAsync(PollContext(), CancellationToken.None);
        await Assert.That(result.Candidate).IsNull();

        CsvFileImportSource failingSource = new(DefaultOptions(directory) with
        {
            MissingDirectoryBehavior = MissingDirectoryBehavior.Fail
        });

        await AssertThrowsAsync<DirectoryNotFoundException>(() =>
            failingSource.PollAsync(PollContext(), CancellationToken.None).AsTask());
    }

    [Test]
    public async Task NoneReadinessAcceptsDiscoveredFile()
    {
        string directory = CreateTempDirectory();
        try
        {
            string file = WriteFile(directory, "orders.csv", "a,b\n1,2\n");
            CsvFileImportSource source = new(DefaultOptions(directory) with
            {
                Readiness = new FileReadinessOptions { Strategy = FileReadinessStrategy.None }
            });

            ImportPollResult result = await source.PollAsync(PollContext(), CancellationToken.None);

            await Assert.That(result.Candidate).IsNotNull();
            await Assert.That(result.Candidate!.SourceItemId).IsEqualTo(Path.GetFullPath(file));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task MarkerFileReadinessAcceptsOnlyFilesWithMarker()
    {
        string directory = CreateTempDirectory();
        try
        {
            WriteFile(directory, "a.csv", "a\n1\n");
            string marked = WriteFile(directory, "b.csv", "b\n2\n");
            WriteFile(directory, "b.csv.done", "");
            CsvFileImportSource source = new(DefaultOptions(directory) with
            {
                Readiness = new FileReadinessOptions
                {
                    Strategy = FileReadinessStrategy.MarkerFile,
                    MarkerFileExtension = ".done"
                },
                Ordering = FileCandidateOrdering.NameAscending
            });

            ImportPollResult result = await source.PollAsync(PollContext(), CancellationToken.None);

            await Assert.That(result.Candidate!.SourceItemId).IsEqualTo(Path.GetFullPath(marked));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task StableSizeReadinessAcceptsUnchangedFiles()
    {
        string directory = CreateTempDirectory();
        try
        {
            WriteFile(directory, "stable.csv", "a\n1\n");
            CsvFileImportSource source = new(DefaultOptions(directory) with
            {
                Readiness = new FileReadinessOptions
                {
                    Strategy = FileReadinessStrategy.StableSize,
                    StableFor = TimeSpan.FromMilliseconds(10)
                }
            });

            ImportPollResult result = await source.PollAsync(PollContext(), CancellationToken.None);

            await Assert.That(result.Candidate).IsNotNull();
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task ExclusiveReadinessIgnoresLockedFilesAndAcceptsOpenableFiles()
    {
        string directory = CreateTempDirectory();
        try
        {
            string locked = WriteFile(directory, "a.csv", "a\n1\n");
            string openable = WriteFile(directory, "b.csv", "b\n2\n");
            await using FileStream _ = File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None);
            CsvFileImportSource source = new(DefaultOptions(directory) with
            {
                Readiness = new FileReadinessOptions { Strategy = FileReadinessStrategy.ExclusiveRead },
                Ordering = FileCandidateOrdering.NameAscending
            });

            ImportPollResult result = await source.PollAsync(PollContext(), CancellationToken.None);

            await Assert.That(result.Candidate!.SourceItemId).IsEqualTo(Path.GetFullPath(openable));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task ProcessedFilesInCursorAreSkipped()
    {
        string directory = CreateTempDirectory();
        try
        {
            string first = WriteFile(directory, "a.csv", "a\n1\n");
            string second = WriteFile(directory, "b.csv", "b\n2\n");
            CsvFileMetadata firstMetadata = Metadata(first);
            JsonObject cursor = CsvCandidateMetadata.CreateProcessedCursorUpdate(
                Path.GetFullPath(first),
                firstMetadata,
                DateTimeOffset.UtcNow);
            CsvFileImportSource source = new(DefaultOptions(directory) with
            {
                Ordering = FileCandidateOrdering.NameAscending
            });

            ImportPollResult result = await source.PollAsync(PollContext(cursor), CancellationToken.None);

            await Assert.That(result.Candidate!.SourceItemId).IsEqualTo(Path.GetFullPath(second));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task CandidateMetadataAndCursorUseCsvShape()
    {
        string directory = CreateTempDirectory();
        try
        {
            string file = WriteFile(directory, "orders.csv", "a;b\n1;2\n");
            CsvPayloadOptions csv = DefaultCsvOptions() with { Delimiter = ";" };
            CsvFileImportSource source = new(DefaultOptions(directory) with
            {
                Csv = csv
            });

            ImportPollResult result = await source.PollAsync(PollContext(), CancellationToken.None);

            CsvFileMetadata metadata = CsvCandidateMetadata.GetFileMetadata(result.Candidate!.Metadata);
            CsvPayloadOptions payloadOptions = CsvCandidateMetadata.GetPayloadOptions(result.Candidate.Metadata);

            await Assert.That(metadata.FullPath).IsEqualTo(Path.GetFullPath(file));
            await Assert.That(payloadOptions.Delimiter).IsEqualTo(";");
            await Assert.That(result.CursorUpdate["csvFileSource"]!["processedFiles"]![Path.GetFullPath(file)]).IsNotNull();
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Test]
    public async Task OrderingIsDeterministicForOldestFirstAndNameAscending()
    {
        string directory = CreateTempDirectory();
        try
        {
            string b = WriteFile(directory, "b.csv", "b\n2\n");
            string a = WriteFile(directory, "a.csv", "a\n1\n");
            File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(a, DateTime.UtcNow);

            ImportPollResult oldest = await new CsvFileImportSource(DefaultOptions(directory) with
            {
                Ordering = FileCandidateOrdering.OldestFirst
            }).PollAsync(PollContext(), CancellationToken.None);

            ImportPollResult named = await new CsvFileImportSource(DefaultOptions(directory) with
            {
                Ordering = FileCandidateOrdering.NameAscending
            }).PollAsync(PollContext(), CancellationToken.None);

            await Assert.That(oldest.Candidate!.SourceItemId).IsEqualTo(Path.GetFullPath(b));
            await Assert.That(named.Candidate!.SourceItemId).IsEqualTo(Path.GetFullPath(a));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    internal static CsvFileImportSourceOptions DefaultOptions(string directory) => new()
    {
        DirectoryPath = directory,
        SearchPattern = "*.csv",
        Readiness = new FileReadinessOptions { Strategy = FileReadinessStrategy.None },
        Csv = DefaultCsvOptions()
    };

    internal static CsvPayloadOptions DefaultCsvOptions() => new()
    {
        EncodingName = "utf-8"
    };

    internal static ImportPollContext PollContext(JsonObject? cursor = null) => new()
    {
        ImportId = "csv",
        State = new ImportState { Cursor = cursor ?? [] },
        TimeProvider = TimeProvider.System
    };

    internal static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    internal static string WriteFile(string directory, string fileName, string contents)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    internal static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CsvFileMetadata Metadata(string file)
    {
        FileInfo info = new(file);
        return new CsvFileMetadata
        {
            FullPath = Path.GetFullPath(file),
            FileName = info.Name,
            Length = info.Length,
            LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
        };
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed record CsvConfiguration(string Directory) : IImportConfiguration;

    private sealed class CapturingMapper : ICsvFileImportSourceOptionsMapper<CsvConfiguration>
    {
        private readonly CsvFileImportSourceOptions _options;

        public CapturingMapper(CsvFileImportSourceOptions options)
        {
            _options = options;
        }

        public bool Called { get; private set; }

        public CsvConfiguration? SeenConfiguration { get; private set; }

        public CsvFileImportSourceOptions Map(
            ImportDefinition<CsvConfiguration> definition,
            ImportSourceFactoryContext<CsvConfiguration> context)
        {
            Called = true;
            SeenConfiguration = definition.Configuration;
            return _options with { DirectoryPath = definition.Configuration.Directory };
        }
    }

    private static ImportDefinition<CsvConfiguration> Definition(string id, CsvConfiguration configuration) => new()
    {
        Id = id,
        Polling = new PollingOptions { Interval = TimeSpan.FromMinutes(1) },
        Configuration = configuration
    };
}
