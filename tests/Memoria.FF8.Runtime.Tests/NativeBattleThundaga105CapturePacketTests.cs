using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105CapturePacketTests
{
    [Fact]
    public void HeadInsertionExecutesOffsetThenAreaThenRestoreAndNeverLinksTheBlackQuad()
    {
        NativeBattleThundaga105CaptureWrite capture = Capture();
        byte[] lengths = new byte[256];
        for (int opcode = 0xE1; opcode <= 0xE6; opcode++)
        {
            lengths[opcode] = 1;
        }
        ImmutableArray<NativeBattleLogicalCommand> commands = capture.Ordering.Traverse(lengths.ToImmutableArray());
        Assert.Equal(new byte[] { 0xE5, 0xE3, 0xE4, 0xE3, 0xE4, 0xE5, 0xE1, 0xE2 }, commands.Select(command => command.Opcode));
        Assert.All(commands, command => Assert.Equal(NativeBattleThundaga105CaptureMemory.PacketBank, command.Packet.Bank));
        Assert.Equal(0xE5000000u | 704u | 256u << 11, commands[0].Words[0]);
        Assert.All(capture.Ordering.Proxies, proxy =>
        {
            Assert.Equal((ushort)0, proxy.Flags20);
            Assert.Equal(new int[] { 0, 0, 0, 0 }, proxy.VertexPayload4.ToArray());
        });
        Assert.Equal(0x20C1C + 24, capture.Cursor);
        Assert.Equal(0, capture.GeometryCount);
        Assert.Equal(0x05000000u, BinaryPrimitives.ReadUInt32LittleEndian(capture.Memory.Arena!.Read(0x20C1C, 4)));
        Assert.Equal(0x28000000u, BinaryPrimitives.ReadUInt32LittleEndian(capture.Memory.Arena.Read(0x20C20, 4)));
        Assert.Equal((byte)0x5A, capture.Memory.Arena.Read(0x20C1C + 24, 1)[0]);
    }

    [Fact]
    public void ColdJsonRetainsThePhysicalStaticLinksForTheNextAliasedTraversal()
    {
        NativeBattleThundaga105CaptureWrite capture = Capture();
        NativeBattleThundaga105CaptureWrite restored = JsonSerializer.Deserialize<NativeBattleThundaga105CaptureWrite>(JsonSerializer.Serialize(capture))!;
        byte[] lengths = new byte[256];
        for (int opcode = 0xE1; opcode <= 0xE6; opcode++)
        {
            lengths[opcode] = 1;
        }
        ImmutableArray<NativeBattleLogicalCommand> original = capture.Ordering.Traverse(lengths.ToImmutableArray());
        ImmutableArray<NativeBattleLogicalCommand> aliased = restored.Ordering.ResetAfterDraw45D080().Traverse(lengths.ToImmutableArray(), (reference, count) => restored.Memory.Thundaga105Capture!.Read(reference.ByteOffset, count));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(aliased));
        Assert.Equal(new byte[4], capture.Memory.Textures!.ReadVram().AsSpan(2 * (256 * 1024 + 704), 4).ToArray());
    }

    private static NativeBattleThundaga105CaptureWrite Capture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear().Write(0x20C1C + 24, [0x5A]), PrimitiveProcess = memory.PrimitiveProcess!.Initialize500400() };
        NativeBattleThundaga105CaptureTransform transform = NativeBattleThundaga105CaptureGeometry.Transform6D5620(new byte[8].ToImmutableArray(), new(new(0, 0, 0), new(128, 0, 128)), new(null, 512, 160, 108));
        return NativeBattleThundaga105CapturePackets.Append6D5620(memory, NativeBattleStageState.FromColdProcess(), NativeBattleLogicalOrdering.Clear(), 0x20C1C, default, transform, null!);
    }
}
