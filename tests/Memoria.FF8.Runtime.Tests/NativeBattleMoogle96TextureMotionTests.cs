using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMoogle96TextureMotionTests
{
    [Fact]
    public void ByteDirectoryAndReverseAtlasUseActualAllocatedOrdinalAndCopyQueue()
    {
        NativeBattleTextureCopy copy = NativeBattleMoogle96TextureMotion.Plan739960(Table(), 3, 1, 1)!;
        Assert.Equal(new NativeBattleTextureCopy(847, 400, 7, 8, 862, 424), copy);
        NativeBattleTextureCopy high = NativeBattleMoogle96TextureMotion.Plan739960(Table(), (1 << 10) | (1 << 11), 1, 1)!;
        Assert.Equal((short)527, high.SourceX);
        Assert.Equal((short)400, high.SourceY);
        NativeBattleEffectTextureState queue = NativeBattleEffectTextureState.FromColdProcess().EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        Assert.Equal(1, queue.Count);
        Assert.Equal((byte)3, queue.Slots[0]!.Kind);
        Assert.Equal(new NativeBattleEffectVramCopy(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY), queue.Slots[0]!.Copy);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96TextureMotion.Plan739960(Table(), 1, 1, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96TextureMotion.Plan739960(Table(), 3, ushort.MaxValue, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96TextureMotion.Plan739960(Table(), 3, 0, ushort.MaxValue));
    }

    [Fact]
    public void SignedCountdownReadsOnZeroOneAndNegativeButOtherwiseOnlyDecrements()
    {
        NativeBattleMoogle96TextureProgram program = Program((1, 2), (2, 1), (0xFF, 0));
        NativeBattleMoogle96TextureStep first = NativeBattleMoogle96TextureMotion.Advance739890(Task(), program, Table());
        Assert.Single(first.Copies);
        Assert.Equal((ushort)2, first.Task.ReadWord(0x132));
        NativeBattleMoogle96TextureStep wait = NativeBattleMoogle96TextureMotion.Advance739890(first.Task, program, Table());
        Assert.Empty(wait.Copies);
        Assert.Equal((ushort)0, wait.Task.ReadWord(0x12C));
        Assert.Equal((ushort)1, wait.Task.ReadWord(0x132));
        NativeBattleMoogle96TextureStep next = NativeBattleMoogle96TextureMotion.Advance739890(wait.Task, program, Table());
        Assert.Single(next.Copies);
        Assert.Equal((ushort)1, next.Task.ReadWord(0x12C));
        NativeBattleMoogle96TextureStep stop = NativeBattleMoogle96TextureMotion.Advance739890(next.Task, program, Table());
        Assert.Empty(stop.Copies);
        Assert.Equal(0u, stop.Task.ReadDword(0x110));
        NativeBattleMoogle96TextureStep negative = NativeBattleMoogle96TextureMotion.Advance739890(Task().WriteWord(0x132, 0xFFFF), program, Table());
        Assert.Equal((ushort)1, negative.Task.ReadWord(0x12C));
        Assert.Equal((ushort)2, negative.Task.ReadWord(0x130));
    }

    [Fact]
    public void LoopSentinelLoadsFirstPairOnceAndRestoresWithoutRecursiveStopHandling()
    {
        NativeBattleMoogle96TextureProgram program = Program((0, 1), (0xFE, 0));
        NativeBattleMoogle96TextureStep first = NativeBattleMoogle96TextureMotion.Advance739890(Task(), program, Table());
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.Task))!;
        NativeBattleMoogle96TextureStep loop = NativeBattleMoogle96TextureMotion.Advance739890(first.Task, program, Table());
        Assert.Equal(JsonSerializer.Serialize(loop), JsonSerializer.Serialize(NativeBattleMoogle96TextureMotion.Advance739890(restored, program, Table())));
        Assert.Equal((ushort)0, loop.Task.ReadWord(0x12C));
        Assert.Single(loop.Copies);
        NativeBattleMoogle96TextureProgram nonRecursive = Program((0xFF, 0), (0xFE, 0));
        NativeBattleMoogle96TextureStep loaded = NativeBattleMoogle96TextureMotion.Advance739890(Task().WriteWord(0x12C, 1).WriteDword(0x10C, 0), nonRecursive, null);
        Assert.Equal(0x3000u, loaded.Task.ReadDword(0x110));
        Assert.Equal((ushort)0xFF, loaded.Task.ReadWord(0x130));
        Assert.Empty(loaded.Copies);
        Assert.Equal(0u, NativeBattleMoogle96TextureMotion.Advance739890(loaded.Task, nonRecursive, null).Task.ReadDword(0x110));
    }

    [Fact]
    public void IndexWrapNullOwnersAndBoundedReadsDoNotInventProgramOrAtlasData()
    {
        NativeBattleMoogle96TextureProgram program = Program((0, 1));
        NativeBattleMoogle96TextureStep wrapped = NativeBattleMoogle96TextureMotion.Advance739890(Task().WriteWord(0x12C, ushort.MaxValue).WriteWord(0x132, 1), program, Table());
        Assert.Equal((ushort)0, wrapped.Task.ReadWord(0x12C));
        Assert.Single(wrapped.Copies);
        Assert.Empty(NativeBattleMoogle96TextureMotion.Advance739890(Task().WriteDword(0x110, 0), null, null).Copies);
        Assert.Empty(NativeBattleMoogle96TextureMotion.Advance739890(Task().WriteDword(0x10C, 0), program, null).Copies);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96TextureMotion.Advance739890(Task(), null, Table()));
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96TextureMotion.Advance739890(Task(), program, null));
        Assert.Throws<InvalidDataException>(() => NativeBattleMoogle96TextureMotion.Advance739890(Task().WriteWord(0x12C, ushort.MaxValue), program, Table()));
        NativeBattleMoogle96TextureTable absent = Table() with { Data = new byte[24].ToImmutableArray() };
        Assert.Null(NativeBattleMoogle96TextureMotion.Plan739960(absent, 3, 0, 0));
    }

    private static NativeBattleModulePoolTask Task()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x140, 1)]);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(0, 1, 0x140);
        return append.Hierarchy.Read(append.Address).WriteDword(0xB4, 0x20DC).WriteDword(0x10C, 0x4000).WriteWord(0xDE, 3).WriteDword(0x110, 0x3000);
    }
    private static NativeBattleMoogle96TextureProgram Program(params (ushort Variant, ushort Duration)[] pairs)
    {
        byte[] bytes = new byte[pairs.Length * 4];
        for (int index = 0; index < pairs.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 4), pairs[index].Variant);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 4 + 2), pairs[index].Duration);
        }
        return new(0x3000, bytes.ToImmutableArray());
    }
    private static NativeBattleMoogle96TextureTable Table() => new(0x4000, ImmutableArray.Create<byte>(2, 13, 1, 12, 34, 5, 6, 7, 8, 9, 10, 11, 12, 1, 30, 40, 7, 8, 13, 14, 15, 16, 0, 0));
}
