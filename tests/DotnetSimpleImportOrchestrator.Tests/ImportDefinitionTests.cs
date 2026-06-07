using DotnetSimpleImportOrchestrator;

namespace DotnetSimpleImportOrchestrator.Tests;

public sealed class ImportDefinitionTests
{
    [Test]
    public async Task GenericDefinitionExposesOrchestrationFieldsAndTypedConfiguration()
    {
        TestConfiguration configuration = new("orders");
        ImportDefinition<TestConfiguration> definition = new()
        {
            Id = "orders-import",
            Priority = ImportPriorities.High,
            Polling = new PollingOptions { Interval = TimeSpan.FromMinutes(10) },
            Configuration = configuration
        };

        await Assert.That(definition.Id).IsEqualTo("orders-import");
        await Assert.That(definition.Priority).IsEqualTo(ImportPriorities.High);
        await Assert.That(definition.Polling.Interval).IsEqualTo(TimeSpan.FromMinutes(10));
        await Assert.That(definition.Configuration).IsSameReferenceAs(configuration);
    }

    [Test]
    public async Task GenericDefinitionCanBeUsedThroughMixedNonGenericView()
    {
        IReadOnlyList<IImportDefinition> imports =
        [
            Definition("alpha", new TestConfiguration("a")),
            Definition("beta", new AlternateConfiguration("b"))
        ];

        await Assert.That(imports).Count().IsEqualTo(2);
        await Assert.That(imports[0].Configuration).IsTypeOf<TestConfiguration>();
        await Assert.That(imports[1].Configuration).IsTypeOf<AlternateConfiguration>();
    }

    [Test]
    public async Task MissingPriorityUsesNormal()
    {
        ImportDefinition<TestConfiguration> definition = Definition("orders", new TestConfiguration("orders"));

        await Assert.That(definition.Priority).IsEqualTo(ImportPriorities.Normal);
    }

    private static ImportDefinition<TConfiguration> Definition<TConfiguration>(
        string id,
        TConfiguration configuration)
        where TConfiguration : IImportConfiguration =>
        new()
        {
            Id = id,
            Polling = new PollingOptions { Interval = TimeSpan.FromMinutes(1) },
            Configuration = configuration
        };

    private sealed record TestConfiguration(string Name) : IImportConfiguration;

    private sealed record AlternateConfiguration(string Name) : IImportConfiguration;
}
