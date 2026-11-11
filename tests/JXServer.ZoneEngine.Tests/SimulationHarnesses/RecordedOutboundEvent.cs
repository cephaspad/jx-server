namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Immutable frame-stamped outbound notification entry captured by <see cref="PlayerOutboundEventRecorder"/> (<c>FR-009</c>).
/// </summary>
public sealed record RecordedOutboundEvent(
    long Frame,
    OutboundNotificationDomain Domain,
    string MethodName,
    object?[] Arguments);
