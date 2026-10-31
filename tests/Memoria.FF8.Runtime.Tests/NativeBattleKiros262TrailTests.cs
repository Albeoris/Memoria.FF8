using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleKiros262TrailTests
{
    [Fact]
    public void FirstDrawAllocatesSixteenBytesButOnlyClearsTheCounterAndActiveHistoryFlags()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleKiros262TrailState state = NativeBattleKiros262TrailState.FromColdProcess();
        state = state with { Memory = state.Memory.Write(NativeBattleKiros262TrailState.History + 24, [7, 8, 9, 10]) };
        int restores = 0;
        NativeBattleKiros262TrailStep result = NativeBattleKiros262Trail.Draw(frame, state, Services((current, _, selector) =>
        {
            Assert.Equal((ushort)0xF1, selector);
            return new(current, new(10, 20, 30), 0, 0, -4096);
        }, (current, _) =>
        {
            restores++;
            return current;
        }, (_, _, _) => throw new InvalidOperationException()));
        Assert.Equal(1, restores);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, result.State.Memory.Read(NativeBattleKiros262TrailState.History + 24, 4));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Arena.Read(0x5000, 2)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 14), result.Frame.Arena.Read(0x5002, 14));
        Assert.Equal(0x20E4AB8u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x20BC, 4)));
        Assert.Equal(0x20E4AC8u, WaterSchedulerFixture.ReadCoreDword(result.Frame, WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC) + 0x74));
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0 }, result.State.Memory.Read(NativeBattleKiros262TrailState.History + 4, 6));
        Assert.Equal((short)-636, BinaryPrimitives.ReadInt16LittleEndian(result.State.Memory.Read(NativeBattleKiros262TrailState.History + 16, 2)));
        Assert.Throws<NotSupportedException>(() => result.State.Memory.Read(NativeBattleKiros262TrailState.History + 10, 2));
    }

    [Fact]
    public void ExistingCounterAndNonzeroYSkipPayloadReadsPoseMutationAndCounterAdvance()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(0x20BC, BitConverter.GetBytes(0x20E4AB8u)).Write(0x2096, [1, 0]).MarkUnknown(0x3000, 4) };
        NativeBattleKiros262TrailServices services = Services((_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
        NativeBattleKiros262TrailStep result = NativeBattleKiros262Trail.Draw(frame, NativeBattleKiros262TrailState.FromColdProcess(), services);
        Assert.Equal(frame, result.Frame);
        Assert.Empty(NativeBattleStorageSnapshots.Capture(result.State.Memory).Regions);
    }

    [Fact]
    public void SecondSampleReusesExactCurvesAndPreservesTheNativeHistoryAcrossSnapshot()
    {
        NativeBattleKiros262TrailServices firstServices = Services((current, actor, _) =>
        {
            Assert.Equal(0x1D972C0u, actor);
            return new(current, new(10, 20, 30), 0, 0, -4096);
        }, (current, _) => current, (_, _, _) => throw new InvalidOperationException());
        NativeBattleKiros262TrailStep first = NativeBattleKiros262Trail.Draw(Fixture(), NativeBattleKiros262TrailState.FromColdProcess(), firstServices);
        NativeBattleKiros262TrailState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleKiros262TrailState>(NativeBattleTestSnapshots.Json(first.State))!;
        int emitted = 0;
        NativeBattleKiros262TrailServices secondServices = Services((current, _, _) => new(current, new(40, 50, 60), 0, 0, -4096), (current, _) => current, (current, state, color) =>
        {
            emitted++;
            Assert.Equal(0x3A123456u, color);
            Assert.Equal((short)40, BinaryPrimitives.ReadInt16LittleEndian(state.Memory.Read(NativeBattleKiros262TrailState.CurveA, 2)));
            Assert.Equal((short)10, BinaryPrimitives.ReadInt16LittleEndian(state.Memory.Read(NativeBattleKiros262TrailState.CurveA + 31 * 8, 2)));
            return current;
        });
        var independentFrame = NativeBattleTestSnapshots.Independent(first.Frame);
        var expected = NativeBattleKiros262Trail.Draw(first.Frame, first.State, secondServices);
        var actual = NativeBattleKiros262Trail.Draw(independentFrame, restored, secondServices);
        Assert.Equal(2, emitted);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Arena.Read(0x5000, 2)));
        Assert.Throws<InvalidDataException>(() => (actual.State with { Memory = actual.State.Memory.Write(0x288, [1]) }).Validate());
    }

    [Fact]
    public void Captured_history_gather_preserves_detached_native_state_packets_and_replaced_sample_births()
    {
        var native = Run(false);
        string frozen = NativeBattleTestSnapshots.Json(native.Step);
        string packets = NativeBattleTestSnapshots.Json(native.Batches);
        var captured = Run(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured.Step));
        Assert.Equal(packets, NativeBattleTestSnapshots.Json(captured.Batches));
        Assert.Equal(2, captured.Identities.Count);
        Assert.NotEqual(captured.Identities[0].ResourceGeneration, captured.Identities[1].ResourceGeneration);
        foreach (NativeBattlePilotVisualIdentity identity in captured.Identities)
        {
            Assert.Equal((ushort)262, identity.Effect);
            Assert.Equal(11u, identity.Bank);
            Assert.Equal(2, identity.Slot);
            Assert.Equal(3, identity.Generation);
        }
        Assert.All(captured.Batches, batch =>
        {
            Assert.NotEmpty(batch.Attempts);
            Assert.All(batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt =>
            {
                NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
                Assert.Equal((ushort)262, geometry.Identity.Effect);
                Assert.True(geometry.UseSourceDepths);
                Assert.Equal(primitive.VertexPayload4[0], geometry.SourceDepths!.Value.X);
            });
        });

        (NativeBattleKiros262TrailStep Step, List<NativeBattleGfGeometryBatch> Batches, List<NativeBattlePilotVisualIdentity> Identities) Run(bool capture)
        {
            NativeBattleWaterExecutionFrame frame = Fixture();
            NativeBattleKiros262TrailState state = NativeBattleKiros262TrailState.FromColdProcess();
            List<NativeBattleGfGeometryBatch> batches = [];
            List<NativeBattlePilotVisualIdentity> identities = [];
            NativeBattlePilotVisualIdentity? emittedIdentity = null;
            int sample = 0;
            NativeBattleKiros262TrailServices services = Services((current, _, _) => new(current, new((short)(10 + sample++ * 30), 20, 30), 0, 0, -4096), (current, _) => current, (current, trail, color) =>
            {
                NativeBattleEffectArena curves = NativeBattleEffectArena.Clear();
                for (int index = 0; index < 32; index++)
                {
                    curves = curves.Write(NativeBattleGilgameshSwordTrailState.CurveA + index * 8, trail.Memory.Read(NativeBattleKiros262TrailState.CurveA + index * 8, 6));
                    curves = curves.Write(NativeBattleGilgameshSwordTrailState.CurveB + index * 8, trail.Memory.Read(NativeBattleKiros262TrailState.CurveB + index * 8, 6));
                }
                NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 3000 };
                NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 288, 160, 108);
                NativeBattleGilgameshSwordTrailPacketResult drawn = NativeBattleGilgameshSwordTrailPackets.Emit(new(curves), view, projection, NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), NativeBattleGteColorInterpolation.CodeConstants45F270, color, pilotIdentity: emittedIdentity);
                batches.Add(NativeBattleCinematicActorPresentation.NativeDepths(drawn.Batch));
                Assert.Equal(drawn.Batch.AllocationBytes + 36, drawn.PoolCursorAdvance);
                return current;
            }) with
            {
                ObserveIdentity = identity =>
                {
                    identities.Add(identity);
                    emittedIdentity = identity;
                }
            };
            for (int index = 0; index < 3; index++)
            {
                NativeBattleKiros262TrailStep step = NativeBattleKiros262Trail.Draw(frame, state, services, capture ? new(262, 11, 2, 3, 4) : null);
                frame = step.Frame;
                state = step.State;
            }
            return (new(frame, state), batches, identities);
        }
    }

    [Fact]
    public void Captured_suspended_trail_keeps_unreached_payload_unknown_and_emits_no_identity()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(0x20BC, BitConverter.GetBytes(0x20E4AB8u)).Write(0x2096, [1, 0]).MarkUnknown(0x3000, 4) };
        NativeBattleKiros262TrailServices services = Services((_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException()) with { ObserveIdentity = _ => throw new InvalidOperationException() };
        NativeBattleKiros262TrailStep result = NativeBattleKiros262Trail.Draw(frame, NativeBattleKiros262TrailState.FromColdProcess(), services, new(262, 11, 2, 3, 4));
        Assert.Equal(frame, result.Frame);
        Assert.Empty(NativeBattleStorageSnapshots.Capture(result.State.Memory).Regions);
    }

    private static NativeBattleKiros262TrailServices Services(Func<NativeBattleWaterExecutionFrame, uint, ushort, NativeBattleKiros262Tip> read, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> restore, Func<NativeBattleWaterExecutionFrame, NativeBattleKiros262TrailState, uint, NativeBattleWaterExecutionFrame> emit) => new(read, restore, emit);

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint manager = WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0x74, 0x20E4AB8);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60, 0x1D972C0);
        return frame with { Arena = frame.Arena.Write(0x20B8, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x3000u)).Write(0x3000, [0xF1, 0, 2, 0]).Write(0x2094, BitConverter.GetBytes((short)666)).Write(0x20CC, BitConverter.GetBytes(0x123456u)).Write(0x5000, Enumerable.Repeat((byte)0xA5, 16).ToArray()) };
    }
}
