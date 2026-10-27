using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterExecutionFrameTests
{
    [Fact]
    public void Temporary_writes_match_scalar_memory_and_explicit_history_with_overlapping_input()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleWaterTemporaryFrame original = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame);
        byte[] expected = original.Bytes.ToArray();
        bool[] known = original.Known.ToArray();
        for (int index = 0; index < 64; index++)
        {
            int source = index * 5 % 350;
            int destination = (source + 3) % 350;
            NativeBattleWaterTemporaryFrame previous = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame);
            byte[] previousBytes = expected.ToArray();
            bool[] previousKnown = known.ToArray();
            byte[] input = expected.AsSpan(source, 24).ToArray();
            input.CopyTo(expected.AsSpan(destination));
            Array.Fill(known, true, destination, input.Length);
            Assert.Same(frame, frame.WriteTemporary(destination, input));
            NativeBattleWaterTemporaryFrame actual = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame);
            Assert.Equal(expected, actual.Bytes.ToArray());
            Assert.Equal(known, actual.Known.ToArray());
            Assert.Equal(previousBytes, previous.Bytes.ToArray());
            Assert.Equal(previousKnown, previous.Known.ToArray());
            Assert.Equal(original.Address, actual.Address);
            Assert.Equal(new byte[] { 1, 2, 3 }, frame.ModuleViewBackup.ToArray());
        }
        Assert.Equal(Enumerable.Range(0, 384).Select(value => unchecked((byte)value)), original.Bytes);
        Assert.All(original.Known, value => Assert.False(value));
        NativeBattleWaterTemporaryFrame frozen = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame);
        NativeBattleWaterTemporaryFrame decoded = JsonSerializer.Deserialize<NativeBattleWaterTemporaryFrame>(JsonSerializer.Serialize(frozen))!;
        NativeBattleWaterTemporaryFrame restored = NativeBattleStorageSnapshots.Restore(decoded);
        Assert.Equal(frozen.Bytes.ToArray(), NativeBattleStorageSnapshots.Capture(restored).Bytes.ToArray());
        restored.WriteUInt32(380, 0xFEDCBA98);
        Assert.Equal(0xFEDCBA98u, restored.ReadUInt32(380));
        Assert.False(frame.TemporaryFrame.IsKnown(380, 4));
    }

    [Fact]
    public void Known_identical_stores_reuse_owners_and_unknown_stores_assign_only_their_lanes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleWaterTemporaryFrame temporary = frame.TemporaryFrame;
        byte[] input = Enumerable.Range(100, 8).Select(value => unchecked((byte)value)).ToArray();
        Assert.Same(frame, frame.WriteTemporary(100, input));
        Assert.Same(temporary, frame.TemporaryFrame);
        Assert.True(temporary.IsKnown(100, 8));
        Assert.Same(frame, frame.WriteTemporary(100, input));
        Assert.Same(frame, frame.WriteTemporary(384, []));
        Assert.Same(frame, frame.WriteTemporaryDword(100, 0xFEDCBA98).WriteTemporaryWord(104, 0x8765).WriteTemporaryByte(106, 0xA5));
        Assert.Equal(0xFEDCBA98u, frame.ReadTemporaryDword(100));
        Assert.Equal((ushort)0x8765, frame.ReadTemporaryWord(104));
        Assert.Equal(0xA5, frame.ReadTemporaryByte(106));
        Assert.False(temporary.IsKnown(99, 1));
        Assert.False(temporary.IsKnown(108, 1));
        byte[] independent = frame.ReadTemporary(100, 8);
        independent[0] = 0;
        Assert.Equal(0xFEDCBA98u, frame.ReadTemporaryDword(100));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryDword(98));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(385, 0)]
    [InlineData(384, 1)]
    [InlineData(383, 2)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(0, int.MaxValue)]
    public void Temporary_access_refuses_invalid_bounds_before_consuming_bytes(int offset, int length)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporary(offset, length));
        if (length >= 0 && length <= 2)
        {
            Assert.Throws<InvalidDataException>(() => frame.WriteTemporary(offset, new byte[length]));
        }
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporaryByte(384));
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporaryWord(383));
        Assert.Throws<InvalidDataException>(() => frame.ReadTemporaryDword(381));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryByte(0));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryWord(0));
        Assert.Throws<NotSupportedException>(() => frame.ReadTemporaryDword(0));
        Assert.Empty(frame.ReadTemporary(384, 0));
        Assert.Throws<InvalidDataException>(() => new NativeBattleWaterTemporaryFrame(frame.TemporaryFrame.Address, new byte[384].ToImmutableArray(), new bool[383].ToImmutableArray()));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void Native_address_validation_remains_at_binding_instead_of_scalar_access(uint address)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Address = address } };
        frame.WriteTemporaryDword(0, 0x12345678);
        Assert.Equal(0x12345678u, frame.ReadTemporaryDword(0));
        NativeBattleScriptedInitializationDirectories directories = new(new("initialization", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterFrameBinding.Bind(frame.Core, frame.TemporaryFrame, directories));
    }

    [Fact]
    public void Warm_scalar_stores_keep_one_owner_without_managed_allocations()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        for (int index = 0; index < 1000; index++)
        {
            Store(index);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            Store(index);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(999u, frame.ReadTemporaryDword(100));

        void Store(int value)
        {
            frame.WriteTemporaryDword(100, (uint)value);
            frame.WriteTemporaryWord(104, (ushort)value);
            frame.WriteTemporaryByte(106, (byte)value);
        }
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        return frame with { TemporaryFrame = new(frame.TemporaryFrame.Address, Enumerable.Range(0, 384).Select(value => unchecked((byte)value)).ToImmutableArray(), Enumerable.Repeat(false, 384).ToImmutableArray()), ModuleViewBackup = [1, 2, 3] };
    }
}
