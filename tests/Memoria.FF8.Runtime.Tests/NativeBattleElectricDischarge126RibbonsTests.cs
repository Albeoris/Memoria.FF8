using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126RibbonsTests
{
    [Theory]
    [InlineData(5000u, 8000, 5000u)]
    [InlineData(1000u, 8000, 4000u)]
    public void World_direction_stack_word_is_the_final_projection_divisor(uint depth, ushort focal, uint expected)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sz = [null, null, null, depth] } };
        Assert.Equal(expected, NativeBattleRibbonProjection.CaptureWorldDirectionWord(memory, new(null, focal, null, null)));
        Assert.Throws<NotSupportedException>(() => NativeBattleRibbonProjection.CaptureWorldDirectionWord(memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() }, new(null, focal, null, null)));
    }

    [Fact]
    public void Clipped_first_world_edge_retains_the_actual_projection_stack_word_for_the_next_edge()
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        frame = Point(frame, WorldPointsOffset, 0, 90, 200, -1, 32, false, 2048, Address(frame, WorldPointsOffset, 1));
        frame = Point(frame, WorldPointsOffset, 1, 100, 200, 512, 32, false, 2048, Address(frame, WorldPointsOffset, 2));
        frame = Point(frame, WorldPointsOffset, 2, 116, 200, 512, 32, false, 2048, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleElectricDischarge126Ribbons.DrawProjected(frame, Address(frame, WorldPointsOffset, 0), 3, 0, Resources()));
        var actual = NativeBattleElectricDischarge126Ribbons.DrawProjected(frame, Address(frame, WorldPointsOffset, 0), 3, 0, Resources(), 5000);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        NativeBattleRibbonDirectionStep direction = NativeBattleRibbonDirection.Resolve(16, 0, 16, true, 5000, 0);
        Assert.Equal(unchecked((short)(100 - (direction.X >> 12))), BinaryPrimitives.ReadInt16LittleEndian(packet.Data.AsSpan().Slice(8)));
        Assert.Equal(unchecked((short)(200 - (direction.Y >> 12))), BinaryPrimitives.ReadInt16LittleEndian(packet.Data.AsSpan().Slice(10)));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(frame.State, actual.Frame.State);
    }

    [Theory]
    [InlineData(false, 49, 199, 201)]
    [InlineData(true, 49, 198, 202)]
    public void Native_policy_uses_only_its_selected_width_field_divisor_and_depth_shift(bool anchored, int bucket, short leftY, short rightY)
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        int bank = anchored ? BonePointsOffset : WorldPointsOffset;
        frame = Point(frame, bank, 0, 100, 200, 512, anchored ? (short)96 : (short)32, anchored, 2048, Address(frame, bank, 1));
        frame = Point(frame, bank, 1, 116, 200, 512, anchored ? (short)96 : (short)32, anchored, 1024, 0);
        string controls = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(new { frame.Memory.GeometryRegisters, frame.Memory.ScriptedColorRegisters }));
        var actual = anchored ? NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(frame, Address(frame, bank, 0), 2, Resources()) : NativeBattleElectricDischarge126Ribbons.DrawProjected(frame, Address(frame, bank, 0), 2, 0, Resources());
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.Equal(bucket, packet.Primitive!.Bucket);
        Assert.Equal(52, actual.Batch.AllocationBytes);
        Assert.Empty(packet.Primitive.VertexPayload4);
        Assert.Equal(leftY, BinaryPrimitives.ReadInt16LittleEndian(packet.Data.AsSpan().Slice(10)));
        Assert.Equal(rightY, BinaryPrimitives.ReadInt16LittleEndian(packet.Data.AsSpan().Slice(22)));
        Assert.Equal(0x3D14C008u, Word(packet, 12));
        Assert.Equal(0x00B7C038u, Word(packet, 24));
        Assert.Equal(0x3E808080u, Word(packet, 4));
        Assert.Equal(0x00808080u, Word(packet, 16));
        Assert.Equal(0x00404040u, Word(packet, 28));
        Assert.Equal(0x00404040u, Word(packet, 40));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(Offset(frame, bank, 1) + (anchored ? 6 : 2), 2));
        Assert.Equal(controls, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(new { actual.Frame.Memory.GeometryRegisters, actual.Frame.Memory.ScriptedColorRegisters })));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(frame.State, actual.Frame.State);
        for (int index = 0; index < 52; index++)
        {
            Assert.Equal(index is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, packet.WriteMask[index]);
        }
    }

    [Fact]
    public void Draw_preserves_working_relative_Arena_offset_and_rejects_other_physical_bank()
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        frame = Point(frame, WorldPointsOffset, 0, 100, 200, 512, 32, false, 2048, Address(frame, WorldPointsOffset, 1));
        frame = Point(frame, WorldPointsOffset, 1, 116, 200, 512, 32, false, 2048, 0);
        var actual = NativeBattleElectricDischarge126Ribbons.DrawProjected(frame, Address(frame, WorldPointsOffset, 0), 2, 0, Resources());
        Assert.Equal((short)199, BinaryPrimitives.ReadInt16LittleEndian(actual.Frame.Memory.Arena!.Read(Offset(frame, WorldPointsOffset, 0) + 0x12, 2)));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(WorldPointsOffset + 0x12, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(frame, Address(frame, WorldPointsOffset, 0), 2, Resources()));
    }

    [Fact]
    public void Rejected_depth_leaves_packetcursor_and_unread_width_color_padding_unknown()
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        int first = Offset(frame, BonePointsOffset, 0);
        int second = Offset(frame, BonePointsOffset, 1);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(first + 0x14, BitConverter.GetBytes((short)-1)).Write(first + 0x24, BitConverter.GetBytes(Address(frame, BonePointsOffset, 1))).Write(second + 0x14, BitConverter.GetBytes((short)-1));
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        var actual = NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(frame, Address(frame, BonePointsOffset, 0), 2, Resources());
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(frame.State, actual.Frame.State);
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(first + 0x10, 4));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(second + 2, 2));
    }

    [Fact]
    public void Restored_chain_repeats_Arena_and_batch_without_copied_state_or_GTE()
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        frame = Point(frame, BonePointsOffset, 0, 100, 200, 512, 96, true, 2048, Address(frame, BonePointsOffset, 1));
        frame = Point(frame, BonePointsOffset, 1, 116, 200, 512, 96, true, 2048, 0);
        NativeBattleElectricDischarge126Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleElectricDischarge126Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        var expected = NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(frame, Address(frame, BonePointsOffset, 0), 2, Resources());
        var actual = NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(restored, Address(restored, BonePointsOffset, 0), 2, Resources());
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected.Frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual.Frame)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected.Batch)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual.Batch)));
    }

    private static uint Word(NativeBattleGfGeometryAttempt packet, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(offset));
    private static uint Address(NativeBattleElectricDischarge126Frame frame, int bank, int index) => frame.State.ReadDword(WorkingGlobal) + (uint)(bank + index * PointStride);
    private static int Offset(NativeBattleElectricDischarge126Frame frame, int bank, int index) => checked((int)(frame.State.ReadDword(WorkingGlobal) - ArenaAddress) + bank + index * PointStride);
    private static NativeBattleElectricDischarge126Resources Resources() => new(new(0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new("unused", []), Enumerable.Repeat((byte)16, 16384).ToImmutableArray(), [], [], [], 0, 0, 0);

    private static NativeBattleElectricDischarge126Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(126, 126, new(126, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x2C874) };
        return new(memory, effect, new(1), NativeBattleElectricDischarge126State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress + 0x400));
    }

    private static NativeBattleElectricDischarge126Frame Point(NativeBattleElectricDischarge126Frame frame, int bank, int index, short x, short y, short depth, short width, bool anchored, short intensity, uint next)
    {
        int at = Offset(frame, bank, index);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(at + (anchored ? 2 : 6), BitConverter.GetBytes(width)).Write(at + 0x10, BitConverter.GetBytes(x)).Write(at + 0x12, BitConverter.GetBytes(y)).Write(at + 0x14, BitConverter.GetBytes(depth)).Write(at + 0x20, BitConverter.GetBytes(intensity)).Write(at + 0x24, BitConverter.GetBytes(next));
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }
}
