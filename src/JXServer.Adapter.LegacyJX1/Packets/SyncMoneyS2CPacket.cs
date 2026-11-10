using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_syncmoney</c> (<c>0x68</c>, <c>13</c> bytes — <c>PLAYER_MONEY_SYNC</c>):
/// synchronizes the player's inventory gold, repository gold, and trade gold (`FR-012`).
/// </summary>
public readonly record struct SyncMoneyS2CPacket(
    int InventoryMoney,
    int RepositoryMoney,
    int TradeMoney = 0)
{
    public const int PayloadSize = 13;

    public static bool TryParse(ReadOnlySpan<byte> payload, out SyncMoneyS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_syncmoney)
        {
            packet = default;
            return false;
        }

        packet = new SyncMoneyS2CPacket(
            InventoryMoney: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            RepositoryMoney: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            TradeMoney: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_syncmoney;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), InventoryMoney);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), RepositoryMoney);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), TradeMoney);
    }
}
