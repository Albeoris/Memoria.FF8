using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterWorkCommandTests
{
    private const uint Manager = 0x2797690;
    private const uint Pc = 0xFFFFFFFC;
    private const int ResourceOffset = 0x10000;
    private const uint Resource = NativeBattleEffectNodeMemory.ArenaAddress + ResourceOffset;

    [Theory]
    [InlineData(0x8021)]
    [InlineData(0x9021)]
    public void ModesZeroAndOneReadLiveHeaderButNotSizeOrParity(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0x8000, 1);
        frame = frame with { Arena = PutDword(frame.Arena, ResourceOffset + 0x1C, 0x158).MarkUnknown(0x1006, 2) };
        bool[] known = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame).Known.ToArray();
        known[0x141] = false;
        frame = frame with { TemporaryFrame = frame.TemporaryFrame.SetKnownness(0, known) };
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x70, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, 0x5678);
        NativeBattleWaterExecutionFrame result = Run(frame, 6);
        uint expected = Resource + 0x18158;
        if (instruction == 0x8021)
        {
            Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x70));
            Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        }
        else
        {
            Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xB8));
            Assert.Equal(0x1234u, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x70));
            Assert.Equal(0x5678u, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        }
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x90));
        Assert.False(NativeBattleStorageSnapshots.Capture(result.TemporaryFrame).Known[0x141]);
        Assert.Equal(4u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(-1, 1, -128, -128)]
    [InlineData(-1, -1, -128, 128)]
    [InlineData(short.MinValue, -128, -4194304, 536870912)]
    [InlineData(short.MaxValue, 127, 4194176, 532660352)]
    [InlineData(256, 0, 32768, 0)]
    public void PrivateBaseUsesSignedSizeAndSignedActualParity(short size, sbyte parity, int expectedSize, int delta)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xA021, 0, 1, unchecked((ushort)size)).WriteTemporaryByte(0x141, unchecked((byte)parity));
        NativeBattleWaterExecutionFrame result = Run(frame);
        uint start = Resource + 0x10058;
        Assert.Equal(start, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xD4));
        Assert.Equal(unchecked((uint)expectedSize), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xDC));
        Assert.Equal(unchecked(start + (uint)delta), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xD8));
        Assert.Equal(unchecked((byte)parity), result.ReadTemporaryByte(0x141));
    }

    [Fact]
    public void SelectedBasesAreActualAliasesAndNeverToggleParity()
    {
        NativeBattleWaterExecutionFrame frame = Run(Fixture(0x8021, 0x8000, 1));
        frame = Reenter(frame, 0x9021, 0x8000, 1, 0);
        frame = Run(frame);
        frame = Reenter(frame, 0xA021, 0, 1, 0x100).WriteTemporaryByte(0x141, 1);
        frame = Run(frame);
        uint expected = Resource + 0x18058;
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0x70));
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0x74));
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0xB8));
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(frame, Manager + 0xD8));
        Assert.Equal((byte)1, frame.ReadTemporaryByte(0x141));
        frame = Reenter(frame, 0xA021, 0, 1, 0x100);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame expected = Run(frame);
        NativeBattleWaterExecutionFrame actual = Run(restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal((byte)1, actual.ReadTemporaryByte(0x141));
    }

    [Theory]
    [InlineData(0xFFFFFFFFu, 0x57u)]
    [InlineData(0x7FFFFFFFu, 0x80000057u)]
    public void RawPointerArithmeticWrapsWithoutDereferencingItsNewDestination(uint immediate, uint delta)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8021, (ushort)immediate, (ushort)(immediate >> 16));
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(unchecked(Resource + delta), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        Assert.Same(frame.Arena, result.Arena);
    }

    [Fact]
    public void GrowthClearsOnlyNewHeadsWithUnknownPayloadAndCachedBaseRetained()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x7D, 96);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 8);
        frame = frame with { Arena = frame.Arena.Write(0x2000, Enumerable.Repeat((byte)0xA5, 0x6001).ToArray()).MarkUnknown(0x2800, 0x5800).MarkUnknown(ResourceOffset + 0x1C, 4) };
        NativeBattleWaterExecutionFrame result = Run(frame, 4);
        Assert.Equal((ushort)96, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x38, 2)));
        Assert.All(result.Arena.Read(0x2000, 0x800), value => Assert.Equal(0xA5, value));
        for (int index = 8; index < 96; index++)
        {
            int offset = 0x2000 + 0x100 * index;
            Assert.Equal(new byte[4], result.Arena.Read(offset, 4));
            Assert.Throws<NotSupportedException>(() => result.Arena.Read(offset + 4, 1));
        }
        Assert.Equal((byte)0xA5, result.Arena.Read(0x8000, 1)[0]);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x90));
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(ResourceOffset + 0x1C, 4));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(8)]
    public void NoGrowthDoesNotConsumeCachedBankOrChangeCapacity(short requested)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x7D, unchecked((ushort)requested));
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 8);
        frame = frame with { Core = frame.Core.MarkUnknown(0x27973EC, 4).MarkUnknown(Manager + 0x90, 4), Arena = frame.Arena.MarkUnknown(0x2000, 0x100) };
        NativeBattleWaterExecutionFrame result = Run(frame, 4);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x38, 2)));
    }

    [Fact]
    public void BaseCommandConsumesHighImmediateWordBeforeLowWord()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8021);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 4) };
        Assert.Throws<NotSupportedException>(() => Run(frame));
        // The high word is outside this shorter program; reading the unknown low word first would produce a different exception.
        Assert.Throws<InvalidDataException>(() => Run(frame, 4));
        frame.Arena.Write(0x1004, new byte[2]);
        Assert.Throws<NotSupportedException>(() => Run(frame));
    }

    [Fact]
    public void ActualAccessBoundariesAndUnsupportedModesFailWithoutCappingOrHydration()
    {
        Assert.Throws<NotSupportedException>(() => Run(Fixture(0x0021)));
        Assert.Throws<InvalidDataException>(() => Run(Fixture(0xA021), 6));
        NativeBattleWaterExecutionFrame unknown = Fixture(0x8021);
        unknown = unknown with { Arena = unknown.Arena.MarkUnknown(ResourceOffset + 0x1C, 4) };
        Assert.Throws<NotSupportedException>(() => Run(unknown));
        NativeBattleWaterExecutionFrame end = Fixture(0x7D, 2);
        end = WaterSchedulerFixture.CoreWord(end, NativeBattleScriptedEffectMemory.Address + 0x38, 0);
        end = WaterSchedulerFixture.CoreDword(end, Manager + 0x90, NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length - 4);
        Assert.Throws<NotSupportedException>(() => Run(end));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(end.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x38, 2)));
    }

    [Theory]
    [InlineData(0x0021)]
    [InlineData(0x3021)]
    [InlineData(0x7021)]
    [InlineData(0xB021)]
    [InlineData(0xF021)]
    public void Arena_and_default_work_modes_use_the_selected_base_without_reading_size_or_parity(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0, 5);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1006, 2) };
        if ((instruction & 0x8000) == 0)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(0x2798A6C, 4), Arena = frame.Arena.MarkUnknown(ResourceOffset + 0x1C, 4) };
        }
        uint baseAddress = (instruction & 0x8000) != 0 ? Resource + 0x58 : NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterWorkCommands.Execute(frame, new(0x1000, 8, Pc), NativeBattleEffectNodeMemory.ArenaAddress);
        Assert.Equal(unchecked(baseAddress + 0x50000), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x70));
        Assert.Equal(unchecked(baseAddress + 0x50000), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        Assert.Equal(unchecked(Pc + 8), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0x1021, 0)]
    [InlineData(0x2021, -128)]
    [InlineData(0x2021, 0)]
    [InlineData(0x2021, 128)]
    public void Arena_base_modes_preserve_signed_private_size_and_live_aliases(ushort instruction, short size)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0, 4, unchecked((ushort)size)).WriteTemporaryByte(0x141, 0xFF);
        frame = frame with { Core = frame.Core.MarkUnknown(0x2798A6C, 4) };
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterWorkCommands.Execute(frame, new(0x1000, 8, Pc), NativeBattleEffectNodeMemory.ArenaAddress);
        uint target = NativeBattleEffectNodeMemory.ArenaAddress + 0x40000;
        if (instruction == 0x1021)
        {
            Assert.Equal(target, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xB8));
        }
        else
        {
            Assert.Equal(target, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xD4));
            Assert.Equal(unchecked((uint)(size << 7)), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xDC));
            Assert.Equal(unchecked(target - (uint)(size << 7)), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xD8));
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleWaterWorkCommands.Execute(restored, new(0x1000, 8, Pc), NativeBattleEffectNodeMemory.ArenaAddress)));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, ushort low = 0, ushort high = 0, ushort size = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, Resource);
        frame = frame with { Arena = PutDword(frame.Arena, ResourceOffset + 0x1C, 0x58) };
        return Reenter(frame, instruction, low, high, size);
    }

    private static NativeBattleWaterExecutionFrame Reenter(NativeBattleWaterExecutionFrame frame, ushort instruction, ushort low, ushort high, ushort size)
    {
        frame = frame.WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), low);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), high);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), size);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, int length = 8) => NativeBattleWaterWorkCommands.Execute(frame, new(0x1000, length, Pc));
}
