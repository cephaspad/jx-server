namespace JXServer.Primitives.WorldZones;

/// <summary>
/// Defines the server-side action executed when a player steps onto a region trap cell (<c>TRAP_SECTION</c>).
/// </summary>
public enum PortalTrapActionKind : byte
{
    ToggleFightStateAndSetPos = 1,
    ChangeMapPortal = 2,
}
