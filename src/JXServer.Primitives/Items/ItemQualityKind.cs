namespace JXServer.Primitives.Items;

/// <summary>
/// Item quality tier (<c>0</c>=Normal White/Blue, <c>1</c>=Golden, <c>2</c>=Purple Enchased, <c>3</c>=Platina, <c>4</c>=Broken).
/// </summary>
public enum ItemQualityKind : byte
{
    Normal = 0,
    Golden = 1,
    Purple = 2,
    Platina = 3,
    Broken = 4,
}
