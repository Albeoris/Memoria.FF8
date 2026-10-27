using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset)]
    [InlineData(1u, FirstPacketOffset)]
    public void Root_uses_old_DWORD_toggle_and_keeps_target_phase_independent(uint toggle, int offset)
    {
        Fixture fixture = new();
        NativeBattleAcid317Frame frame = NativeBattleAcid317Controller.Write(fixture.Frame, RootBank, NativeBattleAcid317Controller.Read(fixture.Frame, RootBank).WriteDword(16, toggle));
        frame = fixture.Step(frame);
        Assert.Equal(ArenaAddress + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(toggle == 0 ? 1u : 0u, NativeBattleAcid317Controller.Read(frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)11, NativeBattleAcid317Controller.Read(frame, ArenaAddress + 0x1280).ReadWord(12));
        Assert.Single(fixture.Operations.OfType<NativeBattleAcid317Sound>());
    }

    [Fact]
    public void Root_initializes_once_and_snapshots_target_before_binding_caster()
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool root = fixture.Frame.Effect.Pools[0];
        root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(12, 1).WriteDword(16, 0)) });
        NativeBattleAcid317Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [root] } };
        frame = fixture.Step(frame);
        Assert.Equal(4, frame.Effect.Pools.Length);
        Assert.Equal(new ushort[] { 20, 616, 80, 32 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 1, 8, 128 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Collection(fixture.Operations, operation => Assert.Equal(new NativeBattleAcid317SnapshotBody(3, ArenaAddress + BodyCacheOffset), operation), operation => Assert.Equal(new NativeBattleAcid317BindCaster(0, ArenaAddress + BindingOffset, 0xFFE6B0), operation), operation => Assert.Equal(new NativeBattleAcid317RingSequence(0, 3), operation), operation => Assert.Equal(new NativeBattleAcid317ActorFlags(3, 4, 0), operation));
        Assert.Equal((ushort)1, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal((ushort)99, frame.Effect.Pools[1].Slots[0].ReadWord(0x1C));
        Assert.Equal((byte)1, NativeBattleAcid317Controller.Read(frame, RootBank).ReadByte(14));
        Assert.Equal(0x1000224u, frame.Effect.Pools[1].Slots[0].ReadDword(0x20));
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(PointsOffset + 6, 2)));
        fixture.Operations.Clear();
        frame = fixture.Step(frame);
        Assert.DoesNotContain(fixture.Operations, operation => operation is NativeBattleAcid317BindCaster or NativeBattleAcid317SnapshotBody);
    }

    [Fact]
    public void Target_draws_ACT_then_sine_body_and_contacts_at_old_fifty_seven()
    {
        Fixture fixture = new();
        NativeBattleAcid317Frame frame = fixture.TargetPhase(57);
        frame = fixture.Step(frame);
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleAcid317AdvanceAct>(operation), operation => Assert.Equal(new NativeBattleAcid317ActorFlags(3, 0, 4), operation), operation => Assert.Equal(new NativeBattleAcid317DrawBody132(3, unchecked((short)NativeBattleCameraMath.Sine(24 * 2048 / 40))), operation), operation => Assert.Equal(new NativeBattleAcid317Contact(0), operation));
        Assert.Equal((ushort)58, frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Terminal_target_clears_hide_flag_after_real_ACT_service_and_releases_root()
    {
        Fixture fixture = new();
        NativeBattleAcid317Frame frame = fixture.Step(fixture.TargetPhase(73));
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleAcid317AdvanceAct>(operation), operation => Assert.Equal(new NativeBattleAcid317ActorFlags(3, 4, 0), operation));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)73, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.All(frame.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
    }

    [Theory]
    [InlineData(0x660830u, 0x10016A8u, 9, 36)]
    [InlineData(0x6608D0u, 0x100139Cu, 13, 21)]
    [InlineData(0x660D40u, 0x10014FCu, 16, 20)]
    public void Particle_writes_real_phase_and_advances_its_distinct_motion_after_draw(uint callback, uint source, int duration, int velocity)
    {
        Fixture fixture = new();
        NativeBattleAcid317Frame frame = fixture.OnlyParticle(callback);
        uint address = ArenaAddress;
        frame = NativeBattleAcid317Controller.Write(frame, address, NativeBattleAcid317Controller.Read(frame, address).WriteDword(12, 0).WriteWord(0x10, 10).WriteWord(0x12, 20).WriteWord(0x14, 30).WriteWord(0x16, 1024).WriteWord(0x18, 5).WriteWord(0x1A, 20).WriteWord(0x1C, 7));
        frame = fixture.Step(frame);
        NativeBattleAcid317DrawSprite180 draw = Assert.Single(fixture.Operations.OfType<NativeBattleAcid317DrawSprite180>());
        Assert.Equal(source, draw.SourceAddress);
        Assert.Equal((ushort)0, draw.Phase);
        NativeBattleModulePoolTask task = NativeBattleAcid317Controller.Read(frame, address);
        Assert.Equal(1u, task.ReadDword(12));
        Assert.Equal((ushort)velocity, task.ReadWord(0x1A));
        Assert.Equal((ushort)(callback == 0x660D40 ? 10 : 15), task.ReadWord(0x10));
        frame = NativeBattleAcid317Controller.Write(frame, address, task.WriteDword(12, (uint)(duration - 1)));
        frame = fixture.Step(frame);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Chain_births_and_weighted_registers_are_preserved_by_standalone_JSON()
    {
        Fixture first = new();
        NativeBattleAcid317Frame frame = first.Step(first.TargetPhase(18));
        Assert.Equal(3, first.Operations.OfType<NativeBattleAcid317ProjectPoints>().Count());
        Assert.Equal(3, first.Operations.OfType<NativeBattleAcid317DrawTrail>().Count());
        Assert.Equal(8, first.Operations.OfType<NativeBattleAcid317DrawSprite180>().Count());
        Assert.NotNull(frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Null(frame.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(11, first.MouthCalls);
        NativeBattleAcid317Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAcid317Frame>(NativeBattleTestSnapshots.Json(frame))!;
        first.Operations.Clear();
        Fixture second = new();
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Step(frame)), NativeBattleTestSnapshots.Json(second.Step(restored)));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Operations), NativeBattleTestSnapshots.Json(second.Operations));
    }

    private sealed class Fixture
    {
        public NativeBattleAcid317Frame Frame { get; }
        public List<NativeBattleAcid317Operation> Operations { get; } = [];
        public int MouthCalls { get; private set; }
        private readonly NativeBattleActionPacketsState packets;
        private readonly NativeBattleAcid317Resources resources;
        public Fixture()
        {
            packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, 317, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, _) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 2).WriteByte(15, 1)) });
            NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, ArenaAddress + 0x1280, 616, 1);
            (target, _) = target.Append(TargetCallback);
            target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, target.Slots[0].WriteBytes(12, new byte[604]).WriteWord(12, 10).WriteDword(16, 0x1D97494)) });
            NativeBattleOriginalEffectState effect = new(317, 317, new(317, Entry, Initializer), new(0, 1, 8, 0, 0, 0), RootDescriptor) { Pools = [root, target, NativeBattleEffectTaskPool.Initialize(TrailDescriptor, ArenaAddress + 0x1000, 80, 8), NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, ArenaAddress, 32, 128)] };
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            for (int index = 0; index < PointCount; index++)
            {
                arena = arena.Write(PointsOffset + index * PointStride + 6, BitConverter.GetBytes((ushort)0xFFFF));
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(317) with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
            Frame = new(memory, effect, new(1), NativeBattleAcid317State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D972C0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            byte[] act = new byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(12), 20);
            resources = new(new("fixture", []), new(0x1000224, act.ToImmutableArray()), new(0, 0x100139C, 0, 0, []), new(0, 0x10014FC, 0, 0, []), new(0, 0x10016A8, 0, 0, []), new("fixture", []), new byte[16384].ToImmutableArray(), new byte[8].ToImmutableArray(), 0x55784);
        }
        public NativeBattleAcid317Frame TargetPhase(ushort phase) => NativeBattleAcid317Controller.Write(Frame, ArenaAddress + 0x1280, NativeBattleAcid317Controller.Read(Frame, ArenaAddress + 0x1280).WriteWord(12, phase));
        public NativeBattleAcid317Frame OnlyParticle(uint callback)
        {
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, ArenaAddress + 0x1280, 616, 1);
            NativeBattleEffectTaskPool particles = Frame.Effect.Pools[3];
            (particles, _) = particles.Append(callback);
            return Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(1, targets).SetItem(3, particles) } };
        }
        public NativeBattleAcid317Frame Step(NativeBattleAcid317Frame frame) => NativeBattleAcid317Controller.Advance(frame, resources, new(packets, actor => new(actor, 2), _ => 30, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0), (current, _) => new(current, new(100, 200, 300, 99)), (current, _, _, _) => new(current, new(200, 250, 400)), current =>
        {
            MouthCalls++;
            return new(current, new(20, 30, 40));
        }, (current, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleAcid317DrawSprite180 draw)
            {
                Assert.Equal(draw.Phase, BitConverter.ToUInt16(current.Staging!.Read(draw.TemporaryAddress + 4, 2)));
            }
            return current;
        }) { Colors = new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L)) });
    }
}
