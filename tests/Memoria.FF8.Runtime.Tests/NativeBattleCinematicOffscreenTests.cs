using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicOffscreenTests
{
    private const uint Start = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;

    [Theory]
    [InlineData(0x4F, 8)]
    [InlineData(0x90, 10)]
    public void InactiveControlAdvancesWithoutReadingOperandsSlotOrScratchOwners(ushort instruction, uint advance)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x27975D8 + 0x30, 1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 10), Core = frame.Core.MarkUnknown(0x27973E8, 4).MarkUnknown(0x2797690 + 0x74, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicOffscreen.Execute(frame, new(0x1000, 10, 0x80001000), null!);
        Assert.Equal(unchecked(0x80001000 + advance), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1000, 1));
    }

    [Theory]
    [InlineData(0, 0x5EAC)]
    [InlineData(1, 0x9EAC)]
    [InlineData(2, 0x6EAC)]
    [InlineData(3, 0xAEAC)]
    public void ProjectionCaptureAllocatesOptionalTablesAndRunsExactStageGroups(ushort flags, uint size)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x4F, flags, 384, 256);
        List<NativeBattleCinematicOffscreenStageRequest> draws = [];
        List<(uint Address, int Entries)> clears = [];
        List<(uint Ordering, uint Packet)> inserts = [];
        List<uint> executes = [];
        int debug = 0;
        NativeBattleCinematicOffscreenServices services = Services(draws, clears, inserts, executes, () => debug++);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicOffscreen.Execute(frame, new(0x1000, 8, 0x80001000), services);
        uint large = (flags & 1) != 0 ? Start + 0xEC : 0;
        uint small = (flags & 2) != 0 ? Start + ((flags & 1) != 0 ? 0x40ECu : 0xECu) : 0;
        Assert.Equal(small, Dword(result, Start));
        Assert.Equal(large, Dword(result, Start + 4));
        Assert.Equal(size, Dword(result, Start + 0xC));
        Assert.Equal(size, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xC4));
        Assert.Equal(Start + size, Dword(result, Start + 0x14));
        Assert.Equal(Start + size, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(1, debug);
        Assert.Equal(0x80001008u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(unchecked((uint)Start), result.ReadTemporaryDword(0xF0));
        int[] groups = ((flags & 1) != 0 ? new[] { 2, 3 } : []).Concat((flags & 2) != 0 ? new[] { 0, 1 } : []).ToArray();
        Assert.Equal(groups, draws.Select(draw => draw.Group));
        Assert.All(draws, draw => Assert.Equal(0x27979E8u, draw.MatrixAddress));
        Assert.All(draws.Where(draw => draw.Group >= 2), draw => Assert.Equal((byte)4, draw.DepthShift));
        Assert.All(draws.Where(draw => draw.Group < 2), draw => Assert.Equal((byte)6, draw.DepthShift));
        Assert.Equal(clears.Count * 2, draws.Count);
        Assert.Equal(clears.Count * 2, inserts.Count);
        Assert.Equal(clears.Select(clear => clear.Address + (uint)((clear.Entries - 1) * 4)), executes);
        Assert.Equal(Dword(result, Start + 8) + (uint)(16 * draws.Count), result.ReadTemporaryDword(0xE4));
        Assert.Equal(unchecked((ushort)-106), Word(result, Start + 0x10 + 0x14));
        Assert.Equal(unchecked((ushort)-108), Word(result, Start + 0x10 + 0x16));
        Assert.Equal(0x010000C0u, Dword(result, Start + 0x50 + 4));
        Assert.Equal((ushort)490, Word(result, Start + 0x50 + 8));
        Assert.Equal((ushort)364, Word(result, Start + 0x50 + 10));
        Assert.Equal((byte)30, result.Arena.Read(Offset(Start + 0x50 + 0x1B), 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Offset(Start + 0x6C), 3));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Offset(Start + 0xAC + 24), 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Offset(Start + 0x10), 4));
    }

    [Fact]
    public void GroundCaptureUsesEulerViewCurrentPoseAndIndependentFocalAndPreservesUnwrittenPadding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x90, 400, 256, 320, 224);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-32768));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 17);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 90);
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
            ReadCurrentEffect = _ => 204,
            ProjectAnchor = _ => throw new InvalidOperationException("Ground view does not project its anchor through the other capture mode.")
        };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicOffscreen.Execute(frame, new(0x1000, 10, 0x80001000), services);
        Assert.Equal(1, debug);
        Assert.Equal((ushort)256, focal);
        Assert.Equal(32768u, result.ReadTemporaryDword(0xC4));
        Assert.Equal(unchecked((uint)-90), result.ReadTemporaryDword(0xC8));
        Assert.Equal(17u, result.ReadTemporaryDword(0xCC));
        Assert.False(result.TemporaryFrame.Known[0xC2]);
        Assert.False(result.TemporaryFrame.Known[0xC3]);
        NativeBattleCinematicOffscreenStageRequest draw = Assert.Single(draws);
        Assert.Equal(1, draw.Group);
        Assert.Equal(frame.TemporaryFrame.Address + 0xB0, draw.MatrixAddress);
        Assert.Equal((byte)6, draw.DepthShift);
        Assert.Equal((Start + 0xEC, 1024), Assert.Single(clears));
        Assert.Equal(Start + 0xEC + 0xFFC, Assert.Single(executes));
        Assert.Equal(new[] { (Start + 0xEC + 0x40, Start + 0xAC), (Start + 0xEC + 0xFC0, Start + 0x6C) }, inserts);
        Assert.Equal(0x5BECu, Dword(result, Start + 0xC));
        Assert.Equal((ushort)368, Word(result, Start + 0x50 + 8));
        Assert.Equal((ushort)256, Word(result, Start + 0x50 + 10));
        Assert.Equal(new byte[] { 10, 20, 8 }, result.Arena.Read(Offset(Start + 0x50 + 0x19), 3));
        Assert.Equal(0x01000140u, Dword(result, Start + 0x10 + 0x18));
        Assert.Equal(0x8000100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Offset(Start + 0x50 + 28 + 36), 1));
    }

    [Fact]
    public void PrivateStageAndSoftwareServicesReloadLiveOwnersWithoutAdvancingMainPacketCursor()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x90, 384, 256, 320, 224);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x777);
        List<NativeBattleCinematicOffscreenStageRequest> draws = [];
        List<(uint Address, int Entries)> clears = [];
        List<(uint Ordering, uint Packet)> inserts = [];
        List<uint> executes = [];
        NativeBattleCinematicOffscreenServices services = Services(draws, clears, inserts, executes, () => { }) with
        {
            ExecuteSoftwareOrdering45CEE0 = (current, address) =>
            {
                executes.Add(address);
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFE);
            }
        };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicOffscreen.Execute(restored, new(0x1000, 10, 0x80001000), services);
        Assert.Equal(8u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(0x777u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Single(executes);
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
