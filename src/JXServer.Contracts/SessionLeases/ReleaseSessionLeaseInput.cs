using JXServer.Contracts.Characters;

namespace JXServer.Contracts.SessionLeases;

/// <summary>
/// Input DTO for releasing a session lease and optionally persisting a character world snapshot.
/// </summary>
public sealed record ReleaseSessionLeaseInput(
    Guid AccountId,
    long LeaseEpoch,
    UpdateCharacterWorldSnapshotDto? CharacterSnapshot = null,
    bool OnlyIfGatewayAuthenticated = false);
