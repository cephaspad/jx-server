using MemoryPack;

namespace JXServer.GameAssets.Shops;

/// <summary>
/// Immutable merchandise entry within an NPC shop (<c>settings/goods.txt</c>) per <c>JXData Standard v1</c>.
/// </summary>
[MemoryPackable]
public sealed partial record ShopGoodTemplate(
    int GoodsId,
    string? ItemCode,
    int Genre,
    int DetailType,
    int ParticularType,
    byte Level,
    int ElementSeries,
    int Price,
    int DiscountPrice = 0,
    string? CurrencyTypeOverride = null,
    int ConsumeType = 0,
    int ExpireWallClockMinutes = 0,
    int ExpireOnlineMinutes = 0);
