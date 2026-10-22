using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSaliva138ControllerTests
{
    private const uint WorkingAddress = 0x20DFAB8;
    private const uint MainAddress = WorkingAddress + MainOffset;

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_uses_old_toggle_and_does_not_initialize_at_age_zero(uint toggle, int offset, uint next)
    {
        Fixture f = new();
        NativeBattleSaliva138Frame frame = NativeBattleSaliva138Controller.Write(f.Frame, RootBank, NativeBattleSaliva138Controller.Read(f.Frame, RootBank).WriteDword(16, toggle));
        var actual = NativeBattleSaliva138Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(WorkingAddress + (uint)offset, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, NativeBattleSaliva138Controller.Read(actual.Frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, NativeBattleSaliva138Controller.Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Empty(f.Acts);
    }

    [Theory]
    [InlineData(22, 0)]
    [InlineData(23, 2)]
    [InlineData(28, 2)]
    [InlineData(29, 3)]
    [InlineData(36, 3)]
    [InlineData(37, 1)]
    [InlineData(54, 1)]
    [InlineData(55, 0)]
    public void Main_ACT_windows_depth_morph_and_local_padding_follow_actual_producers(int age, int count)
    {
        Fixture f = new();
        var actual = NativeBattleSaliva138Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(count, f.Acts.Count);
        Assert.Equal(age >= 55 ? 2 : 0, actual.Result);
        Assert.Equal((ushort)(age >= 55 ? age : age + 1), NativeBattleSaliva138Controller.Read(actual.Frame, MainAddress).ReadWord(12));
        Assert.All(f.Acts, act => Assert.Equal(WorkingAddress + MorphOffset, U32(act.Context.Read(36, 4))));
        foreach (var act in f.Acts)
        {
            Assert.Equal(act.Source == ImpactActAddress ? 0 : -128, I32(act.Context.Read(32, 4)));
            if (age <= 36)
            {
                Assert.Equal(new byte[2], act.Context.Read(18, 2));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => act.Context.Read(18, 2));
            }
        }
        if (age == 55)
        {
            Assert.Equal(f.Frame.Random, actual.Frame.Random);
        }
    }

    [Fact]
    public void Main_successful_moving_births_consume_six_CRT_and_one_GPF_each_then_curve_CRT()
    {
        Fixture f = new();
        NativeBattleSaliva138Frame frame = f.Main(23);
        var actual = NativeBattleSaliva138Controller.AdvanceMain(frame, MainAddress, f.Resources, f.Inputs);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 13; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(2, actual.Frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(1, actual.Frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleModulePoolTask first = actual.Frame.Effect.Pools[3].Slots[0];
        Assert.Equal((ushort)0, first.ReadWord(30));
        Assert.Equal(new ushort[] { 100, 200, 300 }, new[] { first.ReadWord(16), first.ReadWord(18), first.ReadWord(20) });
        Assert.InRange(first.ReadWord(22), (ushort)1024, (ushort)2047);
        Assert.Equal(77u + 2, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(110, I32(f.Acts[0].Context.Read(20, 4)));
        Assert.Equal(220, I32(f.Acts[0].Context.Read(24, 4)));
        Assert.Equal(330, I32(f.Acts[0].Context.Read(28, 4)));
        Assert.NotNull(actual.Frame.Memory.ScriptedColorRegisters!.Mac[0]);
    }

    [Fact]
    public void Main_full_moving_pool_skips_all_birth_RNG_but_keeps_native_curve_birth()
    {
        Fixture f = new();
        NativeBattleSaliva138Frame frame = f.Main(23);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[3];
        for (int index = 0; index < MovingSpriteCount; index++)
        {
            (pool, _) = pool.Append(MovingSpriteCallback);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(3, pool) } };
        var actual = NativeBattleSaliva138Controller.AdvanceMain(frame, MainAddress, f.Resources, f.Inputs);
        Assert.Equal(frame.Random.Draw(out _), actual.Frame.Random);
    }

    [Fact]
    public void Main_first_contact_only_and_sound_use_literal_ages_and_preserve_service_feedback()
    {
        Fixture f = new();
        NativeBattleSaliva138Controller.AdvanceMain(f.Main(25), MainAddress, f.Resources, f.Inputs);
        Assert.Contains(new NativeBattleSaliva138Sound(0x641B5, 0, 128), f.Operations);
        f.Operations.Clear();
        f.Acts.Clear();
        var actual = NativeBattleSaliva138Controller.AdvanceMain(f.Main(40), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(new NativeBattleSaliva138Contact(0), Assert.Single(f.Operations));
        Assert.Equal(1u, actual.Frame.State.ReadDword(CursorGlobal) - (uint)f.Acts.Count);
    }

    [Theory]
    [InlineData(33, 1)]
    [InlineData(34, 0)]
    [InlineData(35, 1)]
    [InlineData(39, 1)]
    [InlineData(41, 0)]
    public void Main_simple_birth_uses_even_impact_phase_with_three_CRT_and_overwrites_phase_W(int age, int births)
    {
        Fixture f = new();
        var actual = NativeBattleSaliva138Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(births, actual.Frame.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        NativeCrtRandomState expected = f.Frame.Random;
        for (int index = 0; index < births * 3; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
        if (births != 0)
        {
            Assert.Equal((ushort)0, actual.Frame.Effect.Pools[4].Slots[0].ReadWord(18));
        }
    }

    [Theory]
    [InlineData(false, 11, 0)]
    [InlineData(false, 12, 2)]
    [InlineData(true, 8, 0)]
    [InlineData(true, 9, 2)]
    public void Sprite_prepares_before_draw_releases_then_moves_and_increments_signed_phase(bool moving, int age, int result)
    {
        Fixture f = new();
        int poolIndex = moving ? 3 : 4;
        var appended = NativeBattleSaliva138Controller.Append(f.Frame, poolIndex, moving ? MovingSpriteCallback : SimpleSpriteCallback);
        uint address = appended.Address;
        NativeBattleModulePoolTask task = NativeBattleSaliva138Controller.Read(appended.Frame, address).WriteBytes(12, new byte[moving ? 20 : 8]);
        task = moving ? task.WriteWord(12, 100).WriteWord(14, unchecked((ushort)-7)).WriteWord(16, 50).WriteWord(18, 60).WriteWord(20, 2000).WriteWord(22, 1024).WriteWord(24, 4).WriteWord(26, unchecked((ushort)-8)).WriteWord(28, 12).WriteWord(30, (ushort)age) : task.WriteWord(12, 50).WriteWord(14, 60).WriteWord(16, 2000).WriteWord(18, (ushort)age);
        NativeBattleSaliva138Frame frame = NativeBattleSaliva138Controller.Write(appended.Frame, address, task);
        f.DrawCheck = current =>
        {
            var draw = Assert.IsType<NativeBattleSaliva138DrawSprite180>(f.Operations.Last());
            Assert.Equal(moving ? SpriteAAddress : SpriteBAddress, draw.SourceAddress);
            Assert.Equal((ushort)age, BinaryPrimitives.ReadUInt16LittleEndian(current.Staging!.Read(draw.TemporaryAddress + 4, 2)));
            Assert.Equal((ushort)(moving ? 1 : 0), BinaryPrimitives.ReadUInt16LittleEndian(current.Staging.Read(draw.TemporaryAddress + 0x24, 2)));
            Assert.Throws<NotSupportedException>(() => current.Staging.Read(draw.TemporaryAddress + 24, 2));
            Assert.Equal((ushort)(moving ? 60 : age), NativeBattleSaliva138Controller.Read(current, address).ReadWord(18));
        };
        var actual = moving ? NativeBattleSaliva138Controller.AdvanceMovingSprite(frame, address, f.Resources, f.Inputs) : NativeBattleSaliva138Controller.AdvanceSimpleSprite(frame, address, f.Resources, f.Inputs);
        Assert.Equal(result, actual.Result);
        task = NativeBattleSaliva138Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)(age + 1), task.ReadWord(moving ? 30 : 18));
        if (moving)
        {
            Assert.Equal(new ushort[] { 54, 52, 2012 }, new[] { task.ReadWord(16), task.ReadWord(18), task.ReadWord(20) });
            Assert.Equal((ushort)93, task.ReadWord(12));
        }
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Root_completion_ignores_surviving_moving_pool_after_drawing_it()
    {
        Fixture f = new();
        NativeBattleSaliva138Frame frame = f.Frame with { Effect = f.Frame.Effect with { Pools = f.Frame.Effect.Pools.SetItem(1, f.Frame.Effect.Pools[1] with { Head = null, Tail = null, Slots = f.Frame.Effect.Pools[1].Slots.Select(task => task with { Flags = 0 }).ToImmutableArray() }) } };
        frame = NativeBattleSaliva138Controller.Write(frame, RootBank, NativeBattleSaliva138Controller.Read(frame, RootBank).WriteWord(12, 10).WriteWord(14, 1));
        (frame, uint address) = NativeBattleSaliva138Controller.Append(frame, 3, MovingSpriteCallback);
        frame = NativeBattleSaliva138Controller.Write(frame, address, NativeBattleSaliva138Controller.Read(frame, address).WriteBytes(12, new byte[20]).WriteWord(20, 2000).WriteWord(22, 1024));
        var actual = NativeBattleSaliva138Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)10, NativeBattleSaliva138Controller.Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)1, NativeBattleSaliva138Controller.Read(actual.Frame, address).ReadWord(30));
        Assert.Equal(1, actual.Frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.IsType<NativeBattleSaliva138DrawSprite180>(Assert.Single(f.Operations));
    }

    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static int I32(byte[] bytes) => BinaryPrimitives.ReadInt32LittleEndian(bytes);

    private sealed class Fixture
    {
        public NativeBattleSaliva138Frame Frame { get; }
        public NativeBattleSaliva138Resources Resources { get; }
        public NativeBattleSaliva138Inputs Inputs { get; }
        public List<(uint Source, NativeBattleEffectArena Context)> Acts { get; } = [];
        public List<NativeBattleSaliva138Operation> Operations { get; } = [];
        public Action<NativeBattleSaliva138Frame>? DrawCheck { get; set; }
        public Fixture()
        {
            Resources = new(new(MainActAddress, []), new(SecondaryActAddress, []), new(ImpactActAddress, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, MeshAddress, []), new(0, MatrixAddress, 0, 0, []), new("unused", []), 0x641B5, [], [], [], []);
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(CurveDescriptor, WorkingAddress + CurveOffset, CurveStride, CurveCount),
                NativeBattleEffectTaskPool.Initialize(MovingSpriteDescriptor, WorkingAddress + MovingSpriteOffset, MovingSpriteStride, MovingSpriteCount),
                NativeBattleEffectTaskPool.Initialize(SimpleSpriteDescriptor, WorkingAddress, SimpleSpriteStride, SimpleSpriteCount)
            ];
            (pools[0], _) = pools[0].Append(RootCallback);
            pools[0] = pools[0] with { Slots = pools[0].Slots.SetItem(0, pools[0].Slots[0].WriteDword(12, 0).WriteDword(16, 0)) };
            (pools[1], _) = pools[1].Append(MainCallback);
            pools[1] = pools[1] with { Slots = pools[1].Slots.SetItem(0, pools[1].Slots[0].WriteBytes(12, new byte[884]).WriteWord(28, 500).WriteWord(30, 600).WriteWord(32, 2000)) };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            NativeBattleOriginalEffectState effect = new(138, 137, new(138, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            Frame = new(memory, effect, new(55), NativeBattleSaliva138State.FromColdProcess().WriteDword(WorkingGlobal, WorkingAddress).WriteDword(CasterGlobal, 0x1D972C0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _, _, _) => throw new InvalidOperationException(), current => new(current with { State = current.State.WriteDword(CursorGlobal, 77) }, new(100, 200, 300)), _ => 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 }, null), _ => throw new InvalidOperationException(), (current, _, source, _, _, context) =>
            {
                Acts.Add((source.Address, context));
                return new(current with { State = current.State.WriteDword(CursorGlobal, current.State.ReadDword(CursorGlobal) + 1) }, 1);
            }, (current, operation) =>
            {
                Operations.Add(operation);
                DrawCheck?.Invoke(current);
                return current with { State = current.State.WriteDword(CursorGlobal, current.State.ReadDword(CursorGlobal) + 1) };
            });
        }
        public NativeBattleSaliva138Frame Main(int age) => NativeBattleSaliva138Controller.Write(Frame, MainAddress, NativeBattleSaliva138Controller.Read(Frame, MainAddress).WriteWord(12, (ushort)age));
    }
}
