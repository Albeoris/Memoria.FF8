using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGastricJuice128ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset)]
    [InlineData(1u, FirstPacketOffset)]
    public void Root_uses_old_DWORD_toggle_and_keeps_target_phase_independent(uint toggle, int offset)
    {
        Fixture fixture = new();
        NativeBattleGastricJuice128Frame frame = NativeBattleGastricJuice128Controller.Write(fixture.Frame, RootBank, NativeBattleGastricJuice128Controller.Read(fixture.Frame, RootBank).WriteDword(16, toggle));
        frame = fixture.Step(frame);
        Assert.Equal(ArenaAddress + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(toggle == 0 ? 1u : 0u, NativeBattleGastricJuice128Controller.Read(frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)11, NativeBattleGastricJuice128Controller.Read(frame, ArenaAddress + 0x1280).ReadWord(12));
        Assert.Single(fixture.Operations.OfType<NativeBattleGastricJuice128Sound>());
    }

    [Fact]
    public void Root_initializes_once_and_snapshots_actual_target_before_first_main_callback()
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool root = fixture.Frame.Effect.Pools[0];
        root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteDword(12, 1).WriteDword(16, 0)) });
        NativeBattleGastricJuice128Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [root] } };
        frame = fixture.Step(frame);
        Assert.Equal(4, frame.Effect.Pools.Length);
        Assert.Equal(new ushort[] { 20, 616, 80, 32 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 1, 8, 128 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Collection(fixture.Operations, operation => Assert.Equal(new NativeBattleGastricJuice128SnapshotBody(3, ArenaAddress + BodyCacheOffset), operation), operation => Assert.Equal(new NativeBattleGastricJuice128ActorFlags(3, 4, 0), operation));
        Assert.Equal((ushort)1, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal((ushort)99, frame.Effect.Pools[1].Slots[0].ReadWord(0x1C));
        Assert.Equal((byte)1, NativeBattleGastricJuice128Controller.Read(frame, RootBank).ReadByte(14));
        Assert.Equal(MainActAddress, frame.Effect.Pools[1].Slots[0].ReadDword(0x20));
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(PointsOffset + 6, 2)));
        fixture.Operations.Clear();
        frame = fixture.Step(frame);
        Assert.DoesNotContain(fixture.Operations, operation => operation is NativeBattleGastricJuice128SnapshotBody);
    }

    [Fact]
    public void Target_draws_ACT_then_sine_body_and_contacts_at_old_fifty_three()
    {
        Fixture fixture = new();
        NativeBattleGastricJuice128Frame frame = fixture.TargetPhase(53);
        frame = fixture.Step(frame);
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleGastricJuice128AdvanceAct>(operation), operation => Assert.Equal(new NativeBattleGastricJuice128ActorFlags(3, 0, 4), operation), operation => Assert.Equal(new NativeBattleGastricJuice128DrawBody132(3, unchecked((short)NativeBattleCameraMath.Sine(24 * 2048 / 40))), operation), operation => Assert.Equal(new NativeBattleGastricJuice128Contact(0), operation));
        Assert.Equal((ushort)54, frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Terminal_target_clears_hide_flag_after_real_ACT_service_and_releases_root()
    {
        Fixture fixture = new();
        NativeBattleGastricJuice128Frame frame = fixture.Step(fixture.TargetPhase(69));
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleGastricJuice128AdvanceAct>(operation), operation => Assert.Equal(new NativeBattleGastricJuice128ActorFlags(3, 4, 0), operation));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)69, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.All(frame.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
    }

    [Theory]
    [InlineData(0x6B8B20u, 0x1233538u, 9, 20)]
    [InlineData(0x6B8BC0u, 0x123322Cu, 13, 21)]
    [InlineData(0x6B9030u, 0x123338Cu, 16, 20)]
    public void Particle_writes_real_phase_and_advances_its_distinct_motion_after_draw(uint callback, uint source, int duration, int velocity)
    {
        Fixture fixture = new();
        NativeBattleGastricJuice128Frame frame = fixture.OnlyParticle(callback);
        uint address = ArenaAddress;
        frame = NativeBattleGastricJuice128Controller.Write(frame, address, NativeBattleGastricJuice128Controller.Read(frame, address).WriteDword(12, 0).WriteWord(0x10, 10).WriteWord(0x12, 20).WriteWord(0x14, 30).WriteWord(0x16, 1024).WriteWord(0x18, 5).WriteWord(0x1A, 20).WriteWord(0x1C, 7));
        frame = fixture.Step(frame);
        NativeBattleGastricJuice128DrawSprite180 draw = Assert.Single(fixture.Operations.OfType<NativeBattleGastricJuice128DrawSprite180>());
        Assert.Equal(source, draw.SourceAddress);
        Assert.Equal((ushort)0, draw.Phase);
        NativeBattleModulePoolTask task = NativeBattleGastricJuice128Controller.Read(frame, address);
        Assert.Equal(1u, task.ReadDword(12));
        Assert.Equal((ushort)velocity, task.ReadWord(0x1A));
        Assert.Equal((ushort)(callback == 0x6B9030 ? 10 : 15), task.ReadWord(0x10));
        frame = NativeBattleGastricJuice128Controller.Write(frame, address, task.WriteDword(12, (uint)(duration - 1)));
        frame = fixture.Step(frame);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Main_births_three_native_streams_and_eight_sprays_at_old_fourteen()
    {
        Fixture fixture = new();
        NativeBattleGastricJuice128Frame frame = fixture.TargetPhase(14);
        var actual = NativeBattleGastricJuice128Controller.AdvanceMain(frame, ArenaAddress + MainOffset, fixture.Resources, fixture.Inputs());
        Assert.Equal(3, actual.Frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(8, actual.Frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(14, fixture.BoneCalls.Count);
        Assert.Equal(3, fixture.BoneCalls.Count(call => call.Bone == 3 && call.Fraction == 4095));
        Assert.Equal(8, fixture.BoneCalls.Count(call => call.Bone == 20 && call.Fraction == 0));
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 35; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
        Assert.All(actual.Frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0), task =>
        {
            Assert.Equal((ushort)256, task.ReadWord(0x26));
            Assert.Equal((ushort)150, task.ReadWord(0x4C));
            Assert.Equal(new byte[8], actual.Frame.Memory.Arena!.Read((int)(task.ReadDword(0x10) - ArenaAddress) + 8, 8));
        });
    }

    [Fact]
    public void Root_wraps_tenth_age_without_reinitializing_actual_pools()
    {
        Fixture fixture = new();
        NativeBattleGastricJuice128Frame frame = NativeBattleGastricJuice128Controller.Write(fixture.Frame, RootBank, NativeBattleGastricJuice128Controller.Read(fixture.Frame, RootBank).WriteWord(12, 9));
        frame = fixture.Step(frame);
        Assert.Equal((ushort)0, NativeBattleGastricJuice128Controller.Read(frame, RootBank).ReadWord(12));
        Assert.DoesNotContain(fixture.Operations, operation => operation is NativeBattleGastricJuice128SnapshotBody);
    }

    private sealed class Fixture
    {
        public NativeBattleGastricJuice128Frame Frame { get; }
        public List<NativeBattleGastricJuice128Operation> Operations { get; } = [];
        public List<(short Bone, short Fraction)> BoneCalls { get; } = [];
        private readonly NativeBattleActionPacketsState packets;
        public NativeBattleGastricJuice128Resources Resources { get; }
        public Fixture()
        {
            packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, 128, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, _) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 2).WriteByte(15, 1)) });
            NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + 0x1280, 616, 1);
            (target, _) = target.Append(MainCallback);
            target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, target.Slots[0].WriteBytes(12, new byte[604]).WriteWord(12, 10).WriteDword(16, 0x1D97494)) });
            NativeBattleOriginalEffectState effect = new(128, 128, new(128, Entry, Initializer), new(0, 1, 8, 0, 0, 0), RootDescriptor) { Pools = [root, target, NativeBattleEffectTaskPool.Initialize(CurveDescriptor, ArenaAddress + 0x1000, 80, 8), NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, ArenaAddress, 32, 128)] };
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            for (int index = 0; index < PointCount; index++)
            {
                arena = arena.Write(PointsOffset + index * PointStride + 6, BitConverter.GetBytes((ushort)0xFFFF));
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(128) with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            Frame = new(memory, effect, new(1), NativeBattleGastricJuice128State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D972C0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            byte[] act = new byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(12), 20);
            Resources = new(new(MainActAddress, act.ToImmutableArray()), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new("fixture", []), 0x641DD, new byte[16384].ToImmutableArray());
        }
        public NativeBattleGastricJuice128Frame TargetPhase(ushort phase) => NativeBattleGastricJuice128Controller.Write(Frame, ArenaAddress + 0x1280, NativeBattleGastricJuice128Controller.Read(Frame, ArenaAddress + 0x1280).WriteWord(12, phase));
        public NativeBattleGastricJuice128Frame OnlyParticle(uint callback)
        {
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + 0x1280, 616, 1);
            NativeBattleEffectTaskPool particles = Frame.Effect.Pools[3];
            (particles, _) = particles.Append(callback);
            return Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(1, targets).SetItem(3, particles) } };
        }
        public NativeBattleGastricJuice128Frame Step(NativeBattleGastricJuice128Frame frame) => NativeBattleGastricJuice128Controller.Advance(frame, Resources, Inputs());

        public NativeBattleGastricJuice128Inputs Inputs() => new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, actor => new(actor, 2), _ => 30, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0), _ => throw new InvalidOperationException(), (current, _) => new(current, new(100, 200, 300, 99)), (current, _, bone, fraction) =>
        {
            BoneCalls.Add((bone, fraction));
            return new(current, new(200, 250, 400));
        }, (current, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleGastricJuice128DrawSprite180 draw)
            {
                Assert.Equal(draw.Phase, BitConverter.ToUInt16(current.Staging!.Read(draw.TemporaryAddress + 4, 2)));
            }
            return current;
        });
    }
}
