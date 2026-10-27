using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136ControllerTests
{
    private const uint WorkingAddress = 0x20DFAB8;
    private const uint MainAddress = WorkingAddress + MainOffset;

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_uses_old_toggle_without_initializing_at_age_zero(uint toggle, int offset, uint next)
    {
        Fixture f = new();
        NativeBattleOilShot136Frame frame = NativeBattleOilShot136Controller.Write(f.Frame, RootBank, NativeBattleOilShot136Controller.Read(f.Frame, RootBank).WriteDword(16, toggle));
        var actual = NativeBattleOilShot136Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(WorkingAddress + (uint)offset, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, NativeBattleOilShot136Controller.Read(actual.Frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, NativeBattleOilShot136Controller.Read(actual.Frame, RootBank).ReadWord(12));
    }

    [Theory]
    [InlineData(29, 0)]
    [InlineData(30, 1)]
    [InlineData(38, 1)]
    [InlineData(44, 2)]
    [InlineData(63, 2)]
    [InlineData(64, 1)]
    [InlineData(81, 1)]
    [InlineData(82, 0)]
    public void Main_ACT_windows_preserve_native_mode_depth_and_unknown_padding(int age, int count)
    {
        Fixture f = new();
        var actual = NativeBattleOilShot136Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(count, f.Acts.Count);
        Assert.Equal(age >= 82 ? 2 : 0, actual.Result);
        Assert.Equal((ushort)(age >= 82 ? age : age + 1), NativeBattleOilShot136Controller.Read(actual.Frame, MainAddress).ReadWord(12));
        Assert.All(f.Acts, act =>
        {
            Assert.Equal(WorkingAddress + MorphOffset, U32(act.Context.Read(40, 4)));
            Assert.Equal(act.Source == MainActAddress ? 0 : 1, I32(act.Context.Read(36, 4)));
            Assert.Equal(act.Source == ImpactActAddress ? 0 : -128, I32(act.Context.Read(32, 4)));
            Assert.Throws<NotSupportedException>(() => act.Context.Read(18, 2));
        });
    }

    [Fact]
    public void Main_moving_birth_consumes_six_CRT_and_full_pool_skips_all_six()
    {
        Fixture f = new();
        NativeBattleOilShot136Frame frame = f.Main(30);
        var actual = NativeBattleOilShot136Controller.AdvanceMain(NativeBattleTestSnapshots.Independent(frame), MainAddress, f.Resources, f.Inputs);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(1, actual.Frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleModulePoolTask moving = actual.Frame.Effect.Pools[2].Slots[0];
        Assert.InRange(moving.ReadWord(22), (ushort)2048, (ushort)4095);
        Assert.Equal((ushort)0, moving.ReadWord(30));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        for (int index = 0; index < MovingSpriteCount; index++)
        {
            (pool, _) = pool.Append(MovingSpriteCallback);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        actual = NativeBattleOilShot136Controller.AdvanceMain(frame, MainAddress, f.Resources, f.Inputs);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(30, 0)]
    [InlineData(33, 1350)]
    [InlineData(34, 1800)]
    [InlineData(69, 1800)]
    [InlineData(70, 1800)]
    [InlineData(81, 150)]
    [InlineData(82, 0)]
    public void Main_stage_envelope_and_terminal_clear_follow_old_age(int age, int depth)
    {
        Fixture f = new();
        NativeBattleOilShot136Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Contains(new NativeBattleOilShot136Stage(depth, 0xF0F0F0), f.Operations);
        Assert.Equal(age == 30, f.Operations.OfType<NativeBattleOilShot136Sound>().Any());
        if (age == 82)
        {
            Assert.Equal(new NativeBattleOilShot136Stage(0, 0), f.Operations.Last());
        }
    }

    [Fact]
    public void Main_contact_uses_only_first_relative_contact_at_old_age_62()
    {
        Fixture f = new();
        NativeBattleOilShot136Controller.AdvanceMain(f.Main(62), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(new NativeBattleOilShot136Contact(0), Assert.Single(f.Operations.OfType<NativeBattleOilShot136Contact>()));
    }

    [Theory]
    [InlineData(false, 14, 0)]
    [InlineData(false, 15, 2)]
    [InlineData(true, 7, 0)]
    [InlineData(true, 8, 2)]
    public void Sprites_release_then_move_and_increment_signed_phase(bool moving, int age, int result)
    {
        Fixture f = new();
        var appended = NativeBattleOilShot136Controller.Append(f.Frame, moving ? 2 : 4, moving ? MovingSpriteCallback : SimpleSpriteCallback);
        uint address = appended.Address;
        NativeBattleModulePoolTask task = NativeBattleOilShot136Controller.Read(appended.Frame, address).WriteBytes(12, new byte[moving ? 20 : 8]);
        task = moving ? task.WriteWord(12, 100).WriteWord(14, unchecked((ushort)-7)).WriteWord(16, 50).WriteWord(18, 60).WriteWord(20, 2000).WriteWord(22, 1024).WriteWord(24, 4).WriteWord(26, unchecked((ushort)-8)).WriteWord(28, 12).WriteWord(30, (ushort)age) : task.WriteWord(12, 50).WriteWord(14, 60).WriteWord(16, 2000).WriteWord(18, (ushort)age);
        NativeBattleOilShot136Frame frame = NativeBattleOilShot136Controller.Write(appended.Frame, address, task);
        f.DrawCheck = current =>
        {
            var draw = Assert.IsType<NativeBattleOilShot136DrawSprite180>(f.Operations.Last());
            Assert.Equal(moving ? SpriteAAddress : SpriteBAddress, draw.SourceAddress);
            Assert.Equal((ushort)age, BinaryPrimitives.ReadUInt16LittleEndian(current.Staging!.Read(draw.TemporaryAddress + 4, 2)));
            Assert.Throws<NotSupportedException>(() => current.Staging.Read(draw.TemporaryAddress + 24, 2));
        };
        var actual = moving ? NativeBattleOilShot136Controller.AdvanceMovingSprite(frame, address, f.Resources, f.Inputs) : NativeBattleOilShot136Controller.AdvanceSimpleSprite(frame, address, f.Resources, f.Inputs);
        Assert.Equal(result, actual.Result);
        task = NativeBattleOilShot136Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)(age + 1), task.ReadWord(moving ? 30 : 18));
        if (moving)
        {
            Assert.Equal(new ushort[] { 54, 52, 2012 }, new[] { task.ReadWord(16), task.ReadWord(18), task.ReadWord(20) });
            Assert.Equal((ushort)93, task.ReadWord(12));
        }
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACT_children_stop_before_motion_when_actual_remaining_reaches_zero(bool large)
    {
        Fixture f = new() { Remaining = 0 };
        var appended = NativeBattleOilShot136Controller.Append(f.Frame, large ? 5 : 3, large ? LargeParticleCallback : EmitterCallback);
        NativeBattleModulePoolTask task = NativeBattleOilShot136Controller.Read(appended.Frame, appended.Address).WriteBytes(12, new byte[16]).WriteWord(12, 10).WriteWord(14, 20).WriteWord(16, 30).WriteWord(18, 1).WriteWord(20, 100).WriteWord(22, 100).WriteWord(24, 100);
        NativeBattleOilShot136Frame frame = NativeBattleOilShot136Controller.Write(appended.Frame, appended.Address, task);
        var actual = large ? NativeBattleOilShot136Controller.AdvanceLargeParticle(frame, appended.Address, f.Resources, f.Inputs) : NativeBattleOilShot136Controller.AdvanceEmitter(frame, appended.Address, f.Resources, f.Inputs);
        Assert.Equal(2, actual.Result);
        Assert.Equal(task.ReadBytes(12, 16), NativeBattleOilShot136Controller.Read(actual.Frame, appended.Address).ReadBytes(12, 16));
        Assert.Equal(large ? 1 : 2, I32(Assert.Single(f.Acts).Context.Read(36, 4)));
    }

    [Theory]
    [InlineData(41, 0, 0)]
    [InlineData(42, 1, 1)]
    [InlineData(43, 0, 1)]
    [InlineData(52, 1, 1)]
    [InlineData(53, 0, 1)]
    [InlineData(54, 0, 1)]
    [InlineData(57, 0, 1)]
    [InlineData(58, 0, 0)]
    public void Main_large_and_simple_births_follow_distinct_impact_windows(int age, int large, int simple)
    {
        Fixture f = new();
        var actual = NativeBattleOilShot136Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(large, actual.Frame.Effect.Pools[5].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(simple, actual.Frame.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        NativeCrtRandomState expected = f.Frame.Random;
        int draws = (large + simple) * 3 + (age <= 42 && age > 30 ? 1 : 0);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
    }

    [Fact]
    public void Root_completion_draws_ignored_children_then_detaches_their_raw_flags()
    {
        Fixture f = new();
        NativeBattleOilShot136Frame frame = f.Main(82);
        frame = NativeBattleOilShot136Controller.Write(frame, RootBank, NativeBattleOilShot136Controller.Read(frame, RootBank).WriteWord(12, 10).WriteWord(14, 1));
        (frame, uint address) = NativeBattleOilShot136Controller.Append(frame, 2, MovingSpriteCallback);
        frame = NativeBattleOilShot136Controller.Write(frame, address, NativeBattleOilShot136Controller.Read(frame, address).WriteBytes(12, new byte[20]).WriteWord(20, 2000).WriteWord(22, 1024));
        frame = NativeBattleOilShot136Controller.Advance(frame, f.Resources, f.Inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, frame.Effect.Pools.Length);
        Assert.Contains(f.Operations, operation => operation is NativeBattleOilShot136DrawSprite180);
        byte[] retained = frame.Memory.Arena!.Read(MovingSpriteOffset, MovingSpriteStride);
        Assert.Equal((ushort)1, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(retained) & 1));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(retained.AsSpan(30)));
        Assert.Equal((ushort)10, NativeBattleOilShot136Controller.Read(frame, RootBank).ReadWord(12));
    }

    [Fact]
    public void Large_falling_phase_applies_WORD_gravity_then_initializes_impact_at_ground()
    {
        Fixture f = new() { Remaining = 0 };
        var appended = NativeBattleOilShot136Controller.Append(f.Frame, 5, LargeParticleCallback);
        NativeBattleModulePoolTask task = NativeBattleOilShot136Controller.Read(appended.Frame, appended.Address).WriteBytes(12, new byte[16]).WriteWord(12, 10).WriteWord(14, unchecked((ushort)-1)).WriteWord(16, 30).WriteWord(18, 0).WriteWord(20, 2).WriteWord(22, 0).WriteWord(24, unchecked((ushort)-3));
        var actual = NativeBattleOilShot136Controller.AdvanceLargeParticle(NativeBattleOilShot136Controller.Write(appended.Frame, appended.Address, task), appended.Address, f.Resources, f.Inputs);
        Assert.Equal(0, actual.Result);
        task = NativeBattleOilShot136Controller.Read(actual.Frame, appended.Address);
        Assert.Equal(new ushort[] { 12, 0, 27, 1 }, new[] { task.ReadWord(12), task.ReadWord(14), task.ReadWord(16), task.ReadWord(18) });
        Assert.Equal((ushort)16, task.ReadWord(22));
        Assert.Equal(LargeImpactActAddress, task.ReadDword(28));
        Assert.Equal(2, I32(Assert.Single(f.Acts).Context.Read(36, 4)));
    }

    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static int I32(byte[] bytes) => BinaryPrimitives.ReadInt32LittleEndian(bytes);

    private sealed class Fixture
    {
        public NativeBattleOilShot136Frame Frame { get; }
        public NativeBattleOilShot136Resources Resources { get; }
        public NativeBattleOilShot136Inputs Inputs { get; }
        public List<(uint Source, NativeBattleEffectArena Context)> Acts { get; } = [];
        public List<NativeBattleOilShot136Operation> Operations { get; } = [];
        public Action<NativeBattleOilShot136Frame>? DrawCheck { get; set; }
        public int Remaining { get; set; } = 1;
        public Fixture()
        {
            Resources = new(new(MainActAddress, new byte[8].ToImmutableArray()), new(SecondaryActAddress, new byte[8].ToImmutableArray()), new(ImpactActAddress, new byte[8].ToImmutableArray()), new(EmitterActAddress, new byte[8].ToImmutableArray()), new(LargeParticleActAddress, new byte[8].ToImmutableArray()), new(LargeImpactActAddress, new byte[8].ToImmutableArray()), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, MeshAddress, []), new(0, MatrixAddress, 0, 0, []), new("unused", []), 0x641E0, [], new int[2049].ToImmutableArray());
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(MovingSpriteDescriptor, WorkingAddress + MovingSpriteOffset, MovingSpriteStride, MovingSpriteCount),
                NativeBattleEffectTaskPool.Initialize(EmitterDescriptor, WorkingAddress + EmitterOffset, EmitterStride, EmitterCount),
                NativeBattleEffectTaskPool.Initialize(SimpleSpriteDescriptor, WorkingAddress + SimpleSpriteOffset, SimpleSpriteStride, SimpleSpriteCount),
                NativeBattleEffectTaskPool.Initialize(LargeParticleDescriptor, WorkingAddress, LargeParticleStride, LargeParticleCount)
            ];
            (pools[0], _) = pools[0].Append(RootCallback);
            pools[0] = NativeBattleTaskFixture.EditPool(pools[0], data => data with { Slots = pools[0].Slots.SetItem(0, pools[0].Slots[0].WriteDword(12, 0).WriteDword(16, 0)) });
            (pools[1], _) = pools[1].Append(MainCallback);
            pools[1] = NativeBattleTaskFixture.EditPool(pools[1], data => data with { Slots = pools[1].Slots.SetItem(0, pools[1].Slots[0].WriteBytes(12, new byte[1120]).WriteWord(28, 500).WriteWord(30, 600).WriteWord(32, 2000)) });
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            NativeBattleOriginalEffectState effect = new(136, 135, new(136, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            byte[] mesh = new byte[MeshBytes];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 0x368);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 108);
            NativeBattleOilShot136State state = NativeBattleOilShot136State.FromColdProcess().WriteDword(WorkingGlobal, WorkingAddress).WriteDword(CasterGlobal, 0x1D972C0) with { Mesh = mesh.ToImmutableArray(), ScaleMatrix32 = NativeBattleOilShot136Controller.PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0)), PendingColdSources = false };
            Frame = new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools), effect, new(55), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => throw new InvalidOperationException(), current => new(current with { State = current.State.WriteDword(CursorGlobal, 77) }, new(100, 200, 300)), _ => 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 }, null), (current, _, source, _, _, context) =>
            {
                Acts.Add((source.Address, context));
                return new(current with { State = current.State.WriteDword(CursorGlobal, current.State.ReadDword(CursorGlobal) + 1) }, Remaining);
            }, (current, operation) =>
            {
                Operations.Add(operation);
                DrawCheck?.Invoke(current);
                return current with { State = current.State.WriteDword(CursorGlobal, current.State.ReadDword(CursorGlobal) + 1) };
            });
        }
        public NativeBattleOilShot136Frame Main(int age) => NativeBattleOilShot136Controller.Write(Frame, MainAddress, NativeBattleOilShot136Controller.Read(Frame, MainAddress).WriteWord(12, (ushort)age));
    }
}
