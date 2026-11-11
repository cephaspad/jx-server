namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Represents a static map object placement record (<c>KSPObj</c>) from <c>Region_S.dat</c> Section 3.
/// </summary>
public sealed record ParsedRegionObj(
    int RegionX,
    int RegionY,
    int ObjDataId,
    short State,
    int MpsX,
    int MpsY,
    sbyte Dir,
    string ScriptPath);
