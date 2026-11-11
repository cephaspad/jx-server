using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using JXServer.Primitives.Characters;
using JXServer.Primitives.SessionLeases;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class SessionLeaseEpochFencingAdversarialTests
{
    [Fact]
    public async Task StaleEpochSnapshot_WhenLeaseHasAdvancedToNewEpochAndInGame_MustRejectSnapshotPersistence()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var leaseAppService = harness.Services.GetRequiredService<ISessionLeaseAppService>();
        var leaseRepo = harness.Services.GetRequiredService<ISessionLeaseRepository>();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();

        // 1. Create character in database with initial Level 10, Map 53, Life 1000
        var character = new Character(
            id: characterId,
            accountId: accountId,
            slotIndex: 0,
            roleName: "Kiếm Ma",
            gender: CharacterGender.Male,
            series: ElementSeries.Metal,
            mapId: 53,
            revivalMapId: 53,
            revivalPointId: 19,
            mapMpsX: 52000,
            mapMpsY: 101696,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            lucky: 10,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300);

        await charRepo.InsertAsync(character, autoSave: true);

        // 2. Setup active lease in state InGame with epoch 2 (advanced due to new login)
        var lease = new SessionLease(accountId, "127.0.0.1", 1, DateTimeOffset.UtcNow);
        lease.BeginGatewaySession(2, DateTimeOffset.UtcNow);
        Guid ticket = Guid.NewGuid();
        lease.IssueWorldHandoffTicket(characterId, "Kiếm Ma", 53, ticket, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        lease.ConsumeWorldHandoffTicket(ticket, DateTimeOffset.UtcNow, "gs-conn-2");
        await leaseRepo.UpsertAsync(lease);

        // 3. Stale worker holding snapshot from epoch 1 attempts to commit stale world snapshot (Map 1, Life 500)
        var staleSnapshot = new UpdateCharacterWorldSnapshotDto(
            CharacterId: characterId,
            MapId: 1,
            MapMpsX: 10000,
            MapMpsY: 20000,
            CurrentLife: 500,
            CurrentMana: 250,
            CurrentStamina: 100,
            LeaseEpoch: 1);

        var releaseInput = new ReleaseSessionLeaseInput(
            AccountId: accountId,
            LeaseEpoch: 1, // Stale epoch 1 vs active lease epoch 2
            CharacterSnapshot: staleSnapshot);

        await leaseAppService.CompleteLogoutOrEvictAsync(releaseInput);

        // 4. Assert: Database character MUST retain its original MapId 53 and Life 1000 (stale snapshot rejected)
        Character? refreshedChar = await charRepo.FindAsync(characterId);
        Assert.NotNull(refreshedChar);
        Assert.Equal(53, refreshedChar.MapId);
        Assert.Equal(52000, refreshedChar.MapMpsX);
        Assert.Equal(1000, refreshedChar.CurrentLife);

        // Assert: Lease epoch 2 is NOT released by stale worker
        SessionLease? currentLease = await leaseRepo.FindByAccountIdAsync(accountId);
        Assert.NotNull(currentLease);
        Assert.Equal(2, currentLease.LeaseEpoch);
        Assert.NotEqual(SessionLeaseState.Offline, currentLease.State);
    }

    [Fact]
    public async Task ValidEpochSnapshot_WhenLeaseMatchesWorkerEpoch_MustPersistSnapshotAndReleaseLease()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var leaseAppService = harness.Services.GetRequiredService<ISessionLeaseAppService>();
        var leaseRepo = harness.Services.GetRequiredService<ISessionLeaseRepository>();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();

        var character = new Character(
            id: characterId,
            accountId: accountId,
            slotIndex: 0,
            roleName: "Độc Cô",
            gender: CharacterGender.Male,
            series: ElementSeries.Fire,
            mapId: 53,
            revivalMapId: 53,
            revivalPointId: 19,
            mapMpsX: 52000,
            mapMpsY: 101696,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            lucky: 10,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300);

        await charRepo.InsertAsync(character, autoSave: true);

        // Active lease at epoch 1
        var lease = new SessionLease(accountId, "127.0.0.1", 1, DateTimeOffset.UtcNow);
        await leaseRepo.UpsertAsync(lease);

        // Worker submits valid snapshot matching epoch 1
        var validSnapshot = new UpdateCharacterWorldSnapshotDto(
            CharacterId: characterId,
            MapId: 80,
            MapMpsX: 60000,
            MapMpsY: 70000,
            CurrentLife: 888,
            CurrentMana: 444,
            CurrentStamina: 222,
            LeaseEpoch: 1,
            Level: 12,
            InventoryMoney: 10000);

        var releaseInput = new ReleaseSessionLeaseInput(
            AccountId: accountId,
            LeaseEpoch: 1,
            CharacterSnapshot: validSnapshot);

        await leaseAppService.CompleteLogoutOrEvictAsync(releaseInput);

        // Assert: Database character updated with valid snapshot
        Character? refreshedChar = await charRepo.FindAsync(characterId);
        Assert.NotNull(refreshedChar);
        Assert.Equal(80, refreshedChar.MapId);
        Assert.Equal(60000, refreshedChar.MapMpsX);
        Assert.Equal(888, refreshedChar.CurrentLife);
        Assert.Equal(12, refreshedChar.Level);
        Assert.Equal(10000, refreshedChar.InventoryMoney);

        // Assert: Lease is released to Offline
        SessionLease? currentLease = await leaseRepo.FindByAccountIdAsync(accountId);
        Assert.NotNull(currentLease);
        Assert.Equal(SessionLeaseState.Offline, currentLease.State);
    }

    [Fact]
    public async Task GatewayAuthenticatedTransition_WhenLeaseAdvancedByOneAndNoActiveChar_AllowsSnapshotPersistence()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var leaseAppService = harness.Services.GetRequiredService<ISessionLeaseAppService>();
        var leaseRepo = harness.Services.GetRequiredService<ISessionLeaseRepository>();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();

        var character = new Character(
            id: characterId,
            accountId: accountId,
            slotIndex: 0,
            roleName: "Lệnh Hồ Xung",
            gender: CharacterGender.Male,
            series: ElementSeries.Wood,
            mapId: 53,
            revivalMapId: 53,
            revivalPointId: 19,
            mapMpsX: 52000,
            mapMpsY: 101696,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            lucky: 10,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300);

        await charRepo.InsertAsync(character, autoSave: true);

        // Account re-authenticated at gateway (epoch 2, GatewayAuthenticated, no active character yet)
        var lease = new SessionLease(accountId, "127.0.0.1", 1, DateTimeOffset.UtcNow);
        lease.BeginGatewaySession(2, DateTimeOffset.UtcNow);
        await leaseRepo.UpsertAsync(lease);

        // Worker reports final world snapshot from epoch 1
        var snapshot = new UpdateCharacterWorldSnapshotDto(
            CharacterId: characterId,
            MapId: 53,
            MapMpsX: 52100,
            MapMpsY: 101750,
            CurrentLife: 999,
            CurrentMana: 450,
            CurrentStamina: 290,
            LeaseEpoch: 1);

        var releaseInput = new ReleaseSessionLeaseInput(
            AccountId: accountId,
            LeaseEpoch: 1,
            CharacterSnapshot: snapshot);

        await leaseAppService.CompleteLogoutOrEvictAsync(releaseInput);

        // Snapshot is saved because character just left world and gateway hasn't selected new character yet
        Character? refreshedChar = await charRepo.FindAsync(characterId);
        Assert.NotNull(refreshedChar);
        Assert.Equal(52100, refreshedChar.MapMpsX);
        Assert.Equal(999, refreshedChar.CurrentLife);

        // Lease is NOT released (remains GatewayAuthenticated at epoch 2)
        SessionLease? currentLease = await leaseRepo.FindByAccountIdAsync(accountId);
        Assert.NotNull(currentLease);
        Assert.Equal(2, currentLease.LeaseEpoch);
        Assert.Equal(SessionLeaseState.GatewayAuthenticated, currentLease.State);
    }

    [Fact]
    public async Task ConcurrentEvictionAndStaleSnapshot_RaceTest_AccountLockSerializesSafely()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();
        var leaseAppService = harness.Services.GetRequiredService<ISessionLeaseAppService>();
        var leaseRepo = harness.Services.GetRequiredService<ISessionLeaseRepository>();
        var charRepo = harness.Services.GetRequiredService<ICharacterRepository>();

        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();

        var character = new Character(
            id: characterId,
            accountId: accountId,
            slotIndex: 0,
            roleName: "Thần Điêu",
            gender: CharacterGender.Male,
            series: ElementSeries.Wood,
            mapId: 53,
            revivalMapId: 53,
            revivalPointId: 19,
            mapMpsX: 52000,
            mapMpsY: 101696,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            lucky: 10,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300);

        await charRepo.InsertAsync(character, autoSave: true);

        var lease = new SessionLease(accountId, "127.0.0.1", 1, DateTimeOffset.UtcNow);
        await leaseRepo.UpsertAsync(lease);

        var snap = new UpdateCharacterWorldSnapshotDto(
            CharacterId: characterId,
            MapId: 53,
            MapMpsX: 53000,
            MapMpsY: 102000,
            CurrentLife: 999,
            CurrentMana: 499,
            CurrentStamina: 299,
            LeaseEpoch: 1,
            InventoryMoney: 100);

        var task1 = leaseAppService.CompleteLogoutOrEvictAsync(new ReleaseSessionLeaseInput(accountId, 1, CharacterSnapshot: snap));
        var task2 = leaseAppService.CompleteLogoutOrEvictAsync(new ReleaseSessionLeaseInput(accountId, 1, CharacterSnapshot: null));
        var task3 = leaseAppService.CompleteLogoutOrEvictAsync(new ReleaseSessionLeaseInput(accountId, 0, CharacterSnapshot: null));

        await Task.WhenAll(task1, task2, task3);

        SessionLease? finalLease = await leaseRepo.FindByAccountIdAsync(accountId);
        Assert.NotNull(finalLease);
        Assert.Equal(SessionLeaseState.Offline, finalLease.State);
    }
}
