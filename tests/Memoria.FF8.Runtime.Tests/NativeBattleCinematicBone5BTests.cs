using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBone5BTests
{
    [Fact]
    public void MissingEmbeddedModelSkipsExtendedOperandsAndDoesNotRestoreOrUpdate()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x285B);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xBC, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3004, 4) };
        NativeBattleCinematicBone5BServices services = new((_, selector) =>
        {
            Assert.Equal(ushort.MaxValue, selector);
            return Address(0x2100);
        }, (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), NativeBattleActorAnchorRegisterFixture.Services());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBone5B.Execute(frame, Program(), services);
        Assert.Equal(Address(0x3008), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void PhysicalBasicBranchNeverReadsSelectorOperandAndUpdatesAfterPositionWrites()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x045B);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3002, 6) };
        int restores = 0;
        NativeBattleCinematicBone5BServices services = new((_, _) => throw new InvalidOperationException(), (current, owner, physical) =>
        {
            restores++;
            Assert.True(physical);
            Assert.Equal(0x1D972C0u, owner);
            Assert.Equal(Address(0x3004), WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return new(current, Skeleton(), Bones(), Identity(100, 200, 300));
        }, (current, kind) =>
        {
            Assert.Equal(3, kind);
            Assert.Equal(110u << 16, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x5C));
            Assert.Equal(220u << 16, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x60));
            Assert.Equal(430u << 16, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x64));
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xF00D);
        }, (current, address) =>
        {
            Assert.Equal(current.TemporaryFrame.Address + 0xC0, address);
            return current;
        }, NativeBattleActorAnchorRegisterFixture.Services());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBone5B.Execute(frame, Program(), services);
        Assert.Equal(1, restores);
        Assert.Equal(0xF00Du, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void ExtendedEmbeddedBranchUsesSignedFractionAndLiteralByteOffsetWithoutUpdating()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x285B);
        frame = PutWord(frame, 0x3004, 0x8001);
        frame = PutWord(frame, 0x3006, 128);
        NativeBattleCinematicBone5BServices services = new((_, _) => Address(0x2100), (current, source, physical) =>
        {
            Assert.False(physical);
            Assert.Equal(Address(0x2100), source);
            Assert.Equal(Address(0x3008), WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return new(current, Skeleton(), Bones(), null);
        }, (_, _) => throw new InvalidOperationException(), (current, _) => current, NativeBattleActorAnchorRegisterFixture.Services());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBone5B.Execute(frame, Program(), services);
        Assert.Equal(15, Int(result, 0x2094));
        Assert.Equal(26, Int(result, 0x2098));
        Assert.Equal(87, Int(result, 0x209C));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2092, 2));
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(result))!;
        Assert.Equal(87u, restored.ReadTemporaryDword(0xF8));
        Assert.Throws<NotSupportedException>(() => restored.Arena.Read(0x2092, 2));
    }

    [Fact]
    public void AddressedAnchorReadsOnlyCurrentMetadataAndNeverMatrixPadding()
    {
        byte[] metadata = Skeleton().ToArray();
        List<(int Offset, int Length)> reads = [];
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(Fixture(0x5B), metadata.Length, (offset, length) =>
        {
            reads.Add((offset, length));
            Assert.True(offset < 16 || offset % 48 is 16 or 18);
            return metadata.AsSpan(offset, length).ToArray();
        }, Bones(), 0xF1, -4096, Identity(), NativeBattleActorAnchorRegisterFixture.Registers());
        Assert.Equal(-70, result.Anchor.Z);
        Assert.Equal(new[] { (0, 1), (5, 1), (64, 2), (18, 2) }, reads);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort opcode)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, opcode);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x3000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27970C8, 0x1D972C0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 5);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 6);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 7);
        frame = frame with { Arena = frame.Arena.Write(0x2018, [3]) };
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xBC, Address(0x4000));
        foreach (int field in new[] { 0x8C, 0x8E, 0x90 })
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 1, field, 256);
        }
        for (int index = 0; index < 9; index++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, 0x2797968u + (uint)(index * 2), index % 4 == 0 ? (ushort)4096 : (ushort)0);
        }
        return PutWord(frame, 0x3002, ushort.MaxValue);
    }

    private static ImmutableArray<byte> Skeleton()
    {
        byte[] bytes = new byte[112];
        bytes[0] = 2;
        bytes[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(64), 0);
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<NativeBattleActionMatrix> Bones() => [Identity(), Identity(10, 20, 30)];
    private static NativeBattleActionMatrix Identity(int x = 0, int y = 0, int z = 0) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, y, z, 4096);
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static NativeBattleWaterScriptProgram Program() => new(0x3000, 8, Address(0x3000));
    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value) => frame with { Arena = frame.Arena.Write(offset, BitConverter.GetBytes(value)) };
    private static int Int(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Arena.Read(offset, 4));
}
