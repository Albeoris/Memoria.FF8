using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorClone48Tests
{
    [Fact]
    public void InitializationCopiesNativeMixedCaptureFieldsAndUsesCurrentPoseServicesInOrder()
    {
        List<string> calls = [];
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorClone48.Draw(frame, Services(calls, (current, clone, actor, basis) =>
        {
            calls.Add("draw");
            Assert.Equal(Address(0x4000), clone);
            Assert.Equal(0x1D972C0u, actor);
            Assert.Equal((105, 206, 307), (basis.X, basis.Y, basis.Z));
            Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, basis.Rotation);
            Assert.Equal(Address(0x4080), Dword(current, 0x4044));
            Assert.Equal(0x11223344u, Dword(current, 0x405C));
            Assert.Equal(3u, Dword(current, 0x4060));
            Assert.Equal((ushort)30, WaterSchedulerFixture.ReadSlotWord(current, 0, 0xAC));
            Assert.Equal((ushort)30, WaterSchedulerFixture.ReadSlotWord(current, 0, 0xC4));
            Assert.Throws<NotSupportedException>(() => current.Arena.Read(0x4010, 12));
            Assert.Throws<NotSupportedException>(() => current.Arena.Read(0x4030, 16));
            return new(current, 0x12345698);
        }));
        Assert.Equal(new[] { "hide", "resolve", "refresh", "color", "mask", "resolve", "input", "refresh", "draw", "restore" }, calls);
        Assert.Equal(Address(0x4080), WaterSchedulerFixture.ReadCoreDword(result, 0x2797704));
        Assert.Equal(0x12345698u, WaterSchedulerFixture.ReadCoreDword(result, 0x279770C));
        Assert.Equal(0x1D972C0u, Dword(result, 0x400C));
        Assert.Equal(0x00D00140u, Dword(result, 0x4058));
        Assert.Equal(uint.MaxValue, Dword(result, 0x4050));
        Assert.Equal((ushort)0, Word(result, 0x4002));
        Assert.Equal(0, result.Arena.Read(0x4021, 1)[0]);
    }

    [Fact]
    public void ExistingCloneRefreshesWithoutReallocationAndRotationZUsesReferencePositionZ()
    {
        List<string> calls = [];
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicActorClone48.Draw(Fixture(), Services(calls, (current, _, _, _) => new(current, 0x12345678)));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 15);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 25);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 50);
        calls.Clear();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorClone48.Draw(frame, Services(calls, (current, _, _, basis) =>
        {
            calls.Add("draw");
            Assert.Equal((110, 211, 327), (basis.X, basis.Y, basis.Z));
            Assert.Equal((ushort)20, current.ReadTemporaryWord(0x84));
            Assert.Equal((ushort)0, current.ReadTemporaryWord(0x80));
            Assert.Equal((ushort)0, current.ReadTemporaryWord(0x82));
            Assert.Equal((ushort)30, WaterSchedulerFixture.ReadSlotWord(current, 0, 0xAC));
            return new(current, 0x12345678);
        }));
        Assert.Equal(new[] { "resolve", "input", "refresh", "draw", "restore" }, calls);
        Assert.Equal(Address(0x4080), WaterSchedulerFixture.ReadCoreDword(result, 0x2797704));
    }

    private static NativeBattleCinematicActorClone48Services Services(List<string> calls, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleActionMatrix, NativeBattleCinematicActorCloneDraw> draw) => new((_, selector) =>
    {
        calls.Add("resolve");
        Assert.Equal((ushort)1, selector);
        return Address(0x2100);
    }, (current, actor) =>
    {
        calls.Add("hide");
        Assert.Equal(0x1D972C0u, actor);
        return current;
    }, _ =>
    {
        calls.Add("color");
        return 0x11223344;
    }, _ =>
    {
        calls.Add("mask");
        return 3;
    }, (current, clone, actor) =>
    {
        calls.Add("refresh");
        Assert.Equal(0x1D972C0u, actor);
        int offset = checked((int)(clone - NativeBattleEffectNodeMemory.ArenaAddress));
        return current with { Arena = current.Arena.MarkUnknown(offset + 0x10, 12).MarkUnknown(offset + 0x30, 16) };
    }, (current, address) =>
    {
        calls.Add("input");
        Assert.Equal(current.TemporaryFrame.Address + 0xC0, address);
        return current;
    }, draw, (current, _) =>
    {
        calls.Add("restore");
        return current;
    });

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279770C, 0x12345678);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27970C8, 0x1D972C0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = frame with { Arena = frame.Arena.Write(0x3000, new byte[] { 1, 0 }) };
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 10);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 20);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 30);
        for (int index = 0; index < 9; index++)
        {
            ushort value = index % 4 == 0 ? (ushort)4096 : (ushort)0;
            frame = WaterSchedulerFixture.CoreWord(frame, 0x2797968u + (uint)(index * 2), value);
            frame = WaterSchedulerFixture.CoreWord(frame, 0x27979E8u + (uint)(index * 2), value);
        }
        for (int index = 0; index < 3; index++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, 0x27970ECu + (uint)(index * 2), (ushort)((index + 1) * 100));
            frame = WaterSchedulerFixture.CoreDword(frame, 0x27979FCu + (uint)(index * 4), (uint)(index + 5));
        }
        return frame;
    }

    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
}
