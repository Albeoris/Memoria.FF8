using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBrothersActor38Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedPhysicalFanUsesSeparatePositionAndDeferredPackedColor(bool culled)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, 0x2797068);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797068 + 0x7C, 0x80001000);
        byte[] view = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(view, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(view.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(view.AsSpan(16), 4096);
        frame = frame with { Core = frame.Core.Write(0x27979E8, view) };
        uint[] screens = [Pack(10, 10), Pack(20, 10), Pack(30, 10), Pack(30, 20), Pack(30, culled ? (short)-30 : (short)30), Pack(20, 30), Pack(10, 30), Pack(10, 20), Pack(20, 20)];
        int projected = 0;
        int colors = 0;
        NativeBattleCinematicBrothersModelServices services = new(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, _) => current, (current, matrix, _) =>
        {
            Assert.Equal((1130, 0, 3030), (matrix.X, matrix.Y, matrix.Z));
            return new(current, screens[projected++], 123, 456);
        }, () => 0, (current, _, ordering, batch) =>
        {
            Assert.Equal(0x4064u, ordering);
            Assert.Equal(0x32123456u, batch.Attempts[0].Primitive!.Words[3]);
            Assert.All(batch.Attempts, attempt => Assert.Equal(new int[] { 0, 0, 0, 0 }, attempt.Primitive!.VertexPayload4));
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), batch);
            Assert.Equal(8, written.Ordering.Proxies.Length);
            Assert.Equal(288, written.Process.ByteCursor);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBrothersModel3.DrawPreparedRing(frame, 1, 0, 0, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), new(1130, 1900, 3030), -1, 0, 0, NativeBattleCameraMath.Cosine(512), _ =>
        {
            colors++;
            return 0x32123456;
        }, services);
        Assert.Equal(9, projected);
        Assert.Equal(culled ? 0 : 1, colors);
        Assert.Equal(culled ? 0x80001000u : 0x80001120u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797068 + 0x7C));
    }

    private static uint Pack(short x, short y) => unchecked((ushort)x) | unchecked((uint)(ushort)y << 16);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedOffsetIsRetainedAndPhysicalPoseAnchorDeltaPositionsTheActor(bool alreadyCaptured)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint header = 0x2797068;
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, header);
        frame = WaterSchedulerFixture.CoreDword(frame, header + 0x60, 0x1D972C0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27971E4, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27971E8, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27971EC, 0);
        frame = frame with { Core = frame.Core.Write(header + 0xD8, new byte[6]).Write(header + 0x84, new byte[] { 10, 0, 20, 0, 30, 0 }) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xBC, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xBE, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC0, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC4, alreadyCaptured ? uint.MaxValue : 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 1024);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 2000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 3000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2000 + 0xC2, 2) };
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGraphicsModel actor = new(0, 2) { Transform = new(new(0, 0, 0), new(10, 20, 30), 0, 0, 4096), GeometryWorldMatrix = alreadyCaptured ? null : identity with { X = 110, Y = 20, Z = 30 } };
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[5] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        List<string> calls = [];
        NativeBattleCinematicBrothersActor38Services services = new((_, address) =>
        {
            Assert.Equal(0x1D972C0u, address);
            return actor;
        }, (current, _, value) =>
        {
            actor = value;
            return current;
        }, (current, _) =>
        {
            calls.Add("restore");
            return new(current, skeleton.ToImmutableArray(), [identity, identity with { X = 20 }], null);
        }, (current, _, basis, position) =>
        {
            calls.Add("shadow");
            Assert.Equal(new NativeBattleVector(1130, 1900, 3030), position);
            Assert.Equal((0, 100, 0), (basis.X, basis.Y, basis.Z));
            Assert.Equal(new NativeBattleVector(1130, 1900, 3030), actor.Transform!.Position28);
            return current;
        }, (current, address) =>
        {
            calls.Add("input");
            Assert.Equal(current.TemporaryFrame.Address + 0xC0, address);
            return current;
        }, NativeBattleActorAnchorRegisterFixture.Services());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBrothersActor38.DrawAF59D0(frame, services);
        Assert.Equal(alreadyCaptured ? new[] { "restore", "input", "input", "shadow" } : new[] { "input", "restore", "input", "input", "shadow" }, calls);
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x2000 + 0xC4, 4)));
        Assert.Equal((ushort)100, WaterSchedulerFixture.ReadSlotWord(result, 0, 0xBC));
        Assert.Equal((short)1024, actor.Rotation16);
        Assert.Equal(alreadyCaptured ? 2 : 0x1002, actor.Flags0);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2000 + 0xC2, 2));
    }
}
