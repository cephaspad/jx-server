using MemoryPack;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Immutable starter village spawn template loaded from <c>nativeplacelist.ini</c>, <c>revivalid.ini</c>, and <c>revivepos.ini</c>.
/// </summary>
[MemoryPackable]
public sealed partial record StarterVillageTemplate(
    ushort MapId,
    string VillageNameUnicode,
    int RevivalId,
    int SpawnMpsX,
    int SpawnMpsY);
