using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect233OffscreenTests
{
    private const uint Start = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;

    [Fact]
    public void Disabled_capture_advances_six_without_consuming_unknown_operands_or_owners()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x6F);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x27975D8 + 0x30, 1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 10), Core = frame.Core.MarkUnknown(0x27973E8, 4).MarkUnknown(0x2797690 + 0x74, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicOffscreen.Execute(frame, new(0x1000, 6, 0x80001000), null!);
        Assert.Equal(0x80001006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1000, 1));
    }

    [Fact]
    public void Capture_uses_two_operands_fixed_size_current_pose_and_preserved_padding_after_restore()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x6F, 384, 256).WriteTemporaryWord(0xC2, 0xBEEF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-32768));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 17);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 90);
        frame = frame with
        {
            Arena = frame.Arena.MarkUnknown(0x1006, 4),
        };
        frame.TemporaryFrame.MarkUnknown(0xD4, 4);
        List<NativeBattleCinematicOffscreenStageRequest> draws = [];
        List<(uint Address, int Entries)> clears = [];
        List<(uint Ordering, uint Packet)> inserts = [];
        List<uint> executes = [];
        int debug = 0;
        ushort focal = 0;
        NativeBattleCinematicOffscreenServices services = Services(draws, clears, inserts, executes, () => debug++) with
        {
            SetFocalLength = (current, value) =>
            {
                focal = value;
                return current;
            },
            ProjectAnchor = _ => throw new InvalidOperationException("The selected capture uses its current Euler pose."),
            ReadCurrentEffect = _ => throw new InvalidOperationException("The selected capture does not query the other mode's effect-specific flag.")
        };
        NativeBattleWaterScriptProgram program = new(0x1000, 6, 0x80001000);
        NativeBattleWaterExecutionFrame original = NativeBattleStorageSnapshots.Capture(frame);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicOffscreen.Execute(frame, program, services);
        Assert.Equal(1, debug);
        Assert.Equal((ushort)256, focal);
        Assert.Equal(0x80001006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(Start + 0xEC, Dword(result, Start));
        Assert.Equal(0u, Dword(result, Start + 4));
        Assert.Equal(Start + 0x10EC, Dword(result, Start + 8));
        Assert.Equal(0x5BECu, Dword(result, Start + 0xC));
        Assert.Equal(0x5BECu, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xC4));
        Assert.Equal(Start + 0x5BEC, Dword(result, Start + 0x14));
        Assert.Equal(Start + 0x5BEC, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(32768u, result.ReadTemporaryDword(0xC4));
        Assert.Equal(unchecked((uint)-90), result.ReadTemporaryDword(0xC8));
        Assert.Equal(17u, result.ReadTemporaryDword(0xCC));
        Assert.Equal((ushort)0xBEEF, result.ReadTemporaryWord(0xC2));
        Assert.False(result.TemporaryFrame.IsKnown(0xD4, 1));
        Assert.Equal(0x01000100u, Dword(result, Start + 0x54));
        Assert.Equal((ushort)352, Word(result, Start + 0x58));
        Assert.Equal((ushort)256, Word(result, Start + 0x5A));
        Assert.Equal((byte)30, result.Arena.Read(Offset(Start + 0x6B), 1)[0]);
        NativeBattleCinematicOffscreenStageRequest draw = Assert.Single(draws);
        Assert.Equal(1, draw.Group);
        Assert.Equal(frame.TemporaryFrame.Address + 0xB0, draw.MatrixAddress);
        Assert.Equal((byte)6, draw.DepthShift);
        Assert.Equal(Start + 0x10EC, draw.OutputAddress);
        Assert.Equal((Start + 0xEC, 1024), Assert.Single(clears));
        Assert.Equal(Start + 0xEC + 0xFFC, Assert.Single(executes));
        Assert.Equal(new[] { (Start + 0xEC + 0x40, Start + 0xAC), (Start + 0xEC + 0xFC0, Start + 0x6C) }, inserts);
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original)))!);
        NativeBattleWaterExecutionFrame restoredResult = NativeBattleCinematicOffscreen.Execute(restored, program, services);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restoredResult)));
    }

    private static NativeBattleCinematicOffscreenServices Services(List<NativeBattleCinematicOffscreenStageRequest> draws, List<(uint Address, int Entries)> clears, List<(uint Ordering, uint Packet)> inserts, List<uint> executes, Action debug)
    {
        byte[] source = new byte[28];
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(4), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(6), 216);
        source[0x16] = 1;
        source[0x17] = 1;
        source[0x19] = 10;
        source[0x1A] = 20;
        source[0x1B] = 30;
        return new(_ => 1, (_, buffer) =>
        {
            Assert.Equal((byte)0, buffer);
            return source.ToImmutableArray();
        }, current =>
        {
            debug();
            return current;
        }, (current, _) => current, (_, selector) =>
        {
            Assert.Equal((ushort)0, selector);
            return 0x27979E8;
        }, current => current.WriteTemporaryWord(0xD0, unchecked((ushort)-10)).WriteTemporaryWord(0xD2, 20).WriteTemporaryDword(0xFC, 0x80020000), (current, address, entries) =>
        {
            clears.Add((address, entries));
            return current;
        }, (current, ordering, packet) =>
        {
            inserts.Add((ordering, packet));
            return current;
        }, (current, request) =>
        {
            draws.Add(request);
            return new(current, request.OutputAddress + 16);
        }, (current, address) =>
        {
            executes.Add(address);
            return current;
        }, _ => 205);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, params ushort[] operands)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973BC, 0x27975D8);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Start);
        byte[] program = new byte[10];
        for (int index = 0; index < operands.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(2 + 2 * index), operands[index]);
        }
        return frame with { Arena = frame.Arena.Write(0x1000, program).MarkUnknown(0x4000, 0xB000) };
    }

    private static int Offset(uint address) => checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(Offset(address), 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(Offset(address), 2));
}
