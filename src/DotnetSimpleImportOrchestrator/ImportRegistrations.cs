using DotnetSimpleImportOrchestrator.Abstractions;

namespace DotnetSimpleImportOrchestrator;

public sealed class ImportSourceFactoryRegistration
{
    private readonly Func<IImportDefinition, ImportState, TimeProvider, CancellationToken, ValueTask<IImportSource>> _createAsync;

    private ImportSourceFactoryRegistration(
        Func<IImportDefinition, ImportState, TimeProvider, CancellationToken, ValueTask<IImportSource>> createAsync)
    {
        _createAsync = createAsync;
    }

    public static ImportSourceFactoryRegistration Create<TConfiguration>(
        IImportSourceFactory<TConfiguration> sourceFactory)
        where TConfiguration : IImportConfiguration
    {
        ArgumentNullException.ThrowIfNull(sourceFactory);

        return new ImportSourceFactoryRegistration(async (definition, state, timeProvider, cancellationToken) =>
        {
            ImportDefinition<TConfiguration> typedDefinition = RequireTypedDefinition<TConfiguration>(definition);
            return await sourceFactory.CreateAsync(
                typedDefinition,
                new ImportSourceFactoryContext<TConfiguration>
                {
                    Definition = typedDefinition,
                    State = state,
                    TimeProvider = timeProvider
                },
                cancellationToken);
        });
    }

    internal ValueTask<IImportSource> CreateAsync(
        IImportDefinition definition,
        ImportState state,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        _createAsync(definition, state, timeProvider, cancellationToken);

    private static ImportDefinition<TConfiguration> RequireTypedDefinition<TConfiguration>(IImportDefinition definition)
        where TConfiguration : IImportConfiguration
    {
        if (definition is ImportDefinition<TConfiguration> typedDefinition)
        {
            return typedDefinition;
        }

        throw new InvalidOperationException(
            $"Import '{definition.Id}' is not an ImportDefinition<{typeof(TConfiguration).Name}>.");
    }
}

public sealed class ImportHandlerRegistration
{
    private readonly Func<IImportDefinition, ImportCandidate, ImportState, Stream, CancellationToken, ValueTask<ImportHandlingResult>> _handleAsync;

    private ImportHandlerRegistration(
        Func<IImportDefinition, ImportCandidate, ImportState, Stream, CancellationToken, ValueTask<ImportHandlingResult>> handleAsync)
    {
        _handleAsync = handleAsync;
    }

    public static ImportHandlerRegistration Create<TConfiguration>(
        IImportHandler<TConfiguration> handler)
        where TConfiguration : IImportConfiguration
    {
        ArgumentNullException.ThrowIfNull(handler);

        return new ImportHandlerRegistration((definition, candidate, state, payload, cancellationToken) =>
        {
            ImportDefinition<TConfiguration> typedDefinition = RequireTypedDefinition<TConfiguration>(definition);
            return handler.HandleAsync(
                new ImportHandlingContext<TConfiguration>
                {
                    Definition = typedDefinition,
                    SourceItemId = candidate.SourceItemId,
                    State = state,
                    Metadata = candidate.Metadata.DeepClone().AsObject()
                },
                payload,
                cancellationToken);
        });
    }

    internal ValueTask<ImportHandlingResult> HandleAsync(
        IImportDefinition definition,
        ImportCandidate candidate,
        ImportState state,
        Stream payload,
        CancellationToken cancellationToken) =>
        _handleAsync(definition, candidate, state, payload, cancellationToken);

    private static ImportDefinition<TConfiguration> RequireTypedDefinition<TConfiguration>(IImportDefinition definition)
        where TConfiguration : IImportConfiguration
    {
        if (definition is ImportDefinition<TConfiguration> typedDefinition)
        {
            return typedDefinition;
        }

        throw new InvalidOperationException(
            $"Import '{definition.Id}' is not an ImportDefinition<{typeof(TConfiguration).Name}>.");
    }
}
