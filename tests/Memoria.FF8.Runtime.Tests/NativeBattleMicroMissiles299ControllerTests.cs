using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMicroMissiles299Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMicroMissiles299ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Initial_root_parity_does_not_read_unreached_models_or_child_services(uint parity, int offset, uint next)
    {
        Fixture fixture = new();
        NativeBattleMicroMissiles299Frame frame = fixture.Frame;
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        frame = frame with { Effect = frame.Effect with { Pools = [root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(16, parity)) }] } };
        NativeBattleMicroMissiles299Frame result = fixture.Step(frame);
        Assert.Equal(ArenaAddress + (uint)offset, result.State.ReadDword(CursorGlobal));
        Assert.Equal(next, result.Effect.Pools[0].Slots[0].ReadDword(16));
        Assert.Single(result.Effect.Pools);
        Assert.Empty(fixture.Operations);
        Assert.Empty(fixture.Anchors);
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Age_one_initializes_exact_arena_pools_and_binds_caster_before_ring()
    {
        Fixture fixture = new();
        NativeBattleMicroMissiles299Frame first = fixture.Step(fixture.Frame);
        NativeBattleMicroMissiles299Frame result = fixture.Step(first);
        Assert.Equal(new ushort[] { 20, 0x80C, 0x144, 0x24 }, result.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 6, 6, 96 }, result.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new[] { RootBank, ArenaAddress + 0x1518, ArenaAddress + 0xD80, ArenaAddress }, result.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Collection(fixture.Operations, operation => Assert.Equal(new NativeBattleMicroMissiles299BindCaster(0, ArenaAddress + BindingOffset, 0x1047120), operation), operation => Assert.Equal(new NativeBattleMicroMissiles299RingSequence(0, 2), operation));
    }

    [Fact]
    public void Complete_real_workspace_cycle_has_six_births_one_contact_and_actual_required_draw_calls()
    {
        Fixture fixture = new();
        NativeBattleMicroMissiles299Frame current = fixture.Frame;
        int callbacks = 0;
        while (current.Effect.ReturnedDescriptor != 0 && callbacks++ < 96)
        {
            current = fixture.Step(current);
        }
        Assert.InRange(callbacks, 1, 95);
        Assert.Equal(0u, current.Effect.ReturnedDescriptor);
        Assert.Equal(Enumerable.Repeat(0, 6), fixture.Anchors);
        Assert.Single(fixture.Operations.OfType<NativeBattleMicroMissiles299Contact>());
        Assert.Equal(0, fixture.Operations.OfType<NativeBattleMicroMissiles299Contact>().Single().Index);
        Assert.Single(fixture.Operations.OfType<NativeBattleMicroMissiles299Sound>());
        Assert.True(fixture.Operations.OfType<NativeBattleMicroMissiles299DrawSprite>().Any());
        Assert.True(fixture.Operations.OfType<NativeBattleMicroMissiles299DrawParticle>().Any());
        Assert.True(fixture.EmittedNodes > 0);
        Assert.All(current.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Particle_draw_precedes_WORD_motion_and_completion_does_not_consume_random()
    {
        Fixture fixture = new();
        NativeBattleMicroMissiles299Frame frame = fixture.Children();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[3];
        (pool, int slot) = pool.Append(ParticleCallback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 32760).WriteWord(0xE, 1).WriteWord(0x10, 2).WriteWord(0x12, 11).WriteWord(0x14, 20).WriteWord(0x16, 0xFFFF).WriteWord(0x18, 3).WriteWord(0x1C, 0xFFFD).WriteWord(0x1E, 2).WriteWord(0x20, 0xFFFC);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(3, pool) } };
        NativeBattleModulePoolTask? before = null;
        fixture.Observe = (current, operation) =>
        {
            if (operation is NativeBattleMicroMissiles299DrawParticle particle)
            {
                before = NativeBattleMicroMissiles299Controller.Read(current, particle.TaskAddress);
            }
        };
        NativeBattleMicroMissiles299Frame result = fixture.Step(frame);
        Assert.NotNull(before);
        Assert.Equal((ushort)32760, before.ReadWord(0xC));
        Assert.Equal((ushort)11, before.ReadWord(0x12));
        NativeBattleModulePoolTask after = result.Effect.Pools[3].Slots[slot];
        Assert.Equal(unchecked((ushort)32780), after.ReadWord(0xC));
        Assert.Equal((ushort)0, after.ReadWord(0xE));
        Assert.Equal((ushort)17, after.ReadWord(0x14));
        Assert.Equal((ushort)1, after.ReadWord(0x16));
        Assert.Equal((ushort)0xFFFF, after.ReadWord(0x18));
        Assert.Equal((ushort)12, after.ReadWord(0x12));
        Assert.Equal(JsonSerializer.Serialize(frame.Random), JsonSerializer.Serialize(result.Random));
    }

    [Fact]
    public void Cold_restored_active_callbacks_produce_the_same_next_frame_and_required_operations()
    {
        Fixture first = new();
        NativeBattleMicroMissiles299Frame current = first.Frame;
        for (int tick = 0; tick < 14; tick++)
        {
            current = first.Step(current);
        }
        NativeBattleMicroMissiles299Frame restored = JsonSerializer.Deserialize<NativeBattleMicroMissiles299Frame>(JsonSerializer.Serialize(current))!;
        Fixture second = new();
        first.Operations.Clear();
        Assert.Equal(JsonSerializer.Serialize(first.Step(current)), JsonSerializer.Serialize(second.Step(restored)));
        Assert.Equal(JsonSerializer.Serialize(first.Operations), JsonSerializer.Serialize(second.Operations));
    }

    private sealed class Fixture
    {
        public NativeBattleMicroMissiles299Frame Frame { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public NativeBattleMicroMissiles299Resources Resources { get; }
        public List<NativeBattleMicroMissiles299Operation> Operations { get; } = [];
        public List<int> Anchors { get; } = [];
        public int EmittedNodes { get; private set; }
        public Action<NativeBattleMicroMissiles299Frame, NativeBattleMicroMissiles299Operation>? Observe { get; set; }
        private NativeBattlePoseSource Pose { get; }
        private NativeBattleGraphicsModel Model { get; }
        public Fixture()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, 299, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            Packets = packets;
            byte[] skeleton = new byte[112];
            skeleton[0] = 2;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            Pose = new(skeleton.ToImmutableArray(), [1]);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            Model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(new(3, 2), Pose);
            Model = Model with { GeometryWorldMatrix = identity, CurrentBodyBoneMatrices = Model.CurrentBodyBoneMatrices! with { Matrices = [identity, identity with { X = 100, Y = 200, Z = 1600 }] }, Rotation14 = 0, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 100, 4096) };
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int slot) = root.Append(RootCallback);
            root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0)) };
            NativeBattleOriginalEffectState effect = new(299, 299, new(299, Entry, Initializer), new(0, 1, 8, 0, 0, 0), RootDescriptor) { Pools = [root] };
            Frame = new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(299), effect, new(1), NativeBattleMicroMissiles299State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D972C0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Resources = new(Source(0x10490A8, 45), Source(0x104B63C, 12), Source(0x104C910, 16), new(0, 0x1048C94, 0, 0, []), new(1, 0x1048DD8, 0, 0, []), new(2, 0x1048F1C, 0, 0, []), new("fixture", []), new("fixture", []), 0x55787);
        }
        public NativeBattleMicroMissiles299Frame Children()
        {
            NativeBattleMicroMissiles299Frame current = Step(Step(Frame));
            NativeBattleEffectTaskPool root = current.Effect.Pools[0];
            return current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(0, root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 11)) }) } };
        }
        public NativeBattleMicroMissiles299Frame Step(NativeBattleMicroMissiles299Frame frame) => NativeBattleMicroMissiles299Controller.Advance(frame, Resources, new(Packets, _ => Model, _ => Pose, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0x1234), (current, actor) =>
        {
            Anchors.Add(actor);
            return new(current, new(0, 100, 0, 100));
        }, (current, operation) =>
        {
            Operations.Add(operation);
            Observe?.Invoke(current, operation);
            return current;
        }, (current, _, _, _) =>
        {
            EmittedNodes++;
            return current;
        }, new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L))));
        private static NativeBattleGfAnimationSource Source(uint address, int duration)
        {
            byte[] bytes = new byte[80];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 32);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), duration);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 16);
            return new(address, bytes.ToImmutableArray());
        }
    }
}
