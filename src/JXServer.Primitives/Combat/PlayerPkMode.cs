namespace JXServer.Primitives.Combat;

/// <summary>
/// Player normal PK state (<c>0</c>=Luyện công/Peace, <c>1</c>=Chiến đấu/Fight, <c>2</c>=Đồ sát/Slaughter).
/// </summary>
public enum PlayerPkMode : byte
{
    Peace = 0,
    Fight = 1,
    Slaughter = 2,
}
