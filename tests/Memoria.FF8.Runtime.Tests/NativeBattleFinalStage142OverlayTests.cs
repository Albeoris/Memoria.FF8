using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFinalStage142OverlayTests
{
    [Fact]
    public void Overlay_emits_six_ordered_packets_with_native_environment_origin_and_seventy_two_bytes()
    {
        NativeBattleFinalStage142State initial = WarmState(0xA5);
        NativeBattleFinalStage142Command command = new(NativeBattleFinalStage142CommandKind.Overlay, 2048, 64, 3, 0x1D9A420);
        NativeBattleFinalStage142OverlayStep result = NativeBattleFinalStage142Overlay.Build(initial, command, Environment(), 0xAB123456);
        Assert.Equal(new[] { 12, 12, 16, 12, 12, 8 }, result.Packets.Select(packet => packet.AllocatedBytes));
        Assert.Equal(new uint[] { 0x1D9A420, 0x1D9A42C, 0x1D9A438, 0x1D9A448, 0x1D9A454, 0x1D9A460 }, result.Packets.Select(packet => packet.Address));
        Assert.All(result.Packets, packet => Assert.Equal(4385, packet.Bucket));
        byte[] bytes = result.State.ReadOverlay(command.PacketAddress, 72);
        Assert.Equal(0x02A5A5A5u, Dword(bytes, 0));
        Assert.Equal(0xE3000140u, Dword(bytes, 4));
        Assert.Equal(0xE4035E7Fu, Dword(bytes, 8));
        Assert.Equal(0x01A5A5A5u, Dword(bytes, 12));
        Assert.Equal(0xE5000140u, Dword(bytes, 16));
        Assert.Equal(0xA5A5A5A5u, Dword(bytes, 20));
        Assert.Equal(0x03000000u, Dword(bytes, 24));
        Assert.Equal(0x62123456u, Dword(bytes, 28));
        Assert.Equal(0x00000001u, Dword(bytes, 32));
        Assert.Equal(0x000300FFu, Dword(bytes, 36));
        Assert.Equal(0xE303C000u, Dword(bytes, 44));
        Assert.Equal(0xE403FCFFu, Dword(bytes, 48));
        Assert.Equal(0xE5078000u, Dword(bytes, 56));
        Assert.Equal(0xA5A5A5A5u, Dword(bytes, 60));
        Assert.Equal(0x01A5A5A5u, Dword(bytes, 64));
        Assert.Equal(0xE1000640u, Dword(bytes, 68));
        Assert.Equal(initial.OverlayCursor, result.State.OverlayCursor);
        Assert.All(initial.ReadOverlay(command.PacketAddress, 72), value => Assert.Equal((byte)0xA5, value));
    }

    [Fact]
    public void Unknown_links_and_offset_padding_remain_unknown_while_rectangle_tag_is_overwritten()
    {
        NativeBattleFinalStage142State initial = NativeBattleFinalStage142State.FromColdProcess();
        NativeBattleGfPacketByteState[] unknown = Enumerable.Repeat(NativeBattleGfPacketByteState.Unknown, 72).ToArray();
        initial = initial.WriteOverlay(0x1D9A020, new byte[72].ToImmutableArray(), unknown.ToImmutableArray());
        NativeBattleFinalStage142OverlayStep result = NativeBattleFinalStage142Overlay.Build(initial, new(NativeBattleFinalStage142CommandKind.Overlay, 0, -1, -1, 0x1D9A020), Environment(), 0xFF010203);
        Assert.Throws<NotSupportedException>(() => result.State.ReadOverlay(0x1D9A020, 3));
        Assert.Throws<NotSupportedException>(() => result.State.ReadOverlay(0x1D9A034, 4));
        Assert.Equal(0x03000000u, Dword(result.State.ReadOverlay(0x1D9A038, 4), 0));
        Assert.Equal(0xFFFF00FFu, Dword(result.State.ReadOverlay(0x1D9A044, 4), 0));
        Assert.Equal(0xE1000FFFu, Dword(result.State.ReadOverlay(0x1D9A064, 4), 0));
        Assert.All(result.Packets[1].WriteMask.AsSpan().Slice(8).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        NativeBattleFinalStage142State restored = JsonSerializer.Deserialize<NativeBattleFinalStage142State>(JsonSerializer.Serialize(result.State))!;
        Assert.Equal(result.State.ReadOverlay(0x1D9A038, 16), restored.ReadOverlay(0x1D9A038, 16));
    }

    [Fact]
    public void Overlay_rejects_missing_environment_wrong_command_and_bank_crossing()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess();
        NativeBattleFinalStage142Command command = new(NativeBattleFinalStage142CommandKind.Overlay, 0, 0, 0, 0x1D9A020);
        Assert.Throws<InvalidDataException>(() => NativeBattleFinalStage142Overlay.Build(state, command, default, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleFinalStage142Overlay.Build(state, command with { Kind = NativeBattleFinalStage142CommandKind.FarColor }, Environment(), 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleFinalStage142Overlay.Build(state, command with { PacketAddress = 0x1D9A420 - 71 }, Environment(), 0));
    }

    private static NativeBattleFinalStage142State WarmState(byte value)
    {
        return NativeBattleFinalStage142State.FromColdProcess().WriteOverlay(0x1D9A420, Enumerable.Repeat(value, 1024).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 1024).ToImmutableArray()) with { OverlayCursor = 0x1D9A468 };
    }

    private static ImmutableArray<byte> Environment()
    {
        byte[] bytes = new byte[92];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, 320);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 320);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), 216);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), -99);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), -88);
        return bytes.ToImmutableArray();
    }

    private static uint Dword(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    }
}
