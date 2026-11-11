namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Represents a static NPC placement record (<c>KSPNpc</c>) from <c>Region_S.dat</c> Section 2.
/// </summary>
public sealed record ParsedRegionNpc(
    int RegionX,
    int RegionY,
    int TemplateId,
    int MpsX,
    int MpsY,
    bool IsSpecialNpc,
    string RawCustomName,
    short Level,
    short CurFrame,
    short Kind,
    byte Camp,
    byte Series,
    string ScriptPath);
