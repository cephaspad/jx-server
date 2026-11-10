using System;
using System.Collections.Generic;
using JXServer.Contracts.Characters;
using JXServer.GameAssets.Items;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Manages a player's spatial inventory containers (<c>pos_hand</c>, <c>pos_equip[15]</c>, <c>pos_equiproom[6, 10]</c>, <c>pos_immediacy[3]</c>),
/// equipment requirement validation, and <c>KItemList::ms_ActivedEquip</c> Five-Elements hidden affix activation (`FR-012`).
/// </summary>
public sealed class PlayerInventoryGrid
{
    /// <summary>
    /// Ground-truth <c>KItemList::ms_ActivedEquip[part][0..1]</c> table from <c>KItemList.cpp</c> mapping each equipment part (<c>0..9</c>)
    /// to the two adjacent equipment parts that activate its hidden Five-Elements suffix slots (<c>1, 3, 5</c>).
    /// </summary>
    private static readonly (EquipmentPartKind SourceA, EquipmentPartKind SourceB)[] ActivatingPartsByEquipSlot =
    [
        (EquipmentPartKind.Body, EquipmentPartKind.Amulet),    // 0: Head
        (EquipmentPartKind.Ring2, EquipmentPartKind.Belt),     // 1: Body
        (EquipmentPartKind.Pendant, EquipmentPartKind.Cuff),   // 2: Belt
        (EquipmentPartKind.Amulet, EquipmentPartKind.Body),    // 3: Weapon
        (EquipmentPartKind.Weapon, EquipmentPartKind.Head),    // 4: Foot
        (EquipmentPartKind.Foot, EquipmentPartKind.Ring1),     // 5: Cuff
        (EquipmentPartKind.Belt, EquipmentPartKind.Ring2),     // 6: Amulet
        (EquipmentPartKind.Weapon, EquipmentPartKind.Head),    // 7: Ring1
        (EquipmentPartKind.Cuff, EquipmentPartKind.Pendant),   // 8: Ring2
        (EquipmentPartKind.Foot, EquipmentPartKind.Ring1),     // 9: Pendant
    ];

    private readonly PlayerItemSlot?[] _equipSlots = new PlayerItemSlot?[(int)EquipmentPartKind.Count];
    private readonly PlayerItemSlot?[,] _equipRoomGrid = new PlayerItemSlot?[ItemConsts.EquipRoomWidthCells, ItemConsts.EquipRoomHeightCells];
    private readonly PlayerItemSlot?[] _immediacySlots = new PlayerItemSlot?[ItemConsts.ImmediacyBeltSlots];
    private readonly Dictionary<uint, PlayerItemSlot> _itemsByRuntimeId = [];
    private uint _nextRuntimeItemId = 1;

    public PlayerItemSlot? HandItem { get; private set; }

    public IReadOnlyCollection<PlayerItemSlot> AllItems => _itemsByRuntimeId.Values;

    public PlayerItemSlot? GetEquippedItem(EquipmentPartKind part) =>
        (int)part >= 0 && (int)part < _equipSlots.Length ? _equipSlots[(int)part] : null;

    public PlayerItemSlot? GetImmediacyItem(byte index) =>
        index < _immediacySlots.Length ? _immediacySlots[index] : null;

    public PlayerItemSlot? GetEquipRoomItemAt(byte gridX, byte gridY) =>
        gridX < ItemConsts.EquipRoomWidthCells && gridY < ItemConsts.EquipRoomHeightCells
            ? _equipRoomGrid[gridX, gridY]
            : null;

    public bool TryGetItemByRuntimeId(uint runtimeItemId, out PlayerItemSlot? slot) =>
        _itemsByRuntimeId.TryGetValue(runtimeItemId, out slot);

    public uint AllocateRuntimeItemId() => _nextRuntimeItemId++;

