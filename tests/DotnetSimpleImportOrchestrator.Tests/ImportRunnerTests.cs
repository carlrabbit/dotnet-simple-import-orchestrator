using System.Text;
using System.Text.Json.Nodes;
using DotnetSimpleImportOrchestrator;
using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class ImportRunnerTests
{
    [Test]
    public async Task ImportIdsMustBeNonEmptyAndUniqueWithinOneRunnerCall()
    {
        TestHarness harness = new();

        await AssertThrowsAsync<ArgumentException>(() =>
            harness.Runner.RunOnceAsync([Definition("")], new ImportRuntimeState()).AsTask());

        await AssertThrowsAsync<ArgumentException>(() =>
            harness.Runner.RunOnceAsync([Definition("orders"), Definition("orders")], new ImportRuntimeState()).AsTask());
    }

    [Test]
    public async Task PollingIntervalsMustBePositive()
    {
        TestHarness harness = new();

        await AssertThrowsAsync<ArgumentException>(() =>
            harness.Runner.RunOnceAsync(
                [Definition("orders", pollingInterval: TimeSpan.Zero)],
                new ImportRuntimeState()).AsTask());
    }

    [Test]
    public async Task MissingConfigurationIsRejected()
    {
        TestHarness harness = new();

        await AssertThrowsAsync<ArgumentException>(() =>
            harness.Runner.RunOnceAsync(
                [
                    new ImportDefinition<TestConfiguration>
                    {
                        Id = "orders",
                        Polling = new PollingOptions { Interval = TimeSpan.FromMinutes(1) },
                        Configuration = null!
                    }
                ],
                new ImportRuntimeState()).AsTask());
    }

    [Test]
    public async Task DueImportsAreOrderedByPriorityThenImportId()
    {
        List<string> pollOrder = [];
        TestHarness harness = new();
        harness.Register("b-normal", TestSource.NoCandidate(pollOrder));
        harness.Register("a-normal", TestSource.NoCandidate(pollOrder));
        harness.Register("c-high", TestSource.NoCandidate(pollOrder));

        await harness.Runner.RunOnceAsync(
            [
                Definition("b-normal", priority: ImportPriorities.Normal),
                Definition("a-normal", priority: ImportPriorities.Normal),
                Definition("c-high", priority: ImportPriorities.High)
            ],
            new ImportRuntimeState());

        await Assert.That(pollOrder).IsEquivalentTo(["c-high", "a-normal", "b-normal"]);
    }

    [Test]
    public async Task NotDueImportsAreNotPolled()
    {
        ManualTimeProvider timeProvider = new(DateTimeOffset.Parse("2026-06-07T12:00:00Z"));
        TestSource source = TestSource.NoCandidate();
        TestHarness harness = new(timeProvider);
        harness.Register("orders", source);

        ImportRunResult result = await harness.Runner.RunOnceAsync(
            [Definition("orders", pollingInterval: TimeSpan.FromMinutes(30))],
            new ImportRuntimeState
            {
                Imports =
                {
                    ["orders"] = new ImportState { LastCheckedAt = timeProvider.GetUtcNow().AddMinutes(-5) }
                }
            });

        await Assert.That(source.PollCount).IsEqualTo(0);
        await Assert.That(result.Checks).Count().IsEqualTo(1);
        await Assert.That(result.Checks[0].Outcome).IsEqualTo(ImportCheckOutcome.NotDue);
    }

    [Test]
    public async Task RunnerPassStopsAfterFirstSuccessfulImport()
    {
        TestSource first = TestSource.Candidate("first-item", "first");
        TestSource second = TestSource.Candidate("second-item", "second");
        TestHarness harness = new();
        harness.Register("first", first);
        harness.Register("second", second);

        ImportRunResult result = await harness.Runner.RunOnceAsync(
            [
                Definition("first", priority: ImportPriorities.High),
                Definition("second", priority: ImportPriorities.Low)
            ],
            new ImportRuntimeState());

        await Assert.That(result.SuccessfulImportPerformed).IsTrue();
        await Assert.That(result.SuccessfulImportId).IsEqualTo("first");
        await Assert.That(first.PollCount).IsEqualTo(1);
        await Assert.That(second.PollCount).IsEqualTo(0);
        await Assert.That(result.State.Imports.ContainsKey("second")).IsFalse();
    }

    [Test]
    public async Task HighPriorityNoCandidateContinuesToNextDueImport()
    {
        TestHarness harness = new();
        harness.Register("first", TestSource.NoCandidate());
        harness.Register("second", TestSource.Candidate("second-item", "payload"));

        ImportRunResult result = await harness.Runner.RunOnceAsync(
            [
                Definition("first", priority: ImportPriorities.High),
                Definition("second", priority: ImportPriorities.Low)
            ],
            new ImportRuntimeState());

        await Assert.That(result.SuccessfulImportPerformed).IsTrue();
        await Assert.That(result.SuccessfulImportId).IsEqualTo("second");
        await Assert.That(result.Checks.Select(static check => check.Outcome))
            .IsEquivalentTo([ImportCheckOutcome.NoCandidate, ImportCheckOutcome.Imported]);
        await Assert.That(result.State.Imports["first"].LastCheckedAt).IsNotNull();
        await Assert.That(result.State.Imports["second"].LastSuccessfulImportAt).IsNotNull();
    }

    [Test]
    public async Task SourceFailureRecordsFailureAndContinuesToLowerPriorityImport()
    {
        TestHarness harness = new();
        harness.Register("first", TestSource.Throwing(new InvalidOperationException("source failed")));
        harness.Register("second", TestSource.Candidate("second-item", "payload"));

        ImportRunResult result = await harness.Runner.RunOnceAsync(
            [
                Definition("first", priority: ImportPriorities.High),
                Definition("second", priority: ImportPriorities.Low)
            ],
            new ImportRuntimeState());

        await Assert.That(result.SuccessfulImportId).IsEqualTo("second");
        await Assert.That(result.Checks[0].Outcome).IsEqualTo(ImportCheckOutcome.SourceFailed);
        await Assert.That(result.Checks[0].Exception).IsNotNull();
        await Assert.That(result.State.Imports["first"].LastError).IsNotNull();
    }

    [Test]
    public async Task HandlerFailureRecordsFailureAndContinuesToLowerPriorityImport()
    {
        TestHarness harness = new();
        harness.Register("first", TestSource.Candidate("first-item", "payload"), TestHandler.Failure("handler failed"));
        harness.Register("second", TestSource.Candidate("second-item", "payload"));

        ImportRunResult result = await harness.Runner.RunOnceAsync(
            [
                Definition("first", priority: ImportPriorities.High),
                Definition("second", priority: ImportPriorities.Low)
            ],
            new ImportRuntimeState());

        await Assert.That(result.SuccessfulImportId).IsEqualTo("second");
        await Assert.That(result.Checks[0].Outcome).IsEqualTo(ImportCheckOutcome.HandlerFailed);
        await Assert.That(result.State.Imports["first"].LastError!.Message).IsEqualTo("handler failed");
    }

    [Test]
    public async Task SuccessfulImportPassesStreamToHandlerAndCommitsCursorUpdates()
    {
        TestHandler handler = TestHandler.Success(new JsonObject { ["handled"] = true });
        TestHarness harness = new();
        harness.Register(
            "orders",
            TestSource.Candidate("orders-001", "id,name\n1,Ada\n", new JsonObject { ["polled"] = true }),
            handler);

        ImportRunResult result = await harness.Runner.RunOnceAsync([Definition("orders")], new ImportRuntimeState());

        await Assert.That(handler.Payloads).Count().IsEqualTo(1);
        await Assert.That(handler.Payloads[0]).Contains("Ada");
        await Assert.That(result.SuccessfulImportPerformed).IsTrue();
        await Assert.That(result.State.Imports["orders"].Cursor["polled"]!.GetValue<bool>()).IsTrue();
        await Assert.That(result.State.Imports["orders"].Cursor["handled"]!.GetValue<bool>()).IsTrue();
        await Assert.That(result.State.Imports["orders"].LastSuccessfulImportAt).IsNotNull();
    }

    [Test]
    public async Task RemovedImportsDoNotAutomaticallyDeleteExistingState()
    {
        TestHarness harness = new();
        ImportRuntimeState existingState = new()
        {
            Imports =
            {
                ["removed"] = new ImportState
                {
                    Cursor = new JsonObject { ["keep"] = true }
                }
            }
        };

        ImportRunResult result = await harness.Runner.RunOnceAsync([], existingState);

        await Assert.That(result.State.Imports.ContainsKey("removed")).IsTrue();
        await Assert.That(result.State.Imports["removed"].Cursor["keep"]!.GetValue<bool>()).IsTrue();
    }

    [Test]
    public async Task RunnerSnapshotsImportListAtStartOfCall()
    {
        List<IImportDefinition> imports = [Definition("first")];
        TestHarness harness = new();
        harness.Register("first", TestSource.NoCandidate(onPoll: () => imports.Add(Definition("second"))));
        harness.Register("second", TestSource.Candidate("second-item", "payload"));

        ImportRunResult result = await harness.Runner.RunOnceAsync(imports, new ImportRuntimeState());

        await Assert.That(imports).Count().IsEqualTo(2);
        await Assert.That(result.Checks).Count().IsEqualTo(1);
        await Assert.That(result.Checks[0].ImportId).IsEqualTo("first");
        await Assert.That(result.State.Imports.ContainsKey("second")).IsFalse();
    }

    private static ImportDefinition<TestConfiguration> Definition(
        string id,
        int? priority = null,
        TimeSpan? pollingInterval = null) =>
        new()
        {
            Id = id,
            Priority = priority ?? ImportPriorities.Normal,
            Polling = new PollingOptions { Interval = pollingInterval ?? TimeSpan.FromMinutes(1) },
            Configuration = new TestConfiguration("test")
        };

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

    private sealed record TestConfiguration(string Name) : IImportConfiguration;

    private sealed class TestHarness
    {
        private readonly Dictionary<string, ImportSourceFactoryRegistration> _sourceFactories = [];
        private readonly Dictionary<string, ImportHandlerRegistration> _handlers = [];

        public TestHarness(TimeProvider? timeProvider = null)
        {
            Runner = new ImportRunner(_sourceFactories, _handlers, timeProvider);
        }

        public ImportRunner Runner { get; }

        public void Register(string importId, TestSource? source = null, TestHandler? handler = null)
        {
            _sourceFactories[importId] = ImportSourceFactoryRegistration.Create(
                new TestSourceFactory(source ?? TestSource.NoCandidate()));
            _handlers[importId] = ImportHandlerRegistration.Create(handler ?? TestHandler.Success());
        }
    }

    private sealed class TestSourceFactory : IImportSourceFactory<TestConfiguration>
    {
        private readonly TestSource _source;

        public TestSourceFactory(TestSource source)
        {
            _source = source;
        }

        public ValueTask<IImportSource> CreateAsync(
            ImportDefinition<TestConfiguration> definition,
            ImportSourceFactoryContext<TestConfiguration> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IImportSource>(_source);
    }

    private sealed class TestSource : IImportSource
    {
        private readonly ImportPollResult? _result;
        private readonly Exception? _exception;
        private readonly List<string>? _pollOrder;
        private readonly Action? _onPoll;

        private TestSource(
            ImportPollResult? result,
            Exception? exception = null,
            List<string>? pollOrder = null,
            Action? onPoll = null)
        {
            _result = result;
            _exception = exception;
            _pollOrder = pollOrder;
            _onPoll = onPoll;
        }

        public int PollCount { get; private set; }

        public static TestSource NoCandidate(List<string>? pollOrder = null, Action? onPoll = null) =>
            new(ImportPollResult.NoCandidate(), pollOrder: pollOrder, onPoll: onPoll);

        public static TestSource Candidate(string sourceItemId, string payload, JsonObject? cursorUpdate = null) =>
            new(ImportPollResult.CandidateResult(new ImportCandidate
            {
                SourceItemId = sourceItemId,
                OpenReadAsync = _ => ValueTask.FromResult<Stream>(
                    new MemoryStream(Encoding.UTF8.GetBytes(payload)))
            }, cursorUpdate));

        public static TestSource Throwing(Exception exception) =>
            new(null, exception);

        public ValueTask<ImportPollResult> PollAsync(ImportPollContext context, CancellationToken cancellationToken)
        {
            PollCount++;
            _pollOrder?.Add(context.ImportId);
            _onPoll?.Invoke();

            if (_exception is not null)
            {
                throw _exception;
            }

            return ValueTask.FromResult(_result!);
        }
    }

    private sealed class TestHandler : IImportHandler<TestConfiguration>
    {
        private readonly ImportHandlingResult _result;

        private TestHandler(ImportHandlingResult result)
        {
            _result = result;
        }

        public List<string> Payloads { get; } = [];

        public static TestHandler Success(JsonObject? cursorUpdate = null) =>
            new(ImportHandlingResult.Success(cursorUpdate));

        public static TestHandler Failure(string message) =>
            new(ImportHandlingResult.Failure(message));

        public async ValueTask<ImportHandlingResult> HandleAsync(
            ImportHandlingContext<TestConfiguration> context,
            Stream payload,
            CancellationToken cancellationToken)
        {
            using StreamReader reader = new(payload, Encoding.UTF8);
            Payloads.Add(await reader.ReadToEndAsync(cancellationToken));
            return _result;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
