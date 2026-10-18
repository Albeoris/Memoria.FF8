using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterHeaderNextCommandTests
{
    private const uint Manager = 0x2797690;
    private const uint Command = 0x2797628;
    private const uint Header = NativeBattleScriptedEffectMemory.Address;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(128, 127)]
    [InlineData(255, 255)]
    public void CompletionUsesUnsignedCountsDefersOperandAndLeavesDrawOrderAndSlotSuffix(byte index, byte count)
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleWaterExecutionFrame frame = InitialFrame();
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, new byte[] { index, count }).Write(0x2797554, new byte[] { 0x80, 0xFF }), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        frame = SlotDword(frame, 0xC, 0xA5A5A5A5);
        NativeBattleWaterHeaderNextResult result = Execute(frame, fixture.Inputs.Resource1, (_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected reset header lookup."), (_, _, _, _) => throw new Xunit.Sdk.XunitException("Unexpected reset capture."));
        Assert.Null(result.SlotBank);
        Assert.Null(result.CurrentHeaderIndex);
        Assert.Equal(new byte[12], result.Frame.Arena.Read(SlotOffset(frame), 12));
        Assert.Equal(0xA5A5A5A5u, ReadSlot(result.Frame, 0xC));
        Assert.All(result.Frame.Core.Read(0x2797454, 128), value => Assert.Equal((byte)0xFF, value));
        Assert.Equal(frame.Core.Read(0x2797554, 128), result.Frame.Core.Read(0x2797554, 128));
        Assert.Equal(0u, Core(result.Frame, Command + 8));
        Assert.Equal((ushort)0, Word(result.Frame, Header + 0x18));
        Assert.Equal((ushort)4, Word(result.Frame, Header + 0x1A));
        Assert.Equal((ushort)0xFFFF, result.Frame.ReadTemporaryWord(0x13E));
        Assert.Equal(0u, Core(result.Frame, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Frame.Arena.Read(0x1002, 2));
    }

    [Fact]
    public void NextHeaderResetsOnlySharedBlockRebuildsCachedEightSlotsAndCapturesCurrentHeader()
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleWaterExecutionFrame frame = InitialFrame();
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, new byte[] { 1, 200 }), Arena = PutDword(frame.Arena, 0x201C, 0x100).MarkUnknown(0x2204, 0xFC) };
        frame = CoreDword(frame, Manager + 0x70, 0x12345678);
        frame = CoreDword(frame, Manager + 0x78, 0x23456789);
        frame = CoreDword(frame, Manager + 0x80, 0x3456789A);
        frame = CoreDword(frame, Manager + 0x94, 0x456789AB);
        frame = WaterSchedulerFixture.CoreWord(frame, Header + 0x1C, 0xABCD);
        frame = frame.WriteTemporaryByte(0x145, 0xFF).WriteTemporaryByte(0x144, 17).WriteTemporaryWord(0x13E, 0x1234);
        uint savedSlot = Core(frame, 0x27973E8);
        NativeBattleWaterInitializationResource resource = fixture.Inputs.Resource1 with { Length = 0x10058 };
        int captures = 0;
        NativeBattleWaterHeaderNextResult result = Execute(frame, resource, (core, arena, parameters) =>
        {
            Assert.Equal(fixture.Inputs.ActionParametersAddress, parameters);
            Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(core.Read(Manager + 0x74, 4)));
            return fixture.Inputs.HeaderBaseAddress;
        }, (core, arena, address, relative) =>
        {
            captures++;
            Assert.Equal((byte)2, relative);
            Assert.Equal(fixture.Inputs.HeaderBaseAddress + 2 * 0x14u, address);
            Assert.Equal(Address(0x2100), BinaryPrimitives.ReadUInt32LittleEndian(core.Read(Manager + 0x90, 4)));
            Assert.Equal(Address(0x2100), BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973E8, 4)));
            Assert.Equal(new byte[12], core.Read(Header + 0x168, 12));
            Assert.All(core.Read(0x2797204, 128), value => Assert.Equal((byte)0, value));
            return Capture(fixture.Inputs, 4, [2, 2, 3]);
        });
        Assert.Equal(1, captures);
        Assert.NotNull(result.SlotBank);
        Assert.Equal(8, result.SlotBank.Capacity);
        Assert.Equal(0x2100, result.SlotBank.SlotBaseOffset);
        Assert.Equal(4, result.CurrentHeaderIndex);
        Assert.Equal(0x1010u, Core(result.Frame, 0x2797450));
        Assert.Equal(savedSlot, Core(result.Frame, 0x27973E8));
        Assert.Equal(0x456789ABu, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x2100, 4)));
        Assert.Equal((byte)2, result.Frame.Arena.Read(0x211B, 1)[0]);
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Arena.Read(0x219A, 2)));
        Assert.Equal((ushort)0xABCD, Word(result.Frame, Header + 0x1C));
        Assert.Equal((ushort)8, Word(result.Frame, Header + 0x38));
        Assert.Equal((byte)2, result.Frame.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(0x23456789u, Core(result.Frame, Manager + 0x78));
        Assert.Equal(0x3456789Au, Core(result.Frame, Manager + 0x80));
        Assert.Equal(frame.Core.Read(0x2797968, 0x80), result.Frame.Core.Read(0x2797968, 0x80));
        Assert.Equal((byte)0, result.Frame.ReadTemporaryByte(0x145));
        Assert.Equal((byte)0, result.Frame.ReadTemporaryByte(0x144));
        Assert.Equal((ushort)0, result.Frame.ReadTemporaryWord(0x13E));
        Assert.Throws<NotSupportedException>(() => result.Frame.Arena.Read(0x2204, 1));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void OperandIsReadFromCurrentArenaOnlyAfterResetAndCurrentSlotRestoration()
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleWaterExecutionFrame frame = InitialFrame();
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, new byte[] { 0, 1 }) };
        uint slot = Core(frame, 0x27973E8);
        frame = CoreDword(frame, 0x2797450, slot);
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame) + 2, new byte[] { 0xA5, 0xA5 }) };
        frame = CoreDword(frame, Manager + 0x94, 0xFFFF5678);
        NativeBattleWaterHeaderNextResult result = NativeBattleWaterHeaderNextCommand.Execute(frame, new(SlotOffset(frame), 0x100, slot), fixture.Inputs.Resource1, (_, _, _) => fixture.Inputs.HeaderBaseAddress, (_, _, _, _) => Capture(fixture.Inputs, 3, [1]));
        Assert.Equal(unchecked(slot - 1), Core(result.Frame, 0x2797450));
        Assert.Equal(slot, Core(result.Frame, 0x27973E8));
        Assert.Equal(0xFFFF5678u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(SlotOffset(frame), 4)));
    }

    [Fact]
    public void IndependentJsonNextRepeatsResetCaptureAndDoesNotRepeatFullEntryMetadataWrites()
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleWaterExecutionFrame frame = InitialFrame();
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, new byte[] { 0, 2 }).MarkUnknown(Manager + 0x98, 0x1C).MarkUnknown(0x2798BF8, 32) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterHeaderNextResult a = Execute(frame, fixture.Inputs.Resource1, (_, _, _) => fixture.Inputs.HeaderBaseAddress, (_, _, _, _) => Capture(fixture.Inputs, 1, [6]));
        NativeBattleWaterHeaderNextResult b = Execute(restored, fixture.Inputs.Resource1, (_, _, _) => fixture.Inputs.HeaderBaseAddress, (_, _, _, _) => Capture(fixture.Inputs, 1, [6]));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Throws<NotSupportedException>(() => a.Frame.Core.Read(Manager + 0x98, 1));
        Assert.Throws<NotSupportedException>(() => a.Frame.Core.Read(0x2798BF8, 1));
    }

    [Fact]
    public void UnsignedHeaderComparisonTakesResetFor127Below128AndCompletionFor128At128()
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleWaterExecutionFrame frame = InitialFrame();
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, new byte[] { 127, 128 }) };
        NativeBattleWaterHeaderNextResult result = Execute(frame, fixture.Inputs.Resource1, (_, _, _) => fixture.Inputs.HeaderBaseAddress, (_, _, address, relative) =>
        {
            Assert.Equal((byte)128, relative);
            Assert.Equal(fixture.Inputs.HeaderBaseAddress + 128 * 0x14u, address);
            return Capture(fixture.Inputs, 0, []);
        });
        Assert.NotNull(result.SlotBank);
        Assert.Equal((byte)128, result.Frame.Core.Read(Manager + 0xD0, 1)[0]);
    }

    private static NativeBattleWaterResetCaptureInputs Capture(NativeBattleWaterInitializationInputs inputs, int index, ImmutableArray<byte> contacts) => new(inputs.Caster, index, inputs.FirstHeaderFirstActor, contacts, checked((byte)contacts.Length), inputs.Graphics);
    private static NativeBattleWaterHeaderNextResult Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterInitializationResource resource, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, uint> readHeaderBase, Func<NativeBattleScriptedEffectMemory, NativeBattleEffectArena, uint, byte, NativeBattleWaterResetCaptureInputs> readCapture) => NativeBattleWaterHeaderNextCommand.Execute(frame, new(0x1000, 4, 0x1000), resource, readHeaderBase, readCapture);
    private static NativeBattleWaterExecutionFrame InitialFrame()
    {
        var fixture = NativeBattleWaterInitializationTests.Fixture();
        NativeBattleWaterInitializationResult initialized = NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), fixture.Arena, fixture.Profile, fixture.Inputs);
        NativeBattleWaterExecutionFrame frame = new(initialized.Core, initialized.Arena, initialized.TemporaryFrame, new(37));
        frame = frame.WriteTemporaryWord(0x14A, 1);
        frame = CoreDword(frame, 0x2797450, 0x1000);
        return frame with { Arena = frame.Arena.Write(0x1002, new byte[] { 0x10, 0 }) };
    }
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Core(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));
    private static int SlotOffset(NativeBattleWaterExecutionFrame frame) => checked((int)(Core(frame, 0x27973E8) - NativeBattleEffectNodeMemory.ArenaAddress));
    private static uint ReadSlot(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(SlotOffset(frame) + offset, 4));
    private static NativeBattleWaterExecutionFrame SlotDword(NativeBattleWaterExecutionFrame frame, int offset, uint value) => frame with { Arena = PutDword(frame.Arena, SlotOffset(frame) + offset, value) };
    private static NativeBattleWaterExecutionFrame CoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