    public bool TryFindFreeRect(byte width, byte height, out byte foundX, out byte foundY)
    {
        byte w = Math.Max((byte)1, width);
        byte h = Math.Max((byte)1, height);

        for (byte y = 0; y <= ItemConsts.EquipRoomHeightCells - h; y++)
        {
            for (byte x = 0; x <= ItemConsts.EquipRoomWidthCells - w; x++)
            {
                if (IsEquipRoomRectFree(x, y, w, h, ignoredItem: null))
                {
                    foundX = x;
                    foundY = y;
                    return true;
                }
            }
        }

        foundX = 0;
        foundY = 0;
        return false;
    }

    public bool IsEquipRoomRectFree(byte gridX, byte gridY, byte width, byte height, PlayerItemSlot? ignoredItem = null)
    {
        byte w = Math.Max((byte)1, width);
        byte h = Math.Max((byte)1, height);
        if (gridX + w > ItemConsts.EquipRoomWidthCells || gridY + h > ItemConsts.EquipRoomHeightCells)
        {
            return false;
        }

        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                PlayerItemSlot? occupant = _equipRoomGrid[gridX + dx, gridY + dy];
                if (occupant is not null && !ReferenceEquals(occupant, ignoredItem))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool TryAddOrAutoPlaceItem(
        PlayerItemSlot item,
        ItemContainerPlace preferredPlace,
        byte preferredX,
        byte preferredY,
        PlayerZoneState? owner = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.RuntimeItemId == 0 || _itemsByRuntimeId.ContainsKey(item.RuntimeItemId))
        {
            item.RuntimeItemId = AllocateRuntimeItemId();
        }

        // Try preferred placement first
        if (TryPlaceItemDirect(item, preferredPlace, preferredX, preferredY, owner))
        {
            return true;
        }

        // Fallback: auto-place in Immediacy belt if medicine/scroll and preferred was Immediacy
        if (preferredPlace == ItemContainerPlace.Immediacy && CanPlaceInImmediacy(item))
        {
            for (byte i = 0; i < ItemConsts.ImmediacyBeltSlots; i++)
            {
                if (_immediacySlots[i] is null && TryPlaceItemDirect(item, ItemContainerPlace.Immediacy, i, 0, owner))
                {
                    return true;
                }
            }
        }

        // Fallback: find first free WxH rectangle in EquipRoom (6x10)
        if (TryFindFreeRect(item.Template.GridWidthCells, item.Template.GridHeightCells, out byte freeX, out byte freeY))
        {
            return TryPlaceItemDirect(item, ItemContainerPlace.EquipRoom, freeX, freeY, owner);
        }

        return false;
    }

    public bool TryPlaceItemDirect(
        PlayerItemSlot item,
        ItemContainerPlace place,
        byte gridX,
        byte gridY,
        PlayerZoneState? owner = null)
    {
        switch (place)
        {
            case ItemContainerPlace.Hand:
                if (HandItem is not null)
                {
                    return false;
                }

                item.ContainerPlace = ItemContainerPlace.Hand;
                item.GridX = 0;
                item.GridY = 0;
                HandItem = item;
                _itemsByRuntimeId[item.RuntimeItemId] = item;
                return true;

            case ItemContainerPlace.Equip:
                if (gridX >= _equipSlots.Length || _equipSlots[gridX] is not null)
                {
                    return false;
                }

                var equipPart = (EquipmentPartKind)gridX;
                if (!IsMatchingEquipmentSlot(item, equipPart) || (owner is not null && !CanEquipItem(owner, item, equipPart)))
                {
                    return false;
                }

                item.ContainerPlace = ItemContainerPlace.Equip;
                item.GridX = gridX;
                item.GridY = 0;
                _equipSlots[gridX] = item;
                _itemsByRuntimeId[item.RuntimeItemId] = item;
                if (owner is not null)
                {
                    RecalculateEquipmentAndFiveElementsActivation(owner);
                }

                return true;

            case ItemContainerPlace.Immediacy:
                if (gridX >= ItemConsts.ImmediacyBeltSlots || _immediacySlots[gridX] is not null || !CanPlaceInImmediacy(item))
                {
                    return false;
                }

                item.ContainerPlace = ItemContainerPlace.Immediacy;
                item.GridX = gridX;
                item.GridY = 0;
                _immediacySlots[gridX] = item;
                _itemsByRuntimeId[item.RuntimeItemId] = item;
                return true;

            case ItemContainerPlace.EquipRoom:
            default:
                byte w = Math.Max((byte)1, item.Template.GridWidthCells);
                byte h = Math.Max((byte)1, item.Template.GridHeightCells);
                if (!IsEquipRoomRectFree(gridX, gridY, w, h, ignoredItem: item))
                {
                    return false;
                }

                item.ContainerPlace = ItemContainerPlace.EquipRoom;
                item.GridX = gridX;
                item.GridY = gridY;
                SetEquipRoomOccupancy(item, gridX, gridY, w, h, item);
                _itemsByRuntimeId[item.RuntimeItemId] = item;
                return true;
        }
    }

