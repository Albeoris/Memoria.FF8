using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDisolvingAcid132ControllerTests
{
    private const uint WorkingAddress = 0x20DFAB8;
    private const uint MainAddress = WorkingAddress + MainOffset;

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_old_toggle_selects_actual_packet_bank_without_initializing_at_age_zero(uint toggle, int offset, uint next)
    {
        Fixture f = new();
        NativeBattleDisolvingAcid132Frame frame = NativeBattleDisolvingAcid132Controller.Write(f.Frame, RootBank, NativeBattleDisolvingAcid132Controller.Read(f.Frame, RootBank).WriteDword(16, toggle));
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(WorkingAddress + (uint)offset, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, NativeBattleDisolvingAcid132Controller.Read(actual.Frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, NativeBattleDisolvingAcid132Controller.Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Empty(f.Operations);
    }

    [Theory]
    [InlineData(9, false, false)]
    [InlineData(10, true, false)]
    [InlineData(11, false, false)]
    [InlineData(44, false, false)]
    [InlineData(45, false, true)]
    [InlineData(46, false, true)]
    public void Main_sound_and_first_contact_follow_old_ages_without_random_draws(int age, bool sound, bool contact)
    {
        Fixture f = new();
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(sound, f.Operations.Contains(new NativeBattleDisolvingAcid132Sound(0x6419A, 0, 128)));
        Assert.Equal(contact, f.Operations.Contains(new NativeBattleDisolvingAcid132Contact(0)));
        Assert.Equal(contact ? 2 : 0, actual.Result);
        Assert.Equal((ushort)(contact ? age : age + 1), NativeBattleDisolvingAcid132Controller.Read(actual.Frame, MainAddress).ReadWord(12));
        Assert.Equal(f.Frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(21)]
    [InlineData(23)]
    [InlineData(29)]
    [InlineData(30)]
    public void Main_only_even_ages_in_20_to_28_reach_emission_front(int age)
    {
        Fixture f = new();
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceMain(f.Main(age), MainAddress, f.Resources, f.Inputs);
        Assert.Equal(f.Frame.Random, actual.Frame.Random);
        Assert.Empty(f.Operations);
    }

    [Theory]
    [InlineData(20, 0)]
    [InlineData(22, 1)]
    [InlineData(24, 2)]
    [InlineData(26, 3)]
    [InlineData(28, 4)]
    public void Main_even_emissions_select_original_row_and_one_recipient_bone_random_draw(int age, int row)
    {
        Fixture f = new();
        byte[] vectors = new byte[80];
        for (int index = 0; index < 5; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 16), (short)(100 + index));
            BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 16 + 10), 32);
        }
        NativeBattleDisolvingAcid132Resources resources = f.Resources with { EmissionVectors = vectors.ToImmutableArray() };
        byte? observedSelector = null;
        NativeBattleDisolvingAcid132Inputs inputs = f.Inputs with
        {
            ReadEmitter6B2B70 = (current, source) =>
            {
                Assert.Equal((short)(100 + row), BinaryPrimitives.ReadInt16LittleEndian(source.Read(0, 2)));
                return new(current, new(100, 200, 300));
            },
            ReadModelMatrix = (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null),
            ReadModelBoneCount = _ => 9,
            ReadBone502170 = (current, _, selector, fraction) =>
            {
                Assert.Equal((short)0, fraction);
                observedSelector = selector;
                return new(current, new(500, 600, 700));
            }
        };
        NativeBattleDisolvingAcid132Frame frame = f.Main(age);
        frame = NativeBattleDisolvingAcid132Controller.WriteArena(frame, WorkingAddress + PointsOffset + 6, BitConverter.GetBytes(ushort.MaxValue));
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceMain(frame, MainAddress, resources, inputs);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort random);
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal((byte)((random * 9) >> 15), observedSelector);
        NativeBattleModulePoolTask curve = Assert.Single(actual.Frame.Effect.Pools[4].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)150, curve.ReadWord(58));
        Assert.Equal((ushort)232, curve.ReadWord(34));
        Assert.Equal((ushort)1, curve.ReadWord(46));
        Assert.Equal(new byte[80], f.Resources.EmissionVectors.ToArray());
    }

    [Fact]
    public void CreatePrimaryAct_odd_CRT_selects_original_primary_source_and_consumes_one_draw()
    {
        Fixture f = new();
        NativeBattleDisolvingAcid132Frame frame = f.Frame with { Random = new(1) };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort value);
        Assert.Equal(1, value & 1);
        NativeBattleDisolvingAcid132Frame actual = NativeBattleDisolvingAcid132Controller.CreatePrimaryAct(frame, NativeBattleDisolvingAcid132Controller.PointView(new(10, 20, 30)), f.Resources);
        NativeBattleModulePoolTask task = Assert.Single(actual.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal(PrimaryActAddress, task.ReadDword(0x18));
        Assert.Equal(PrimaryActCallback, task.Callback);
        Assert.Equal(expected, actual.Random);
    }

    [Fact]
    public void CreatePrimaryAct_even_CRT_selects_original_secondary_source_and_consumes_one_draw()
    {
        Fixture f = new();
        NativeBattleDisolvingAcid132Frame frame = f.Frame with { Random = new(0) };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort value);
        Assert.Equal(0, value & 1);
        NativeBattleDisolvingAcid132Frame actual = NativeBattleDisolvingAcid132Controller.CreatePrimaryAct(frame, NativeBattleDisolvingAcid132Controller.PointView(new(10, 20, 30)), f.Resources);
        NativeBattleModulePoolTask task = Assert.Single(actual.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal(SecondaryActAddress, task.ReadDword(0x18));
        Assert.Equal(PrimaryActCallback, task.Callback);
        Assert.Equal(expected, actual.Random);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 1)]
    [InlineData(15, 1)]
    [InlineData(16, 0)]
    public void Primary_ACT_ignores_remaining_and_draws_two_overlapping_windows_before_signed_lifetime(int age, int draws)
    {
        Fixture f = new() { Remaining = 0 };
        NativeBattleDisolvingAcid132Frame frame = f.Act(age, false, out uint address);
        var actual = NativeBattleDisolvingAcid132Controller.AdvancePrimaryAct(frame, address, f.Resources, f.Inputs);
        Assert.Equal(draws, f.Operations.Count);
        Assert.All(f.Operations, operation => Assert.IsType<NativeBattleDisolvingAcid132DrawSprite180>(operation));
        Assert.Equal(age >= 15 ? 2 : 0, actual.Result);
        var act = Assert.Single(f.Acts);
        Assert.Equal(WorkingAddress + MorphOffset, U32(act.Context.Read(8, 4)));
        Assert.Equal(new byte[] { 10, 0, 20, 0, 208, 7, 0, 0 }, act.Context.Read(0, 8));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public void Impact_ACT_completion_uses_actual_remaining_without_incrementing_age(int remaining, int result)
    {
        Fixture f = new() { Remaining = remaining };
        NativeBattleDisolvingAcid132Frame frame = f.Act(7, true, out uint address);
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceImpactAct(frame, address, f.Resources, f.Inputs);
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)7, NativeBattleDisolvingAcid132Controller.Read(actual.Frame, address).ReadWord(12));
        Assert.Empty(f.Operations);
    }

    [Theory]
    [InlineData(6, 1)]
    [InlineData(7, 0)]
    public void Custom_sprite_scheduler_reuses_one_initialized_template_then_draws_before_lifetime_and_motion(int age, int survivors)
    {
        Fixture f = new();
        var appended = NativeBattleDisolvingAcid132Controller.Append(f.Frame, 5, SpriteCallback);
        uint address = appended.Address;
        NativeBattleModulePoolTask task = NativeBattleDisolvingAcid132Controller.Read(appended.Frame, address).WriteBytes(12, new byte[16]).WriteWord(12, 10).WriteWord(14, 20).WriteWord(16, 2000).WriteWord(18, (ushort)age).WriteWord(20, 2).WriteWord(22, unchecked((ushort)-4)).WriteWord(24, 6);
        NativeBattleDisolvingAcid132Frame frame = NativeBattleDisolvingAcid132Controller.Write(appended.Frame, address, task);
        f.DrawCheck = current =>
        {
            var draw = Assert.IsType<NativeBattleDisolvingAcid132DrawTemplate180>(f.Operations.Last());
            uint temporary = draw.TemporaryAddress;
            Assert.Equal((ushort)age, NativeBattleDisolvingAcid132Controller.Read(current, address).ReadWord(18));
            Assert.Equal(1024, BinaryPrimitives.ReadInt16LittleEndian(current.Staging!.Read(temporary + 0x84, 2)));
            foreach (uint offset in new uint[] { 0x48, 0x50, 0x58, 0x60 })
            {
                Assert.Equal(new byte[2], current.Staging.Read(temporary + offset, 2));
                Assert.Throws<NotSupportedException>(() => current.Staging.Read(temporary + offset + 2, 2));
            }
            Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(current.Staging.Read(temporary + 0x98, 4)));
            Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(current.Staging.Read(temporary + 0x9C, 4)));
            Assert.Equal(2030, BinaryPrimitives.ReadInt32LittleEndian(current.Staging.Read(temporary + 0xA0, 4)));
        };
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceSpriteScheduler(frame, f.Resources, f.Inputs);
        Assert.Equal(survivors, actual.Survivors);
        task = NativeBattleDisolvingAcid132Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)(age + 1), task.ReadWord(18));
        Assert.Equal((ushort)(survivors == 0 ? 10 : 12), task.ReadWord(12));
        Assert.Equal((ushort)(survivors == 0 ? 20 : 16), task.ReadWord(14));
        Assert.Equal((ushort)(survivors == 0 ? 2000 : 2006), task.ReadWord(16));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Root_waits_for_custom_sprite_scheduler_after_other_actual_pools_drain()
    {
        Fixture f = new();
        NativeBattleDisolvingAcid132Frame frame = f.Main(45);
        frame = NativeBattleDisolvingAcid132Controller.Write(frame, RootBank, NativeBattleDisolvingAcid132Controller.Read(frame, RootBank).WriteWord(12, 10).WriteBytes(15, [1]));
        (frame, uint address) = NativeBattleDisolvingAcid132Controller.Append(frame, 5, SpriteCallback);
        frame = NativeBattleDisolvingAcid132Controller.Write(frame, address, NativeBattleDisolvingAcid132Controller.Read(frame, address).WriteBytes(12, new byte[16]).WriteWord(16, 2000));
        var actual = NativeBattleDisolvingAcid132Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)1, NativeBattleDisolvingAcid132Controller.Read(actual.Frame, address).ReadWord(18));
        frame = NativeBattleDisolvingAcid132Controller.Write(actual.Frame, address, NativeBattleDisolvingAcid132Controller.Read(actual.Frame, address).WriteWord(18, 7));
        actual = NativeBattleDisolvingAcid132Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)0, NativeBattleDisolvingAcid132Controller.Read(actual.Frame, address).Flags);
    }

    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);

    private sealed class Fixture
    {
        public NativeBattleDisolvingAcid132Frame Frame { get; }
        public NativeBattleDisolvingAcid132Resources Resources { get; }
        public NativeBattleDisolvingAcid132Inputs Inputs { get; }
        public List<(uint Source, NativeBattleEffectArena Context)> Acts { get; } = [];
        public List<NativeBattleDisolvingAcid132Operation> Operations { get; } = [];
        public Action<NativeBattleDisolvingAcid132Frame>? DrawCheck { get; set; }
        public int Remaining { get; set; } = 1;

        public Fixture()
        {
            byte[] template = new byte[128];
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(8), 8);
            for (int phase = 0; phase < 8; phase++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(10 + phase * 2), (ushort)(64 + phase * 4));
            }
            Resources = new(new(PrimaryActAddress, new byte[8].ToImmutableArray()), new(SecondaryActAddress, new byte[8].ToImmutableArray()), new(ImpactActAddress, new byte[8].ToImmutableArray()), new(0, TemplateSpriteAddress, 0, 0, template.ToImmutableArray()), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new("unused", []), 0x6419A, new byte[80].ToImmutableArray(), [], []);
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, MainCount),
                NativeBattleEffectTaskPool.Initialize(ActDescriptor, WorkingAddress + ActOffset, ActStride, ActCount),
                NativeBattleEffectTaskPool.Initialize(SimpleSpriteDescriptor, WorkingAddress + SimpleSpriteOffset, SimpleSpriteStride, SimpleSpriteCount),
                NativeBattleEffectTaskPool.Initialize(CurveDescriptor, WorkingAddress + CurveOffset, CurveStride, CurveCount),
                NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, WorkingAddress, SpriteStride, SpriteCount)
            ];
            (pools[0], _) = pools[0].Append(RootCallback);
            pools[0] = pools[0] with { Slots = pools[0].Slots.SetItem(0, pools[0].Slots[0].WriteDword(12, 0).WriteDword(16, 0)) };
            (pools[1], _) = pools[1].Append(MainCallback);
            pools[1] = pools[1] with { Slots = pools[1].Slots.SetItem(0, pools[1].Slots[0].WriteDword(12, 0).WriteDword(16, 0x1D972C0)) };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            NativeBattleOriginalEffectState effect = new(132, 131, new(132, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            Frame = new(memory, effect, new(55), NativeBattleDisolvingAcid132State.FromColdProcess().WriteDword(WorkingGlobal, WorkingAddress).WriteDword(CasterGlobal, 0x1D972C0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 }, null), _ => throw new InvalidOperationException(), (current, _, source, _, _, context) =>
            {
                Acts.Add((source.Address, context));
                return new(current, Remaining);
            }, (current, operation) =>
            {
                Operations.Add(operation);
                DrawCheck?.Invoke(current);
                return current;
            });
        }

        public NativeBattleDisolvingAcid132Frame Main(int age) => NativeBattleDisolvingAcid132Controller.Write(Frame, MainAddress, NativeBattleDisolvingAcid132Controller.Read(Frame, MainAddress).WriteWord(12, (ushort)age));

        public NativeBattleDisolvingAcid132Frame Act(int age, bool impact, out uint address)
        {
            var appended = NativeBattleDisolvingAcid132Controller.Append(Frame, 2, impact ? ImpactActCallback : PrimaryActCallback);
            address = appended.Address;
            NativeBattleModulePoolTask task = NativeBattleDisolvingAcid132Controller.Read(appended.Frame, address).WriteWord(12, (ushort)age).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 2000).WriteWord(22, 0).WriteDword(24, impact ? ImpactActAddress : PrimaryActAddress);
            return NativeBattleDisolvingAcid132Controller.Write(appended.Frame, address, task);
        }
    }
}
