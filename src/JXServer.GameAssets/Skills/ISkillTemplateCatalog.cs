using System.Collections.Generic;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Read-only catalog of skill templates loaded from <c>JXData Standard v1</c>.
/// </summary>
public interface ISkillTemplateCatalog
{
    int Count { get; }

    IReadOnlyCollection<SkillTemplate> AllSkills { get; }

    bool TryGetSkill(int skillId, out SkillTemplate? skill);

    bool TryGetSkillByCode(string code, out SkillTemplate? skill);

    int GetBasicWeaponSkillId(int weaponDetailOrParticular);
}