    public bool TryRemoveItem(uint runtimeItemId, out PlayerItemSlot? removed, PlayerZoneState? owner = null)
    {
        if (!_itemsByRuntimeId.Remove(runtimeItemId, out removed) || removed is null)
        {
            return false;
        }

        DetachFromContainer(removed);
        if (removed.ContainerPlace == ItemContainerPlace.Equip && owner is not null)
        {
            RecalculateEquipmentAndFiveElementsActivation(owner);
        }

        return true;
    }

    /// <summary>
    /// Executes an atomic move/swap/equip between <c>(srcPlace, srcX, srcY)</c> and <c>(destPlace, destX, destY)</c>
    /// or between <c>pos_hand</c> and a container coordinate (`0x5B` <c>c2s_playermoveitem</c>).
    /// </summary>
    public bool TryMoveOrSwapItem(
        PlayerZoneState owner,
        ItemContainerPlace srcPlace,
        byte srcX,
        byte srcY,
        ItemContainerPlace destPlace,
        byte destX,
        byte destY)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // Case 1: Picking up from a container into Hand, or placing/swapping from Hand into a container
        if (HandItem is null)
        {
            PlayerItemSlot? sourceItem = FindItemAt(srcPlace, srcX, srcY);
            if (sourceItem is null)
            {
                return false;
            }

            // Direct container-to-container move when DestPlace != Hand and DestPlace != None
            if (destPlace is not ItemContainerPlace.Hand and not ItemContainerPlace.None &&
                (destPlace != srcPlace || destX != srcX || destY != srcY))
            {
                return TryMoveDirectBetweenContainers(owner, sourceItem, srcPlace, destPlace, destX, destY);
            }

            DetachFromContainer(sourceItem);
            sourceItem.ContainerPlace = ItemContainerPlace.Hand;
            sourceItem.GridX = 0;
            sourceItem.GridY = 0;
            HandItem = sourceItem;
            if (srcPlace == ItemContainerPlace.Equip)
            {
                RecalculateEquipmentAndFiveElementsActivation(owner);
            }

            return true;
        }

        // HandItem is present: place or swap into destination container
        PlayerItemSlot handItem = HandItem;
        ItemContainerPlace targetPlace = destPlace is ItemContainerPlace.Hand or ItemContainerPlace.None ? srcPlace : destPlace;
        byte targetX = destPlace is ItemContainerPlace.Hand or ItemContainerPlace.None ? srcX : destX;
        byte targetY = destPlace is ItemContainerPlace.Hand or ItemContainerPlace.None ? srcY : destY;

        if (targetPlace is ItemContainerPlace.Hand or ItemContainerPlace.None)
        {
            return false;
        }

