namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Captures a dynamic map trap cell emitted by <c>AddMapTrap</c> during <c>autoexec.lua</c> evaluation.
/// </summary>
public sealed record RecordedLuaMapTrap(
    int MapId,
    int MpsX,
    int MpsY,
    string RawScriptLatin1);
