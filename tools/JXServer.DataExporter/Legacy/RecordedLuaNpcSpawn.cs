namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Captures a world NPC spawn emitted by <c>AddNpc</c> / <c>AddNpcEx</c> + <c>SetNpcScript</c> during <c>autoexec.lua</c> evaluation.
/// </summary>
public sealed record RecordedLuaNpcSpawn(
    int NpcIndex,
    int NpcTemplateId,
    int Level,
    int Series,
    int MapId,
    int MpsX,
    int MpsY,
    bool IsBossOrReviveFlag,
    string RawNameLatin1,
    string RawScriptLatin1);
