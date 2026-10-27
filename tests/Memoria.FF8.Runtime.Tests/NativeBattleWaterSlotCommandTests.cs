using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterSlotCommandTests
{
    private const int FileOffset = 0x400;
    private const int SlotsOffset = FileOffset + 0x58;
    private static readonly NativeBattleWaterSlotBank Bank = new(FileOffset, 0x858, 8, SlotsOffset);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Type_five_copies_only_cached_position_words_from_the_live_update_block_and_can_reference_itself(bool self)
    {
        int selected = self ? 0 : 1;
        NativeBattleEffectArena arena = Create().Write(Address(0, 0x18), [5]).Write(0x2000, [5, 0]);
        arena = arena.MarkUnknown(Address(0, 0x5C), 12).MarkUnknown(Address(0, 0x9A), 2);
        arena = Dword(arena, 0, 0xA0, unchecked((int)(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000)));
        arena = Word(arena, selected, 0x12, 5);
        ushort[] words = [0x8000, 0xFFFF, 0x7FFF];
        for (int lane = 0; lane < 3; lane++)
        {
            arena = Word(arena, selected, 0x94 + lane * 2, words[lane]);
        }
        NativeBattleEffectArena result = NativeBattleWaterSlots.UpdateSelected(arena, Bank, 0, Order((byte)selected));
        Assert.Equal(words, Enumerable.Range(0, 3).Select(lane => ReadWord(result, 0, 0x94 + lane * 2)));
        Assert.Throws<NotSupportedException>(() => result.Read(Address(0, 0x5C), 12));
        Assert.Throws<NotSupportedException>(() => result.Read(Address(0, 0x9A), 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleWaterSlots.UpdateSelected(NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!, Bank, 0, Order((byte)selected))));
        NativeBattleEffectArena missingBlock = Dword(arena, 0, 0xA0, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterSlots.UpdateSelected(missingBlock, Bank, 0, Order((byte)selected)));
    }

    [Fact]
    public void Type_seven_reads_the_live_update_block_and_wraps_signed_motion_with_reference_cached_WORDs()
    {
        NativeBattleEffectArena arena = Create().Write(Address(0, 0x18), [7]).Write(0x2000, [5, 0]);
        arena = Dword(arena, 0, 0xA0, unchecked((int)(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000)));
        arena = Word(arena, 1, 0x12, 5);
        int[] motion = [-65536, int.MinValue, int.MaxValue];
        ushort[] origin = [0, 32767, 1];
        for (int lane = 0; lane < 3; lane++)
        {
            arena = Dword(arena, 0, 0x5C + lane * 4, motion[lane]);
            arena = Word(arena, 1, 0x94 + lane * 2, origin[lane]);
        }
        NativeBattleEffectArena result = NativeBattleWaterSlots.UpdateSelected(arena, Bank, 0, Order(1));
        Assert.Equal(new ushort[] { 65535, 65535, 32768 }, Enumerable.Range(0, 3).Select(lane => ReadWord(result, 0, 0x94 + lane * 2)));
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleWaterSlots.UpdateSelected(restored, Bank, 0, Order(1))));
        NativeBattleEffectArena empty = Dword(Create().Write(Address(0, 0x18), [7]), 0, 0xA0, 0);
        Assert.Equal(NativeBattleTestSnapshots.Json(empty), NativeBattleTestSnapshots.Json(NativeBattleWaterSlots.UpdateSelected(empty, Bank, 0, [])));
    }

    [Fact]
    public void InitializationClearsOnlySuffixAndUsesActualTicksAndHeader()
    {
        NativeBattleEffectArena arena = Create().Write(SlotsOffset, Enumerable.Repeat((byte)0xA5, 0x100).ToArray()).MarkUnknown(SlotsOffset, 4).MarkUnknown(SlotsOffset + 0x100, 0x100);
        NativeBattleEffectArena result = NativeBattleWaterSlots.InitializeB65160(arena, Bank, 0, 0xFEDC, 0x8123, 0x9876);
        byte[] expected = new byte[0x100];
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0x18), 0x03000000);
        expected[0x92] = 0x20;
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0x9A), 0x9876);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xC8), 0xFEDC);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xCA), 0x8123);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0xCC), 0x00808080);
        Assert.Equal(expected.AsSpan(4).ToArray(), result.Read(SlotsOffset + 4, 0xFC));
        Assert.Throws<NotSupportedException>(() => result.Read(SlotsOffset, 4));
        Assert.Throws<NotSupportedException>(() => result.Read(SlotsOffset + 0x100, 1));
        arena = Create().Write(SlotsOffset, Enumerable.Repeat((byte)0xA5, 0x100).ToArray());
        result = NativeBattleWaterSlots.InitializeB65160(arena, Bank, 0, 1, 2, 3);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, result.Read(SlotsOffset, 4));
    }

    [Fact]
    public void LookupCoversEveryAliasFirstDuplicatePausedEntryAndRelativeActor()
    {
        NativeBattleEffectArena arena = Create();
        arena = Word(arena, 1, 0x14, 100);
        arena = Word(arena, 1, 0x16, 200);
        arena = Word(arena, 2, 0x12, 200);
        arena = Word(arena, 2, 0x14, 300);
        arena = Word(arena, 2, 0x16, 400);
        arena = Word(arena, 3, 0x12, 100);
        arena = Word(arena, 4, 0x12, 300);
        arena = Word(arena, 5, 0x12, 400);
        arena = Word(arena, 6, 0x12, 100);
        arena = Word(arena, 7, 0x12, 103);
        arena = arena.Write(Address(1, 0x1B), [3]);
        ImmutableArray<byte> order = Order(0x83, 6, 2, 4, 5, 7);
        for (int selector = 0xFFF0; selector <= 0xFFFF; selector++)
        {
            int expected = new[] { 3, 2, 4, 5 }[(~selector) & 3];
            Assert.Equal(expected, NativeBattleWaterSlots.LookupB65370(arena, Bank, 1, order, selector));
            Assert.Equal(expected, NativeBattleWaterSlots.LookupB65370(arena, Bank, 1, order, unchecked((short)selector)));
        }
        Assert.Equal(7, NativeBattleWaterSlots.LookupB65370(arena, Bank, 1, order, 0x4064));
        Assert.Equal(3, NativeBattleWaterSlots.LookupB65370(arena, Bank, 1, order, 100));
        Assert.Equal(0, NativeBattleWaterSlots.LookupB65370(arena, Bank, 1, order, 999));
        Assert.Equal(0, NativeBattleWaterSlots.LookupB65370(arena, Bank, 1, Order(3), 0xFFFD));
    }

    [Fact]
    public void LookupRejectsMissingOrderAndPhysicalOwnersWithoutReadingUnconsumedSlotBytes()
    {
        NativeBattleEffectArena arena = Word(Create(), 1, 0x12, 44).MarkUnknown(Address(1, 0x30), 0xD0);
        Assert.Equal(1, NativeBattleWaterSlots.LookupB65370(arena, Bank, 0, Order(1), 44));
        Assert.Equal(1, NativeBattleWaterSlots.LookupB65370(arena, Bank, 0, Enumerable.Repeat((byte)1, 128).ToImmutableArray(), 44));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterSlots.LookupB65370(arena, Bank, 0, Enumerable.Repeat((byte)1, 128).ToImmutableArray(), 45));
        Assert.Equal(1, NativeBattleWaterSlots.LookupB65370(arena, Bank, 0, Order(1, 8), 44));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterSlots.LookupB65370(arena, Bank, 0, Order(1, 8), 45));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterSlots.LookupB65370(arena, Bank, 0, Order(8), 44));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterSlots.LookupB65370(arena, Bank with { FileLength = 0x857 }, 0, Order(), 44));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterSlots.CachePositionAA17D0(arena, Bank, 8));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterSlots.CachePositionAA17D0(arena, Bank with { SlotBaseOffset = FileOffset - 1 }, 0));
        NativeBattleWaterSlots.CachePositionAA17D0(arena.MarkUnknown(FileOffset + 0x1C, 4), Bank, 0);
    }

    [Fact]
    public void SixLanePhysicsWrapsSignedAccelerationAndPreservesOriginalFlags()
    {
        NativeBattleEffectArena arena = Create().Write(Address(0, 0x1A), [0xE9]);
        int[] positions = [int.MaxValue, int.MinValue, -1, 65536, -65536, 1];
        int[] velocities = [1, -2, 65536, -1, int.MaxValue, 7];
        short[] accelerations = [1, -1, 2, -2, short.MaxValue, short.MinValue];
        int[] expectedVelocities = [4097, -4098, 73728, -8193, -2013270017, -134217721];
        for (int lane = 0; lane < 6; lane++)
        {
            arena = Dword(arena, 0, 0x50 + 4 * lane, positions[lane]);
            arena = Dword(arena, 0, 0x68 + 4 * lane, velocities[lane]);
            arena = Word(arena, 0, 0x80 + 2 * lane, unchecked((ushort)accelerations[lane]));
        }
        NativeBattleEffectArena result = NativeBattleWaterSlots.AdvancePhysicsA9BB90(arena, Bank, 0, Order());
        for (int lane = 0; lane < 6; lane++)
        {
            Assert.Equal(expectedVelocities[lane], ReadDword(result, 0, 0x68 + 4 * lane));
            Assert.Equal(unchecked(positions[lane] + expectedVelocities[lane]), ReadDword(result, 0, 0x50 + 4 * lane));
            int cacheOffset = lane < 3 ? 0x8C + 2 * lane : 0x94 + 2 * (lane - 3);
            Assert.Equal(unchecked((ushort)(unchecked(positions[lane] + expectedVelocities[lane]) >> 16)), ReadWord(result, 0, cacheOffset));
        }
        Assert.Equal(0xE9, result.Read(Address(0, 0x1A), 1)[0]);
        result = NativeBattleWaterSlots.RefreshAccelerationAA27F0(result, Bank, 0);
        Assert.Equal(9, result.Read(Address(0, 0x1A), 1)[0]);
        result = result.Write(Address(0, 0x80), new byte[12]);
        Assert.Equal(0, NativeBattleWaterSlots.RefreshAccelerationAA27F0(result, Bank, 0).Read(Address(0, 0x1A), 1)[0]);
        result = Word(result, 0, 0x8A, 0xFFFF);
        Assert.Equal(8, NativeBattleWaterSlots.RefreshAccelerationAA27F0(result, Bank, 0).Read(Address(0, 0x1A), 1)[0]);
        result = Word(result, 0, 0x80, 1);
        Assert.Equal(9, NativeBattleWaterSlots.RefreshAccelerationAA27F0(result, Bank, 0).Read(Address(0, 0x1A), 1)[0]);
    }

    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(1024, 100, 0)]
    [InlineData(2048, 0, -100)]
    [InlineData(-1024, -100, 0)]
    public void TypeThreeUsesIdReferenceSignedAngleAndWordWrap(int angle, int x, int z)
    {
        NativeBattleEffectArena arena = TypeThree(Create(), 0, 1, angle, 100 << 16);
        arena = Word(arena, 1, 0x94, 32760);
        arena = Word(arena, 1, 0x96, unchecked((ushort)-20));
        arena = Word(arena, 1, 0x98, unchecked((ushort)-32760));
        arena = Dword(arena, 0, 0x60, -3 << 16);
        NativeBattleEffectArena result = NativeBattleWaterSlots.UpdateSelected(arena, Bank, 0, Order(1));
        Assert.Equal(unchecked((ushort)(32760 + x)), ReadWord(result, 0, 0x94));
        Assert.Equal(unchecked((ushort)-23), ReadWord(result, 0, 0x96));
        Assert.Equal(unchecked((ushort)(-32760 + z)), ReadWord(result, 0, 0x98));
        Assert.Equal(arena.Read(Address(0, 0x50), 24), result.Read(Address(0, 0x50), 24));
    }

    [Fact]
    public void TypeThreeKeepsLowDwordProductsAndCanReferenceItself()
    {
        NativeBattleEffectArena arena = TypeThree(Create(), 0, 0, 0, int.MaxValue);
        arena = Word(arena, 0, 0x94, 50);
        arena = Word(arena, 0, 0x96, 60);
        arena = Word(arena, 0, 0x98, 70);
        NativeBattleEffectArena result = NativeBattleWaterSlots.UpdateSelected(arena, Bank, 0, Order(0));
        Assert.Equal((ushort)50, ReadWord(result, 0, 0x94));
        Assert.Equal((ushort)60, ReadWord(result, 0, 0x96));
        Assert.Equal((ushort)32837, ReadWord(result, 0, 0x98));
    }

    [Fact]
    public void PostActionSelectsBeforeCacheWhilePhysicsConsumesPositionBeforeSelection()
    {
        NativeBattleEffectArena arena = Create().Write(Address(0, 0x18), [0x7F]).MarkUnknown(Address(0, 0x50), 4);
        NotSupportedException post = Assert.Throws<NotSupportedException>(() => NativeBattleWaterSlots.ApplyVectorPostAction(arena, Bank, 0, Order(), NativeBattleWaterVectorPostAction.RefreshPosition));
        Assert.Contains("selected type", post.Message);
        NotSupportedException physics = Assert.Throws<NotSupportedException>(() => NativeBattleWaterSlots.AdvancePhysicsA9BB90(arena, Bank, 0, Order()));
        Assert.Contains("unknown physical bytes", physics.Message);
        Assert.Same(arena, NativeBattleWaterSlots.ApplyVectorPostAction(arena, Bank, 0, Order(), NativeBattleWaterVectorPostAction.None));
    }

    [Fact]
    public void BankRemainsAnArenaAliasAcrossJsonAndRetainsCachedBaseDespiteChangedFileHeader()
    {
        NativeBattleEffectArena arena = Dword(Create(), 0, 0x50, -65536);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!;
        NativeBattleEffectArena first = NativeBattleWaterSlots.CachePositionAA17D0(arena, Bank, 0);
        NativeBattleEffectArena second = NativeBattleWaterSlots.CachePositionAA17D0(restored, Bank, 0);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
        Assert.Equal((ushort)0xFFFF, ReadWord(second, 0, 0x8C));
        second = Dword(second, 1, 0x50, 0x12340000);
        second = second.Write(FileOffset + 0x1C, new byte[] { 0x58, 1, 0, 0 });
        second = NativeBattleWaterSlots.CachePositionAA17D0(second, Bank, 0);
        Assert.Equal((ushort)0xFFFF, ReadWord(second, 0, 0x8C));
        second = second.MarkUnknown(FileOffset + 0x1C, 4);
        NativeBattleWaterSlotBank retained = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterSlotBank>(NativeBattleTestSnapshots.Json(Bank))!;
        second = NativeBattleWaterSlots.CachePositionAA17D0(second, retained, 0);
        Assert.Equal((ushort)0xFFFF, ReadWord(second, 0, 0x8C));
        NativeBattleWaterSlotBank shifted = retained with { Capacity = 7, SlotBaseOffset = SlotsOffset + 0x100 };
        second = NativeBattleWaterSlots.CachePositionAA17D0(second, shifted, 0);
        Assert.Equal((ushort)0x1234, ReadWord(second, 1, 0x8C));
    }

    private static NativeBattleEffectArena TypeThree(NativeBattleEffectArena arena, int index, int reference, int angle, int radius)
    {
        arena = arena.Write(Address(index, 0x18), [3]);
        arena = Word(arena, reference, 0x12, 777);
        arena = Word(arena, index, 0xB0, 777);
        arena = Dword(arena, index, 0x64, unchecked(angle << 16));
        return Dword(arena, index, 0x5C, radius);
    }

    private static NativeBattleEffectArena Create() => new NativeBattleEffectArena([]).Write(FileOffset + 0x1C, new byte[] { 0x58, 0, 0, 0 });
    private static int Address(int index, int offset) => SlotsOffset + index * 0x100 + offset;
    private static ImmutableArray<byte> Order(params byte[] indices) => indices.Concat(Enumerable.Repeat((byte)0xFF, 128 - indices.Length)).ToImmutableArray();
    private static ushort ReadWord(NativeBattleEffectArena arena, int index, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(Address(index, offset), 2));
    private static int ReadDword(NativeBattleEffectArena arena, int index, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(Address(index, offset), 4));

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int index, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(Address(index, offset), bytes);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int index, int offset, int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return arena.Write(Address(index, offset), bytes);
    }
}