        PlayerItemSlot? existingAtTarget = FindItemAt(targetPlace, targetX, targetY);
        if (existingAtTarget is null)
        {
            HandItem = null;
            if (TryPlaceItemDirect(handItem, targetPlace, targetX, targetY, owner))
            {
                return true;
            }

            HandItem = handItem;
            return false;
        }

        // Swap HandItem with existingAtTarget if valid
        if (targetPlace == ItemContainerPlace.EquipRoom)
        {
            byte w = Math.Max((byte)1, handItem.Template.GridWidthCells);
            byte h = Math.Max((byte)1, handItem.Template.GridHeightCells);
            if (!IsEquipRoomRectFree(targetX, targetY, w, h, ignoredItem: existingAtTarget))
            {
                return false;
            }
        }
        else if (targetPlace == ItemContainerPlace.Equip)
        {
            var part = (EquipmentPartKind)targetX;
            if (!IsMatchingEquipmentSlot(handItem, part) || !CanEquipItem(owner, handItem, part))
            {
                return false;
            }
        }
        else if (targetPlace == ItemContainerPlace.Immediacy && !CanPlaceInImmediacy(handItem))
        {
            return false;
        }

        DetachFromContainer(existingAtTarget);
        HandItem = null;
        if (!TryPlaceItemDirect(handItem, targetPlace, targetX, targetY, owner))
        {
            _ = TryPlaceItemDirect(existingAtTarget, targetPlace, existingAtTarget.GridX, existingAtTarget.GridY, owner);
            HandItem = handItem;
            return false;
        }

