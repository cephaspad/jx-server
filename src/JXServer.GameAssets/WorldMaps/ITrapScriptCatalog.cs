namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Read-only catalog mapping 32-bit <c>uTrapId</c> (<c>g_FileName2Id</c> script hashes) to <see cref="PortalTrapBindingTemplate"/> definitions.
/// </summary>
public interface ITrapScriptCatalog
{
    /// <summary>
    /// Gets the total number of indexed trap bindings.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Attempts to retrieve the <see cref="PortalTrapBindingTemplate"/> for the specified 32-bit <paramref name="trapId"/>.
    /// </summary>
    bool TryGetTrapBinding(uint trapId, out PortalTrapBindingTemplate? binding);
}
