using System.Collections.Generic;
using JXServer.Primitives.Characters;
using MemoryPack;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Immutable starter character template loaded from <c>newplayerini00.ini..09.ini</c> and <c>level_add.txt</c>.
/// </summary>
[MemoryPackable]
public sealed partial record CharacterBaseTemplate(
    ElementSeries Series,
    CharacterGender Gender,
    ushort BaseStrength,
    ushort BaseDexterity,
    ushort BaseVitality,
    ushort BaseEnergy,
    ushort BaseLucky,
    ushort InitialLifeMax,
    ushort InitialManaMax,
    ushort InitialStaminaMax,
    IReadOnlyList<StarterSkillEntry> StarterSkills,
    IReadOnlyList<StarterItemTemplate>? StarterItems = null);