        existingAtTarget.ContainerPlace = ItemContainerPlace.Hand;
        existingAtTarget.GridX = 0;
        existingAtTarget.GridY = 0;
        HandItem = existingAtTarget;
        return true;
    }

    public static bool CanPlaceInImmediacy(PlayerItemSlot item) =>
        item.Genre is ItemGenreKind.Medicine or ItemGenreKind.TownPortal or ItemGenreKind.MagicScript &&
        item.Template.GridWidthCells == 1 &&
        item.Template.GridHeightCells == 1;

    public static bool IsMatchingEquipmentSlot(PlayerItemSlot item, EquipmentPartKind part)
    {
        if (item.Genre != ItemGenreKind.Equipment && item.Genre != ItemGenreKind.PlatinaEquipment)
        {
            return false;
        }

        return item.DetailType switch
        {
            0 or 1 => part == EquipmentPartKind.Weapon,
            2 => part == EquipmentPartKind.Body,
            3 => part is EquipmentPartKind.Ring1 or EquipmentPartKind.Ring2,
            4 => part == EquipmentPartKind.Amulet,
            5 => part == EquipmentPartKind.Foot,
            6 => part == EquipmentPartKind.Belt,
            7 => part == EquipmentPartKind.Head,
            8 => part == EquipmentPartKind.Cuff,
            9 => part == EquipmentPartKind.Pendant,
            10 => part == EquipmentPartKind.Horse,
            11 => part == EquipmentPartKind.Mask,
            12 => part == EquipmentPartKind.Mantle,
            13 => part == EquipmentPartKind.Signet,
            14 => part == EquipmentPartKind.Shipin,
            _ => false,
        };
    }

    public static bool CanEquipItem(PlayerZoneState player, PlayerItemSlot item, EquipmentPartKind part)
    {
        if (!IsMatchingEquipmentSlot(item, part) || item.IsBroken)
        {
            return false;
        }

        foreach (ItemRequirementTemplate req in item.Template.Requirements)
        {
            string code = req.RequirementCode ?? string.Empty;
            int minVal = req.MinValue;
            if (string.Equals(code, "level", StringComparison.OrdinalIgnoreCase) && player.Level < minVal)
            {
                return false;
            }

            if (string.Equals(code, "strength", StringComparison.OrdinalIgnoreCase) && player.EffectiveStrength < minVal)
            {
                return false;
            }

            if (string.Equals(code, "dexterity", StringComparison.OrdinalIgnoreCase) && player.EffectiveDexterity < minVal)
            {
                return false;
            }

            if (string.Equals(code, "vitality", StringComparison.OrdinalIgnoreCase) && player.EffectiveVitality < minVal)
            {
                return false;
            }

            if (string.Equals(code, "energy", StringComparison.OrdinalIgnoreCase) && player.EffectiveEnergy < minVal)
            {
                return false;
            }

            if (string.Equals(code, "gender", StringComparison.OrdinalIgnoreCase) && (int)player.Gender != minVal)
            {
                return false;
            }

            if (string.Equals(code, "series", StringComparison.OrdinalIgnoreCase) && (int)player.Series != minVal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Evaluates <c>KItemList::ms_ActivedEquip</c> Five-Elements (<c>Tương Sinh</c>) hidden affix activation across all equipped items
    /// and aggregates base + active magic attribute bonuses onto <paramref name="player"/> (`FR-012`).
    /// </summary>
    public void RecalculateEquipmentAndFiveElementsActivation(PlayerZoneState player)
    {
        ArgumentNullException.ThrowIfNull(player);

        player.BonusLifeMax = 0;
        player.BonusManaMax = 0;
        player.BonusStaminaMax = 0;
        player.BonusStrength = 0;
        player.BonusDexterity = 0;
        player.BonusVitality = 0;
        player.BonusEnergy = 0;
        player.WeaponMinDamage = 0;
        player.WeaponMaxDamage = 0;
        player.BonusPhysicsDamage = 0;
        player.BonusAttackRating = 0;
        player.BonusDefense = 0;
        player.PhysicsResistPercent = 0;
        player.ColdResistPercent = 0;
        player.FireResistPercent = 0;
        player.LightResistPercent = 0;
        player.PoisonResistPercent = 0;

        for (int partIdx = 0; partIdx < _equipSlots.Length; partIdx++)
        {
            PlayerItemSlot? equip = _equipSlots[partIdx];
            if (equip is null || equip.IsBroken)
            {
                continue;
            }

            // 1. Evaluate hidden Five-Elements slot activation (slots 1, 3, 5)
            equip.ActiveHiddenSlots[0] = false;
            equip.ActiveHiddenSlots[1] = false;
            equip.ActiveHiddenSlots[2] = false;

            if (partIdx < ActivatingPartsByEquipSlot.Length)
            {
                (EquipmentPartKind srcA, EquipmentPartKind srcB) = ActivatingPartsByEquipSlot[partIdx];
                PlayerItemSlot? itemA = GetEquippedItem(srcA);
                PlayerItemSlot? itemB = GetEquippedItem(srcB);

                bool aGeneratesTarget = itemA is not null &&
                    !itemA.IsBroken &&
                    FiveElementsSeriesMatrix.Generates(itemA.Series, equip.Series);

                bool bGeneratesTarget = itemB is not null &&
                    !itemB.IsBroken &&
                    FiveElementsSeriesMatrix.Generates(itemB.Series, equip.Series);

                bool playerGeneratesTarget = FiveElementsSeriesMatrix.Generates(player.Series, equip.Series);

                int activeCount = (aGeneratesTarget ? 1 : 0) + (bGeneratesTarget ? 1 : 0);
                if (activeCount >= 1)
                {
                    equip.ActiveHiddenSlots[0] = true; // Hidden slot 1
                }

                if (activeCount >= 2)
                {
                    equip.ActiveHiddenSlots[1] = true; // Hidden slot 3
                    if (playerGeneratesTarget)
                    {
                        equip.ActiveHiddenSlots[2] = true; // Hidden slot 5
                    }
                }
            }

            // 2. Aggregate base attributes
            foreach (ItemBaseAttributeTemplate baseAttr in equip.Template.BaseAttributes)
            {
                ApplyBaseAttributeToPlayer(player, baseAttr);
            }

            // 3. Aggregate explicit magic slots (0, 2, 4) and activated hidden slots (1, 3, 5)
            for (int slotIdx = 0; slotIdx < equip.MagicAttributes.Length; slotIdx++)
            {
                ItemMagicAttributeSlot ma = equip.MagicAttributes[slotIdx];
                if (!ma.IsPopulated)
                {
                    continue;
                }

                bool isHidden = (slotIdx & 1) == 1;
                if (isHidden)
                {
                    int hiddenIdx = slotIdx / 2;
                    if (hiddenIdx >= equip.ActiveHiddenSlots.Length || !equip.ActiveHiddenSlots[hiddenIdx])
                    {
                        continue;
                    }
                }

                ApplyMagicAttributeToPlayer(player, ma);
            }
        }
    }

    public PlayerItemSlot? FindItemAt(ItemContainerPlace place, byte gridX, byte gridY) =>
        place switch
        {
            ItemContainerPlace.Hand => HandItem,
            ItemContainerPlace.Equip => GetEquippedItem((EquipmentPartKind)gridX),
            ItemContainerPlace.Immediacy => GetImmediacyItem(gridX),
            ItemContainerPlace.EquipRoom => GetEquipRoomItemAt(gridX, gridY),
            _ => null,
        };

    public List<CharacterItemDto> ToCharacterItemDtos()
    {
        var list = new List<CharacterItemDto>(_itemsByRuntimeId.Count);
        foreach (PlayerItemSlot slot in _itemsByRuntimeId.Values)
        {
            list.Add(slot.ToCharacterItemDto());
        }

        return list;
    }

    private bool TryMoveDirectBetweenContainers(
        PlayerZoneState owner,
        PlayerItemSlot sourceItem,
        ItemContainerPlace srcPlace,
        ItemContainerPlace destPlace,
        byte destX,
        byte destY)
    {
        byte origX = sourceItem.GridX;
        byte origY = sourceItem.GridY;
        PlayerItemSlot? targetOccupant = FindItemAt(destPlace, destX, destY);

        if (targetOccupant is null || ReferenceEquals(targetOccupant, sourceItem))
        {
            DetachFromContainer(sourceItem);
            if (TryPlaceItemDirect(sourceItem, destPlace, destX, destY, owner))
            {
                if (srcPlace == ItemContainerPlace.Equip && destPlace != ItemContainerPlace.Equip)
                {
                    RecalculateEquipmentAndFiveElementsActivation(owner);
                }

                return true;
            }

            _ = TryPlaceItemDirect(sourceItem, srcPlace, origX, origY, owner);
            return false;
        }

        // Swap two items directly between containers if both are 1x1 or compatible
        byte targOrigX = targetOccupant.GridX;
        byte targOrigY = targetOccupant.GridY;
        DetachFromContainer(sourceItem);
        DetachFromContainer(targetOccupant);

        if (TryPlaceItemDirect(sourceItem, destPlace, destX, destY, owner) &&
            TryPlaceItemDirect(targetOccupant, srcPlace, origX, origY, owner))
        {
            return true;
        }

        DetachFromContainer(sourceItem);
        DetachFromContainer(targetOccupant);
        _ = TryPlaceItemDirect(sourceItem, srcPlace, origX, origY, owner);
        _ = TryPlaceItemDirect(targetOccupant, destPlace, targOrigX, targOrigY, owner);
        return false;
    }

    private void DetachFromContainer(PlayerItemSlot item)
    {
        switch (item.ContainerPlace)
        {
            case ItemContainerPlace.Hand:
                if (ReferenceEquals(HandItem, item))
                {
                    HandItem = null;
                }

                break;

            case ItemContainerPlace.Equip:
                if (item.GridX < _equipSlots.Length && ReferenceEquals(_equipSlots[item.GridX], item))
                {
                    _equipSlots[item.GridX] = null;
                }

                break;

            case ItemContainerPlace.Immediacy:
                if (item.GridX < _immediacySlots.Length && ReferenceEquals(_immediacySlots[item.GridX], item))
                {
                    _immediacySlots[item.GridX] = null;
                }

                break;

            case ItemContainerPlace.EquipRoom:
                for (int y = 0; y < ItemConsts.EquipRoomHeightCells; y++)
                {
                    for (int x = 0; x < ItemConsts.EquipRoomWidthCells; x++)
                    {
                        if (ReferenceEquals(_equipRoomGrid[x, y], item))
                        {
                            _equipRoomGrid[x, y] = null;
                        }
                    }
                }

                break;
        }
    }

    private void SetEquipRoomOccupancy(
        PlayerItemSlot item,
        byte gridX,
        byte gridY,
        byte width,
        byte height,
        PlayerItemSlot? value)
    {
        _ = item;
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                _equipRoomGrid[gridX + dx, gridY + dy] = value;
            }
        }
    }

    private static void ApplyBaseAttributeToPlayer(PlayerZoneState player, ItemBaseAttributeTemplate baseAttr)
    {
        string code = baseAttr.AttributeCode ?? string.Empty;
        if (string.Equals(code, "damage_v", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "physics_damage_v", StringComparison.OrdinalIgnoreCase))
        {
            player.WeaponMinDamage += Math.Max(0, baseAttr.MinValue);
            player.WeaponMaxDamage += Math.Max(baseAttr.MinValue, baseAttr.MaxValue);
        }
        else if (string.Equals(code, "attackrating_v", StringComparison.OrdinalIgnoreCase))
        {
            player.BonusAttackRating += baseAttr.MinValue;
        }
        else if (string.Equals(code, "defense_v", StringComparison.OrdinalIgnoreCase))
        {
            player.BonusDefense += baseAttr.MinValue;
        }
    }

    private static void ApplyMagicAttributeToPlayer(PlayerZoneState player, ItemMagicAttributeSlot ma)
    {
        switch (ma.AttributeId)
        {
            case 85: // lifemax_v
                player.BonusLifeMax += ma.Value0;
                break;
            case 89: // manamax_v
                player.BonusManaMax += ma.Value0;
                break;
            case 93: // staminamax_v
                player.BonusStaminaMax += ma.Value0;
                break;
            case 97: // strength_v
                player.BonusStrength += ma.Value0;
                break;
            case 98: // dexterity_v
                player.BonusDexterity += ma.Value0;
                break;
            case 99: // vitality_v
                player.BonusVitality += ma.Value0;
                break;
            case 100: // energy_v
                player.BonusEnergy += ma.Value0;
                break;
            case 58: // armor_defense_v
                player.BonusDefense += ma.Value0;
                break;
            case 121: // addphysicsdamage_v
            case 126: // addphysicsdamage_p
                player.BonusPhysicsDamage += ma.Value0;
                break;
            case 166: // attackrating_v
                player.BonusAttackRating += ma.Value0;
                break;
            case 101: // poisonres_p
                player.PoisonResistPercent = Math.Min(CombatConsts.HardCapResistPercent, player.PoisonResistPercent + ma.Value0);
                break;
            case 102: // fireres_p
                player.FireResistPercent = Math.Min(CombatConsts.HardCapResistPercent, player.FireResistPercent + ma.Value0);
                break;
            case 103: // lightingres_p
                player.LightResistPercent = Math.Min(CombatConsts.HardCapResistPercent, player.LightResistPercent + ma.Value0);
                break;
            case 104: // physicsres_p
                player.PhysicsResistPercent = Math.Min(CombatConsts.HardCapResistPercent, player.PhysicsResistPercent + ma.Value0);
                break;
            case 105: // coldres_p
                player.ColdResistPercent = Math.Min(CombatConsts.HardCapResistPercent, player.ColdResistPercent + ma.Value0);
                break;
            default:
                player.BonusDefense += Math.Max(0, ma.Value0 / 5);
                break;
        }
    }
}
