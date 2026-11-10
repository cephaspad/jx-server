using System.Collections.Generic;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Read-only catalog of missile templates loaded from <c>JXData Standard v1</c>.
/// </summary>
public interface IMissileTemplateCatalog
{
    int Count { get; }

    IReadOnlyCollection<MissileTemplate> AllMissiles { get; }

    bool TryGetMissile(int missileId, out MissileTemplate? missile);

    bool TryGetMissileByCode(string code, out MissileTemplate? missile);
}
