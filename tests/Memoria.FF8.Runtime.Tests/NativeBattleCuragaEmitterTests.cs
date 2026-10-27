using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaEmitterTests
{
    private const int First = 0xC000;
    private const int Parent = First + 0x25F8;

    [Fact]
    public void Target_anchor_selection_preserves_fourth_DWORD_and_matrix_padding_while_offset_Q16_values_truncate_toward_zero()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(0, 0, false)]);
        NativeBattleCuragaFrame frame = Source(fixture.Frame, source =>
        {
            source[0x12] = 255;
            source[0x25] = 1;
            BinaryPrimitives.WriteInt32LittleEndian(source, 98304);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), -98304);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 180224);
        });
        frame = Task(frame, fixture.Task, task => task.WriteDword(0x34 + 0xE4, 100u << 16).WriteDword(0x34 + 0xE8, 200u << 16).WriteDword(0x34 + 0xEC, 300u << 16).WriteDword(0x34 + 0xF0, 0xDEADBEEF));
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 0, 1).Frame;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(First + 0x2C + 18, [0xCD, 0xAB]) } };
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.InitializeEmitter882990(frame, fixture.Task, First, _ => throw new InvalidOperationException());
        Assert.Equal(new uint[] { 101u << 16, 199u << 16, 302u << 16, 0xDEADBEEF }, Dwords(result, First + 0x4C, 4));
        Assert.Equal(new byte[] { 0xCD, 0xAB }, result.Memory.Arena!.Read(First + 0x2C + 18, 2));
        Assert.Equal(new byte[] { 255, 255 }, result.Memory.Arena.Read(First + 0x64, 2));
    }

    [Fact]
    public void Actor_anchor_and_orientation_four_use_actual_model_WORD14()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(1, 0, false)]);
        NativeBattleCuragaFrame frame = Source(fixture.Frame, source =>
        {
            source[0x1C] = 4;
            source[0x25] = 2;
            BinaryPrimitives.WriteInt32LittleEndian(source, 65536);
        });
        frame = Task(frame, fixture.Task, task => task.WriteWord(0x34 + 0x1E, 2).WriteDword(0x34 + 0x1F4, 100u << 16).WriteDword(0x34 + 0x1F8, 200u << 16).WriteDword(0x34 + 0x1FC, 300u << 16));
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 0, 0).Frame;
        frame = NativeBattleCuragaNodeKinematics.InitializeEmitter882990(frame, fixture.Task, First, index =>
        {
            Assert.Equal(2, index);
            return new(index, 3) { Rotation14 = 1024 };
        });
        Assert.Equal(new uint[] { 100u << 16, 200u << 16, 299u << 16 }, Dwords(frame, First + 0x4C, 3));
    }

    [Fact]
    public void Parent_centroid_uses_wrapped_signed_sums_and_truncates_each_average_without_writing_the_fourth_DWORD()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(3, 0, false)]);
        NativeBattleCuragaFrame frame = Source(fixture.Frame, source => source[0x35] = 1);
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0x20DFAB8 + Parent, 0, 0).Frame;
        frame = Dwords(frame, Parent + 0xDC, [0x7FFFFFFF, unchecked((uint)-3), 7, 9]);
        frame = Dwords(frame, Parent + 0x11C, [1, 0, 4, 10]);
        frame = Dwords(frame, First + 0x58, [0x55555555]);
        frame = NativeBattleCuragaNodeKinematics.FollowParent882150(frame, fixture.Task, First);
        Assert.Equal(new uint[] { unchecked((uint)-1073741824), unchecked((uint)-1), 5, 0x55555555 }, Dwords(frame, First + 0x4C, 4));
    }

    [Fact]
    public void Parent_orientation_copies_actual_matrix_padding_unknownness_and_reapplies_only_the_selected_local_offset()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(3, 0, false)]);
        NativeBattleCuragaFrame frame = Source(fixture.Frame, source =>
        {
            source[0x1C] = 3;
            BinaryPrimitives.WriteInt32LittleEndian(source, 65536);
        });
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0x20DFAB8 + Parent, 0, 0).Frame;
        frame = Dwords(frame, Parent + 0xDC, [10u << 16, 20u << 16, 30u << 16, 123]);
        byte[] matrix = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(matrix, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(16), 4096);
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(20), 100);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Parent + 0x8C, matrix).MarkUnknown(Parent + 0x8C + 18, 2) } };
        frame = NativeBattleCuragaNodeKinematics.FollowParent882150(frame, fixture.Task, First);
        Assert.Equal(new uint[] { 11u << 16, 20u << 16, 30u << 16, 123 }, Dwords(frame, First + 0x4C, 4));
        Assert.Equal(100u, Dwords(frame, First + 0x2C + 20, 1)[0]);
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(First + 0x2C + 18, 2));
    }

    internal static NativeBattleCuragaFrame Source(NativeBattleCuragaFrame frame, Action<byte[]> update)
    {
        byte[] bytes = NativeBattleStorageSnapshots.Capture(frame.Memory.CuragaExecutable!.Data!).Data.ToArray();
        byte[] source = bytes.AsSpan(0x104, 0x158).ToArray();
        update(source);
        source.CopyTo(bytes, 0x104);
        return frame with { Memory = frame.Memory with { CuragaExecutable = new(1, bytes.ToImmutableArray()) } };
    }
    private static NativeBattleCuragaFrame Task(NativeBattleCuragaFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, update(hierarchy.Read(address))).Pools } };
    }
    internal static NativeBattleCuragaFrame Dwords(NativeBattleCuragaFrame frame, int offset, uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, bytes) } };
    }
    internal static uint[] Dwords(NativeBattleCuragaFrame frame, int offset, int count)
    {
        byte[] bytes = frame.Memory.Arena!.Read(offset, count * 4);
        return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToArray();
    }
}
