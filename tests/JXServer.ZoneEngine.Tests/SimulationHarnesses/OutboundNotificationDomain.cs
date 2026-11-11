namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Classifies recorded outbound notification events by functional domain (<c>FR-009</c>, <c>SC-005</c>).
/// </summary>
public enum OutboundNotificationDomain : byte
{
    World = 0,
    Combat = 1,
    Inventory = 2,
    Chat = 3,
    CustomModule = 4,
}
