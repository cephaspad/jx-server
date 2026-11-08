using System.Buffers;
using System.Buffers.Binary;
using JXServer.Adapter.LegacyJX1.Ciphers;
using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Encodings;
using Xunit;

namespace JXServer.Protocol.Tests;

/// <summary>
/// Verifies bit-exact <see cref="KsgPublicKeyTable"/>, <see cref="HandshakeKeyObfuscator"/>, <see cref="KsgStreamCipher"/>,
/// <see cref="SecondLayerXorCipher"/>, <see cref="JxFramePipeProcessor"/>, and <see cref="ProtocolSizeTable"/>.
/// </summary>
public sealed class FramingAndCipherTests
{
    [Fact]
    public void PublicKeys_ShouldLoad5679Entries_WithExpectedFirstKey()
    {
        ReadOnlySpan<uint> keys = KsgPublicKeyTable.PublicKeys;
        Assert.Equal(5679, keys.Length);
        Assert.Equal(0x853B9129u, keys[0]);
    }

    [Theory]
    [InlineData(0x12345678u, 0x9ABCDEF0u)]
    [InlineData(0x00000001u, 0xFFFFFFFFu)]
    [InlineData(0xDEADBEEFu, 0xCAFEBABEu)]
    public void HandshakeKeyObfuscator_And_AccountBeginS2CPacket_ShouldRoundTripExactKeys(uint clientKey, uint serverKey)
    {
        uint obfuscatedClient = HandshakeKeyObfuscator.ObfuscateKey(clientKey);
        uint obfuscatedServer = HandshakeKeyObfuscator.ObfuscateKey(serverKey);

        Assert.Equal(clientKey, HandshakeKeyObfuscator.DeobfuscateKey(obfuscatedClient));
        Assert.Equal(serverKey, HandshakeKeyObfuscator.DeobfuscateKey(obfuscatedServer));

        AccountBeginS2CPacket pkt = AccountBeginS2CPacket.Create(clientKey, serverKey);
        Span<byte> wire = stackalloc byte[AccountBeginS2CPacket.WireFrameSize];
        pkt.WriteWireFrame(wire);

        Assert.Equal(44, BinaryPrimitives.ReadUInt16LittleEndian(wire[..2]));
        Assert.Equal(ProtocolOpcodes.s2c_accountbegin, wire[2]);

        (uint extractedClientKey, uint extractedServerKey) = HandshakeKeyObfuscator.ExtractHandshakeKeys(wire.Slice(2, 42));
        Assert.Equal(clientKey, extractedClientKey);
        Assert.Equal(serverKey, extractedServerKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(41)]
    [InlineData(127)]
    [InlineData(483)]
    public void KsgStreamCipher_ShouldRoundTripPayloadsOfAllAlignments(int length)
    {
        byte[] original = new byte[length];
        for (int i = 0; i < length; i++)
        {
            original[i] = (byte)((i * 37 + 13) & 0xFF);
        }

        byte[] working = original.ToArray();
        const uint connectionKey = 0x5A6B7C8Du;

        KsgStreamCipher.EncodeDecodeByValue(working, connectionKey);
        Assert.NotEqual(original, working);

        KsgStreamCipher.EncodeDecodeByValue(working, connectionKey);
        Assert.Equal(original, working);
    }

    [Fact]
    public void SecondLayerXorCipher_RoleListAndSelectRole_ShouldBeSymmetric()
    {
        byte[] roleListSample = new byte[1 + 35 * 2];
        for (int i = 0; i < roleListSample.Length; i++)
        {
            roleListSample[i] = (byte)(i + 1);
        }

        byte[] roleListCopy = roleListSample.ToArray();
        SecondLayerXorCipher.TransformRoleListPayload(roleListCopy);
        Assert.NotEqual(roleListSample, roleListCopy);
        SecondLayerXorCipher.TransformRoleListPayload(roleListCopy);
        Assert.Equal(roleListSample, roleListCopy);

        Span<byte> roleNameBuffer = stackalloc byte[32];
        int written = Tcvn3Encoding.WriteFixedString("Độc Cô Cầu Bại", roleNameBuffer);
        byte[] expectedNameBytes = roleNameBuffer.ToArray();

        int nNameLen = written - 1;
        SecondLayerXorCipher.TransformSelectRoleName(roleNameBuffer, nNameLen);
        Assert.False(roleNameBuffer.SequenceEqual(expectedNameBytes));

        SecondLayerXorCipher.TransformSelectRoleName(roleNameBuffer, nNameLen);
        Assert.True(roleNameBuffer.SequenceEqual(expectedNameBytes));
        Assert.Equal("Độc Cô Cầu Bại", Tcvn3Encoding.DecodeNullTerminated(roleNameBuffer));
    }

    [Fact]
    public void JxFramePipeProcessor_ShouldReassembleFragmentedAndBatchedFrames()
    {
        const uint clientKey = 0x11223344u;
        const uint serverKey = 0x55667788u;

        var processor = new JxFramePipeProcessor();
        var handshakeWriter = new ArrayBufferWriter<byte>();
        processor.WriteHandshakeFrame(handshakeWriter, clientKey, serverKey);
        Assert.Equal(44, handshakeWriter.WrittenCount);

        // Build two encrypted C2S frames (17B 0x42 + 9B 0x43)
        byte[] frame1Plain = new byte[17];
        frame1Plain[0] = ProtocolOpcodes.c2s_logiclogin;
        Guid.NewGuid().TryWriteBytes(frame1Plain.AsSpan(1, 16));

        byte[] frame2Plain = new byte[9];
        frame2Plain[0] = ProtocolOpcodes.c2s_syncend;
        BinaryPrimitives.WriteUInt32LittleEndian(frame2Plain.AsSpan(1, 4), 0x12345678u);
        BinaryPrimitives.WriteUInt32LittleEndian(frame2Plain.AsSpan(5, 4), 0x9ABCDEF0u);

        byte[] batchWire = new byte[(17 + 2) + (9 + 2)];
        BinaryPrimitives.WriteUInt16LittleEndian(batchWire.AsSpan(0, 2), 19);
        frame1Plain.CopyTo(batchWire.AsSpan(2, 17));
        KsgStreamCipher.EncodeDecodeByValue(batchWire.AsSpan(2, 17), clientKey);

        BinaryPrimitives.WriteUInt16LittleEndian(batchWire.AsSpan(19, 2), 11);
        frame2Plain.CopyTo(batchWire.AsSpan(21, 9));
        KsgStreamCipher.EncodeDecodeByValue(batchWire.AsSpan(21, 9), clientKey);

        // Feed partial 5 bytes first
        var partialSeq = new ReadOnlySequence<byte>(batchWire, 0, 5);
        Assert.False(processor.TryReadC2SFrame(ref partialSeq, out _));

        // Feed full coalesced buffer
        var fullSeq = new ReadOnlySequence<byte>(batchWire);
        Assert.True(processor.TryReadC2SFrame(ref fullSeq, out byte[] decoded1));
        Assert.Equal(frame1Plain, decoded1);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(decoded1));

        Assert.True(processor.TryReadC2SFrame(ref fullSeq, out byte[] decoded2));
        Assert.Equal(frame2Plain, decoded2);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(decoded2));
        Assert.Equal(0, fullSeq.Length);
    }
}
