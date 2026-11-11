using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.Combat;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Singleton read-only catalog of missile templates loaded from <c>JXData Standard v1</c> (`FR-001`).
/// </summary>
public sealed class MissileTemplateCatalog : IMissileTemplateCatalog
{
    private readonly FrozenDictionary<int, MissileTemplate> _missilesById;
    private readonly FrozenDictionary<string, MissileTemplate> _missilesByCode;

    public int Count => _missilesById.Count;

    public IReadOnlyCollection<MissileTemplate> AllMissiles => _missilesById.Values;

    public MissileTemplateCatalog(IOptions<GameAssetsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<MissileTemplate> missiles;
        if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out _) &&
            bundle is not null &&
            bundle.Missiles.Count > 0)
        {
            missiles = bundle.Missiles;
        }
        else
        {
            missiles = CreateDefaultMissiles();
        }

        var byId = new Dictionary<int, MissileTemplate>();
        var byCode = new Dictionary<string, MissileTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (MissileTemplate m in missiles)
        {
            byId[m.Id] = m;
            byCode[m.Code] = m;
        }

        if (missiles.Count < 20)
        {
            foreach (MissileTemplate fallback in CreateDefaultMissiles())
            {
                if (!byId.ContainsKey(fallback.Id))
                {
                    byId[fallback.Id] = fallback;
                    byCode.TryAdd(fallback.Code, fallback);
                }
            }
        }

        _missilesById = byId.ToFrozenDictionary();
        _missilesByCode = byCode.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetMissile(int missileId, out MissileTemplate? missile) =>
        _missilesById.TryGetValue(missileId, out missile);

    public bool TryGetMissileByCode(string code, out MissileTemplate? missile) =>
        _missilesByCode.TryGetValue(code, out missile);

    public static IReadOnlyList<MissileTemplate> CreateDefaultMissiles() =>
    [
        new MissileTemplate(
            Id: 1,
            Code: "fire-bolt-missile",
            Name: "Hỏa Cầu",
            MoveKind: MissileMoveKind.Line64,
            FollowKind: "none",
            SpeedMpsPerFrame: 32,
            HeightMps: 0,
            ZSpeedMpsPerFrame: 0,
            ZAccelMpsPerFrame2: 0,
            LifeFrames: 24,
            HitRadiusMps: 64,
            CollideRadiusCells: 1,
            DamageRadiusCells: 1,
            IsRangeDamage: false,
            DamageIntervalFrames: 0,
            PierceCount: 1,
            CollideVanish: true,
            CanCollideFriend: false,
            AutoExplode: false,
            MustHit: false,
            SubMissileId: 0),
        new MissileTemplate(
            Id: 2,
            Code: "ice-shard-missile",
            Name: "Băng Tiễn",
            MoveKind: MissileMoveKind.Line64,
            FollowKind: "none",
            SpeedMpsPerFrame: 32,
            HeightMps: 0,
            ZSpeedMpsPerFrame: 0,
            ZAccelMpsPerFrame2: 0,
            LifeFrames: 24,
            HitRadiusMps: 64,
            CollideRadiusCells: 1,
            DamageRadiusCells: 1,
            IsRangeDamage: false,
            DamageIntervalFrames: 0,
            PierceCount: 1,
            CollideVanish: true,
            CanCollideFriend: false,
            AutoExplode: false,
            MustHit: false,
            SubMissileId: 0),
        new MissileTemplate(
            Id: 3,
            Code: "poison-bolt-missile",
            Name: "Độc Chưởng",
            MoveKind: MissileMoveKind.Line64,
            FollowKind: "none",
            SpeedMpsPerFrame: 28,
            HeightMps: 0,
            ZSpeedMpsPerFrame: 0,
            ZAccelMpsPerFrame2: 0,
            LifeFrames: 24,
            HitRadiusMps: 64,
            CollideRadiusCells: 1,
            DamageRadiusCells: 1,
            IsRangeDamage: false,
            DamageIntervalFrames: 0,
            PierceCount: 1,
            CollideVanish: true,
            CanCollideFriend: false,
            AutoExplode: false,
            MustHit: false,
            SubMissileId: 0),
        new MissileTemplate(
            Id: 4,
            Code: "lightning-bolt-missile",
            Name: "Lôi Điện",
            MoveKind: MissileMoveKind.Line64,
            FollowKind: "none",
            SpeedMpsPerFrame: 36,
            HeightMps: 0,
            ZSpeedMpsPerFrame: 0,
            ZAccelMpsPerFrame2: 0,
            LifeFrames: 20,
            HitRadiusMps: 64,
            CollideRadiusCells: 1,
            DamageRadiusCells: 1,
            IsRangeDamage: false,
            DamageIntervalFrames: 0,
            PierceCount: 1,
            CollideVanish: true,
            CanCollideFriend: false,
            AutoExplode: false,
            MustHit: false,
            SubMissileId: 0),
    ];
}
