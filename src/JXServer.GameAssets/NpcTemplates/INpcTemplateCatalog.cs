namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// Read-only catalog of static NPC templates (<c>settings/npcs.txt</c>) and global dialog NPC spawns (<c>autoexec.lua</c>).
/// </summary>
public interface INpcTemplateCatalog
{
    /// <summary>
    /// Gets the total number of loaded NPC templates.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Attempts to retrieve an <see cref="NpcTemplate"/> by its 0-based <paramref name="templateId"/>.
    /// </summary>
    bool TryGetTemplate(int templateId, out NpcTemplate? template);

    /// <summary>
    /// Gets the additional static dialog NPC spawns configured in <c>script/global/autoexec.lua</c> for <paramref name="mapId"/>.
    /// </summary>
    IReadOnlyList<StaticNpcSpawnTemplate> GetAutoExecSpawnsForMap(ushort mapId);
}
