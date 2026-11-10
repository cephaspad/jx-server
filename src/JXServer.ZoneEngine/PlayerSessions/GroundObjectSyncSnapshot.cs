namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Read-only snapshot of a ground object used for <c>0x64</c> (<c>s2c_objadd</c>) outbound synchronization (`FR-011`).
/// </summary>
public readonly record struct GroundObjectSyncSnapshot(
    int ObjId,
    int DataId,
    byte Dir,
    int MpsX,
    int MpsY,
    int MoneyNum,
    int ItemId,
    byte ItemWidth,
    ushort Genre,
    byte ItemHeight,
    byte ColorId,
    string NameUtf8);
