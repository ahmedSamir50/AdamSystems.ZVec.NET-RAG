namespace ZVecRagApp;

/// <summary>
/// Opens native vector collections off the UI thread (G4).
/// Exception to ingest Task.Run ban: native collection open only. Never call from MAUI UI thread.
/// </summary>
public static class BackgroundCollectionOpener
{
    public static Task OpenAsync(Func<CancellationToken, Task> openNativeCollection, CancellationToken cancellationToken)
        => Task.Run(() => openNativeCollection(cancellationToken), cancellationToken);
}
