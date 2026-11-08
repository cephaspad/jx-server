using JXServer.Primitives.Characters;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Read-only registry providing starter character templates (<c>newplayerini00..09.ini</c>),
/// starter village spawn coordinates (<c>nativeplacelist.ini</c>, <c>revivepos.ini</c>),
/// and role name filter validation (<c>goddess_rolename.flt</c>).
/// </summary>
public interface IPlayerTemplateCatalog
{
    /// <summary>
    /// Gets the 10 starter character templates indexed by <c>(ElementSeries, CharacterGender)</c>.
    /// </summary>
    IReadOnlyCollection<CharacterBaseTemplate> CharacterTemplates { get; }

    /// <summary>
    /// Gets the starter village templates loaded from <c>nativeplacelist.ini</c> and <c>revivepos.ini</c>.
    /// </summary>
    IReadOnlyCollection<StarterVillageTemplate> StarterVillages { get; }

    /// <summary>
    /// Attempts to retrieve the starter template for the specified five-element series and gender.
    /// </summary>
    bool TryGetCharacterTemplate(ElementSeries series, CharacterGender gender, out CharacterBaseTemplate? template);

    /// <summary>
    /// Attempts to retrieve the starter village template for the specified village map ID.
    /// </summary>
    bool TryGetStarterVillage(ushort villageMapId, out StarterVillageTemplate? village);

    /// <summary>
    /// Determines whether the candidate character name violates any prohibited pattern in <c>goddess_rolename.flt</c>
    /// or contains illegal control characters.
    /// </summary>
    bool IsRoleNameProhibited(string roleNameUnicode);
}
