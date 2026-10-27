using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274TargetTests
{
    [Fact]
    public void FirstMoveBindsCanonicalRowBeforeSequenceAndQueuesActualDescriptorPair()
    {
        Fixture fixture = new();
        NativeBattleDuel274TaskStep step = fixture.Advance(fixture.Frame);
        NativeBattleModulePoolTask task = step.Pool.Slots[0];

        Assert.Equal(new[] { "bind", "sequence6", "camera65", "file715", "draw6" }, fixture.Calls);
        Assert.Equal(1, task.ReadWord(12));
        Assert.Equal(49, task.ReadByte(17));
        Assert.Equal(0, step.Frame.Staging.Read(0x25084D8, 1)[0]);
        Assert.Equal(0, step.Frame.Effect.Pools[0].Slots[0].ReadByte(16));
        Assert.Equal(6, step.Frame.Effect.Pools[0].Slots[0].ReadByte(17));
        Assert.Equal(10, task.ReadWord(32));
        Assert.Equal(20, task.ReadWord(34));
        Assert.Equal(30, task.ReadWord(36));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(38));
        Assert.Equal(0x1234u, task.ReadDword(56));
        Assert.Equal((short)0, fixture.Models[0].Rotation14);
        Assert.Equal(new byte[] { 1, 0, 1, 2, 0 }, step.Frame.Memory.Arena!.Read(2356, 5));
        Assert.Equal((short)715, BinaryPrimitives.ReadInt16LittleEndian(step.Frame.Memory.Arena.Read(2366, 2)));
        Assert.Equal((short)716, BinaryPrimitives.ReadInt16LittleEndian(step.Frame.Memory.Arena.Read(2374, 2)));
        Assert.Equal((short)16, BinaryPrimitives.ReadInt16LittleEndian(step.Frame.Memory.Arena.Read(2372, 2)));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 2380, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena.Read(2368, 4)));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 37196, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena.Read(2348, 4)));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 37196 + 81920, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena.Read(2352, 4)));
    }

    [Fact]
    public void FirstMoveWaitsForActualIoBeforePublishingReadyAndClearingInitialFlag()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame).Frame;
        frame = Task(frame, task => task.WriteWord(12, 48));
        NativeBattleDuel274TaskStep waiting = fixture.Advance(frame);

        Assert.Equal(48, waiting.Pool.Slots[0].ReadWord(12));
        Assert.Equal(1, waiting.Pool.Slots[0].ReadByte(16));
        Assert.Equal(0, waiting.Frame.Effect.Parameters!.Parameter81);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(waiting.Frame))!;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(2356, [0, 0, 1, 2, 2]) } };
        NativeBattleDuel274TaskStep ready = fixture.Advance(frame);

        Assert.Equal(49, ready.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0, ready.Pool.Slots[0].ReadByte(16));
        Assert.Equal(1, ready.Pool.Slots[0].ReadByte(18));
        Assert.Equal(0, ready.Pool.Slots[0].ReadByte(19));
        Assert.Equal(1, ready.Frame.Effect.Parameters!.Parameter81);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, ready.Frame.Memory.Arena!.Read(2356, 5));
    }

    [Fact]
    public void ClosingRestoresUnknownPaddingOpaquelyAndExplicitSkipAvoidsTextureAndContact()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame).Frame;
        frame = Task(frame, task => task.WriteByte(15, 8).WriteByte(16, 0).WriteByte(17, 6).WriteWord(12, 0));
        frame = frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Parameter82 = 1 } }, Staging = frame.Staging.Write(0x25084D9, [8]) };
        fixture.Models[0] = fixture.Models[0] with { Rotation12 = 90, Rotation14 = 91, Rotation16 = 92, RotationPadding18 = 0xABCD, Transform = fixture.Models[0].Transform! with { Position28 = new(1000, 1001, 1002), Vertical34 = 1003 } };
        fixture.Calls.Clear();
        NativeBattleDuel274TaskStep step = fixture.Advance(NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!);

        Assert.Equal(2, step.Result);
        Assert.Equal(1, step.Frame.Effect.Parameters!.Parameter81);
        Assert.Empty(fixture.Calls);
        Assert.Equal((short)10, fixture.Models[0].Rotation12);
        Assert.Equal((short)20, fixture.Models[0].Rotation14);
        Assert.Equal((short)30, fixture.Models[0].Rotation16);
        Assert.Null(fixture.Models[0].RotationPadding18);
        Assert.Equal(new NativeBattleVector(10, 11, 12), fixture.Models[0].Transform!.Position28);
        Assert.Equal(13, fixture.Models[0].Transform!.Vertical34);
        Assert.Throws<NotSupportedException>(() => step.Pool.Slots[0].ReadWord(38));
    }

    [Fact]
    public void ClosingWaitsForPhysicalCompletionByteAndItsOwnDurationAcrossSnapshot()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame).Frame;
        frame = Task(frame, task => task.WriteByte(15, 8).WriteByte(16, 0).WriteByte(17, 6).WriteWord(12, 0));
        frame = frame with { Staging = frame.Staging.Write(0x25084D9, [8]) };
        fixture.Calls.Clear();
        NativeBattleDuel274TaskStep queued = fixture.Advance(frame);

        Assert.Equal(new[] { "contact0", "texture" }, fixture.Calls);
        Assert.Equal(0, queued.Result);
        Assert.Equal(1, queued.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0, queued.Pool.Slots[0].ReadByte(96));
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(queued.Frame))!;
        frame = Task(frame, task => task.WriteByte(96, 1).WriteWord(12, 5));
        Assert.Equal(0, fixture.Advance(frame).Result);
        frame = Task(frame, task => task.WriteByte(96, 1).WriteWord(12, 6));
        NativeBattleDuel274TaskStep complete = fixture.Advance(frame);
        Assert.Equal(2, complete.Result);
        Assert.Equal(1, complete.Frame.Effect.Parameters!.Parameter81);
        Assert.Equal(new[] { "contact0", "texture" }, fixture.Calls);
    }

    private static NativeBattleDuel274Frame Task(NativeBattleDuel274Frame frame, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, update(pool.Slots[0])) })) } };
    }

    private sealed class Fixture
    {
        public NativeBattleGraphicsModel[] Models { get; } = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index is 0 or 3 ? (ushort)3 : (ushort)0) { ModelId4 = index == 0 ? (byte)1 : (byte)77, SequenceByte145 = 1, Rotation12 = 10, Rotation14 = 20, Rotation16 = 30, Transform = new(new(0, 0, 0), new(10, 11, 12), 13, 14, 4096) }).ToArray();
        public List<string> Calls { get; } = [];
        public NativeBattleDuel274Frame Frame { get; }
        private NativeBattleDuel274Resources Resources { get; }
        private NativeBattleDuel274SelectedServices Inputs { get; }

        public Fixture()
        {
            NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x25084A8, NativeBattleEffectNodeMemory.ArenaAddress, 116, 1);
            (target, _) = target.Append(0x689AC0);
            target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, target.Slots[0].WriteByte(15, 6).WriteByte(16, 1).WriteDword(20, NativeBattleDuel274Controller.Address(3))) });
            NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0) { Parameter80 = 6, Parameter82 = 0 }, 0x25084B8) { Pools = [NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1), target] };
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x25084D8, [1, 6]);
            for (int index = 0; index < 7; index++)
            {
                staging = staging.Write(checked(0x2508414u + (uint)index * 4), BitConverter.GetBytes((uint)Models[index].Flags0));
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(273);
            memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x689810 } };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
            Frame = new(memory, effect, staging, new(1));
            byte[] data = new byte[NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address];
            data[checked((int)(0x110E3E6 - NativeBattleDuel274Resources.Address))] = 49;
            Resources = new(data.ToImmutableArray(), []);
            NativeBattleDuel274ActorServices actors = new(index => Models[index].Flags0, (index, flags) => Models[index] = Models[index] with { Flags0 = flags }, _ => 0, index => index, _ => throw new InvalidOperationException("Bounds are not re-read by the target."));
            Inputs = new(actors, index => Models[index], (index, model) => Models[index] = model, _ => new(100, 101, 102, 103), (current, index, row) =>
            {
                Assert.Equal(0, index);
                Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 56, row);
                Calls.Add("bind");
                return Task(current, task => task.WriteDword(56, 0x1234));
            }, (current, _, sequence) =>
            {
                Calls.Add("sequence" + sequence);
                return current;
            }, (current, selector) =>
            {
                Calls.Add("camera" + selector);
                return current;
            }, (current, request) =>
            {
                Calls.Add("file" + request.FileId);
                return current;
            }, (current, sequence) =>
            {
                Calls.Add("contact" + sequence);
                return current;
            }, (current, destination, completion) =>
            {
                Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 37196, destination);
                Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 96, completion);
                Calls.Add("texture");
                return current;
            }, (current, _, selector) =>
            {
                Calls.Add("draw" + selector);
                return current;
            }, _ => throw new InvalidOperationException("No transition is active."), (_, _) => throw new InvalidOperationException("No transition is active."));
        }

        public NativeBattleDuel274TaskStep Advance(NativeBattleDuel274Frame frame) => NativeBattleDuel274Target.AdvanceSelected689AC0(frame, 0, Resources, Inputs);
    }
}
