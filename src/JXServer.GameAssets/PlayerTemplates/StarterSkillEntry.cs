using MemoryPack;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Starter skill entry loaded from <c>newplayerini00.ini..09.ini</c>.
/// </summary>
[MemoryPackable]
public readonly partial record struct StarterSkillEntry(ushort SkillId, byte Level);
