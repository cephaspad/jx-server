using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Drop weight table across equipment parts (weapon subtypes `0..5` and armor/jewelry slots) for a magic affix.
/// </summary>
[MemoryPackable]
public sealed partial record EquipPartWeightsTemplate(
    int Sword = 0,
    int Blade = 0,
    int Club = 0,
    int Spear = 0,
    int Hammer = 0,
    int DualBlade = 0,
    int Dart = 0,
    int Knife = 0,
    int Crossbow = 0,
    int Armor = 0,
    int Ring = 0,
    int Amulet = 0,
    int Boot = 0,
    int Belt = 0,
    int Helm = 0,
    int Cuff = 0,
    int Pendant = 0,
    int Horse = 0)
{
    public int GetWeightForEquip(int detailType, int particularType) =>
        detailType switch
        {
            0 => particularType switch
            {
                0 => Sword,
                1 => Blade,
                2 => Club,
                3 => Spear,
                4 => Hammer,
                5 => DualBlade,
                _ => Sword,
            },
            1 => particularType switch
            {
                0 => Dart,
                1 => Knife,
                2 => Crossbow,
                _ => Dart,
            },
            2 => Armor,
            3 => Ring,
            4 => Amulet,
            5 => Boot,
            6 => Belt,
            7 => Helm,
            8 => Cuff,
            9 => Pendant,
            10 => Horse,
            _ => 0,
        };
}
