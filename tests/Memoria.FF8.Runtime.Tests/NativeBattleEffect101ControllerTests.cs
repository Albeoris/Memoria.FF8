using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect101Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect101ControllerTests
{
    [Fact]
    public void Entry_forces_actual_recipient_bits_and_registers_current_source_without_clone()
    {
        Fixture fixture = new();
        fixture.Models[0] = fixture.Models[0] with { Flags0 = 3 };
        NativeBattleEffect101Frame initialized = NativeBattleEffect101Controller.Initialize(fixture.Frame, fixture.Resources, fixture.Inputs);
        Assert.Equal((ushort)15, fixture.Models[0].Flags0);
        Assert.Equal(ArenaAddress, initialized.State.ReadDword(WorkingGlobal));
        Assert.Equal(0x1D97530u, initialized.State.ReadDword(CasterGlobal));
        Assert.Equal(0x1D972C0u, initialized.State.ReadDword(TargetGlobal));
        Assert.Single(initialized.Effect.Pools);
        Assert.Equal(new byte[8], initialized.Effect.Pools[0].Slots[0].ReadBytes(12, 8));
        Assert.Same(fixture.Frame.Memory.Arena, initialized.Memory.Arena);
    }

    [Fact]
    public void Startup_saves_native_inputs_schedules_real_sequence_fourteen_and_scans_main_same_callback()
    {
        Fixture fixture = new();
        NativeBattleEffect101Frame next = fixture.Step(fixture.Frame);
        Assert.Equal(ArenaAddress + SecondPacketOffset, next.State.ReadDword(CursorGlobal));
        Assert.Equal(2, next.Effect.Pools.Length);
        Assert.Equal((ushort)0x2D0, next.Effect.Pools[1].Stride);
        NativeBattleModulePoolTask main = next.Effect.Pools[1].Slots[0];
        Assert.Equal(900u, main.ReadDword(16));
        Assert.Equal(0xAB204080u, main.ReadDword(20));
        Assert.Equal(200, unchecked((short)main.ReadWord(24)));
        Assert.Equal(-100, unchecked((short)main.ReadWord(26)));
        Assert.Equal(-200, unchecked((short)main.ReadWord(28)));
        Assert.Equal((ushort)1234, main.ReadWord(30));
        Assert.Equal(MainActAddress, main.ReadDword(32));
        Assert.Equal(new NativeBattleEffect101RingSequence(0, 14), Assert.Single(fixture.Operations.OfType<NativeBattleEffect101RingSequence>()));
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect101Sound>());
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal(fixture.Frame.Random, next.Random);
        Assert.Throws<NotSupportedException>(() => next.Memory.Arena!.Read(12, 1));
    }

    [Fact]
    public void Actual_context_is_six_written_anchor_bytes_unknown_W_depth_and_literal_morph_address()
    {
        Fixture fixture = new();
        fixture.Step(fixture.Frame);
        NativeBattleEffectArena context = Assert.Single(fixture.Contexts);
        Assert.Equal((short)500, BitConverter.ToInt16(context.Read(0, 2)));
        Assert.Equal((short)-2700, BitConverter.ToInt16(context.Read(2, 2)));
        Assert.Equal((short)-2000, BitConverter.ToInt16(context.Read(4, 2)));
        Assert.Throws<NotSupportedException>(() => context.Read(6, 2));
        Assert.Equal(-128, BitConverter.ToInt32(context.Read(8, 4)));
        Assert.Equal(ArenaAddress + MorphOffset, BitConverter.ToUInt32(context.Read(12, 4)));
    }

    [Theory]
    [InlineData(6, 1100)]
    [InlineData(12, 2200)]
    [InlineData(42, 2200)]
    [InlineData(48, 1100)]
    public void Stage_ramp_uses_native_signed_truncation_and_actual_zero_color(int age, int expected)
    {
        Fixture fixture = new();
        NativeBattleEffect101Frame frame = Age(fixture.Step(fixture.Frame), age);
        fixture.Clear();
        fixture.Step(frame);
        Assert.Equal(new NativeBattleEffect101Stage(checked((short)expected), 0), Assert.Single(fixture.Operations.OfType<NativeBattleEffect101Stage>()));
    }

    [Fact]
    public void Tint_and_literal_Y_store_precede_world_position_overwrite_and_scale_pointer_is_live_only_through_twenty()
    {
        Fixture fixture = new();
        NativeBattleEffect101Frame frame = Age(fixture.Step(fixture.Frame), 20);
        fixture.Clear();
        NativeBattleEffect101Frame next = fixture.Step(frame);
        Assert.Equal(4096u, next.State.ReadDword(ScaleAddress));
        Assert.Equal(4096u, next.State.ReadDword(ScaleAddress + 4));
        Assert.Equal(4096u, next.State.ReadDword(ScaleAddress + 8));
        Assert.Equal(new uint[] { 0, ScaleAddress }, fixture.Operations.OfType<NativeBattleEffect101ScalePointer>().Select(operation => operation.Address));
        Assert.Equal((short)300, Assert.Single(fixture.Operations.OfType<NativeBattleEffect101PositionY>()).Y);
        Assert.Equal((short)1200, fixture.Models[0].Transform!.Position28.Y);
        Assert.Equal(0xAB204080u, fixture.Models[0].Color44);
        Assert.Equal(4096u, next.Memory.ScriptedColorRegisters!.Ir0);
        fixture.Clear();
        fixture.Step(Age(next, 21));
        Assert.Equal(new NativeBattleEffect101ScalePointer(0, 0), Assert.Single(fixture.Operations.OfType<NativeBattleEffect101ScalePointer>()));
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect101PositionY>());
    }

    [Theory]
    [InlineData(22, 0)]
    [InlineData(23, 1)]
    [InlineData(24, 0)]
    public void Only_old_twenty_three_clears_flag_four_and_world_is_exact_current_bone_plus_caster(int age, int expected)
    {
        Fixture fixture = new();
        NativeBattleEffect101Frame frame = Age(fixture.Step(fixture.Frame), age);
        fixture.Clear();
        fixture.Step(frame);
        Assert.Equal(expected, fixture.Operations.OfType<NativeBattleEffect101ActorFlags>().Count(operation => operation.Mask == 4));
        NativeBattleEffect101WorldR9TXYZ world = Assert.Single(fixture.Operations.OfType<NativeBattleEffect101WorldR9TXYZ>());
        Assert.Equal(new short[] { -4096, 0, 0, 0, 4096, 0, 0, 0, -4096 }, world.Matrix.Rotation);
        Assert.Equal((575, 1200, -3400), (world.Matrix.X, world.Matrix.Y, world.Matrix.Z));
    }

    [Fact]
    public void Return_blend_reads_current_position_and_saved_vector_with_actual_weights_then_terminal_restores_full_inputs()
    {
        Fixture fixture = new();
        NativeBattleEffect101Frame frame = Age(fixture.Step(fixture.Frame), 38);
        fixture.SetPosition(0, new(1, 2, 3));
        fixture.Clear();
        NativeBattleEffect101Frame next = fixture.Step(frame);
        Assert.Equal((new NativeBattleVector(1, 2, 3), (short)4096, (short)0), Assert.Single(fixture.Blends));
        Assert.Equal(new NativeBattleVector(11, 22, 33), fixture.Models[0].Transform!.Position28);
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect101WorldR9TXYZ>());
        Assert.Empty(fixture.Contexts);
        fixture.Clear();
        next = fixture.Step(Age(next, 53));
        Assert.Equal(0u, next.Effect.ReturnedDescriptor);
        Assert.Equal(new NativeBattleVector(200, -100, -200), fixture.Models[0].Transform!.Position28);
        Assert.Equal((short)1234, fixture.Models[0].Transform!.Vertical34);
        Assert.Equal(0xAB204080u, fixture.Models[0].Color44);
        Assert.Equal(2, fixture.Operations.OfType<NativeBattleEffect101Stage>().Count(operation => operation.Depth == 0));
    }

    [Fact]
    public void Independent_JSON_next_preserves_retained_workspaces_and_does_not_add_CRT_draws()
    {
        Fixture fixture = new();
        NativeBattleEffect101Frame frame = Age(fixture.Step(fixture.Frame), 38);
        NativeBattleEffect101Frame imported = JsonSerializer.Deserialize<NativeBattleEffect101Frame>(JsonSerializer.Serialize(frame))!;
        Dictionary<int, NativeBattleGraphicsModel> models = new(fixture.Models);
        NativeBattleEffect101Frame expected = fixture.Step(frame);
        string expectedModels = JsonSerializer.Serialize(fixture.Models);
        fixture.Models.Clear();
        foreach ((int index, NativeBattleGraphicsModel model) in models)
        {
            fixture.Models[index] = model;
        }
        NativeBattleEffect101Frame actual = fixture.Step(imported);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(expectedModels, JsonSerializer.Serialize(fixture.Models));
        Assert.Equal(frame.Random, actual.Random);
    }

    private static NativeBattleEffect101Frame Age(NativeBattleEffect101Frame frame, int age) => NativeBattleEffect101Controller.Write(frame, frame.Effect.Pools[1].BankAddress, frame.Effect.Pools[1].Slots[0].WriteWord(12, unchecked((ushort)age)));

    private sealed class Fixture
    {
        public NativeBattleEffect101Frame Frame { get; }
        public NativeBattleEffect101Resources Resources { get; }
        public NativeBattleEffect101Inputs Inputs { get; }
        public Dictionary<int, NativeBattleGraphicsModel> Models { get; } = [];
        public List<NativeBattleEffect101Operation> Operations { get; } = [];
        public List<NativeBattleEffectArena> Contexts { get; } = [];
        public List<(NativeBattleVector Current, short First, short Second)> Blends { get; } = [];

        public Fixture()
        {
            byte[] act = new byte[0x2CD4];
            BitConverter.GetBytes(8).CopyTo(act, 4);
            BitConverter.GetBytes(38).CopyTo(act, 12);
            byte[] camera = new byte[0x11C];
            BitConverter.GetBytes((ushort)camera.Length).CopyTo(camera, 6);
            Resources = new(new(MainActAddress, act.ToImmutableArray()), new("FF8_EN:13248C0", camera.ToImmutableArray()), 0x64218);
            Models[0] = new(0, 15) { Color44 = 0xAB204080, Transform = new(Initial20: new(0, 0, 0), Position28: new(200, -100, -200), Vertical34: 1234, Angle36: 0, Scale38: 900) };
            Models[4] = new(4, 3) { Transform = new(Initial20: new(0, 0, 0), Position28: new(500, 0, -3000), Vertical34: 0, Angle36: 0, Scale38: 512) };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(4, 8, 13, 0, 290, 101, null));
            (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int slot) = root.Append(RootCallback);
            root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteBytes(12, new byte[8]).WriteWord(12, 1)) };
            NativeBattleOriginalEffectState effect = new(101, 100, new(101, Entry, Initializer), new(4, 0, 1, 0, 0, 0), null) { Pools = [root], ReturnedDescriptor = RootDescriptor, ResourcePointers = [new(InitializerGlobal, 0x30000)] };
            byte[] tim = new byte[546];
            BitConverter.GetBytes(0x10).CopyTo(tim, 0);
            BitConverter.GetBytes(9).CopyTo(tim, 4);
            BitConverter.GetBytes(524).CopyTo(tim, 8);
            BitConverter.GetBytes((short)256).CopyTo(tim, 16);
            BitConverter.GetBytes((short)1).CopyTo(tim, 18);
            BitConverter.GetBytes(14).CopyTo(tim, 532);
            BitConverter.GetBytes((short)1).CopyTo(tim, 540);
            BitConverter.GetBytes((short)1).CopyTo(tim, 542);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x4F000000, Rgb = [1, 2, 3] } };
            Frame = new(memory, effect, new(123), NativeBattleEffect101State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D97530).WriteDword(TargetGlobal, 0x1D972C0));
            Inputs = new(packets, actor => Models[actor], (current, actor) => new(current, Position8(actor)), (current, _) => new(current, 75), (current, _, offset, size, source, context) =>
            {
                Assert.Equal(32, offset);
                Assert.Equal(0x2B0, size);
                Assert.Equal(MainActAddress, source.Address);
                Contexts.Add(context);
                return new(current, 1);
            }, (current, actor, _, first, second) =>
            {
                Blends.Add((Models[actor].Transform!.Position28, first, second));
                return new(current, new(11, 22, 33));
            }, Execute, NativeBattleGteColorInterpolation.CodeConstants45F270);
        }
        public NativeBattleEffect101Frame Step(NativeBattleEffect101Frame frame) => NativeBattleEffect101Controller.Advance(frame, Resources, Inputs);
        public void Clear()
        {
            Operations.Clear();
            Contexts.Clear();
            Blends.Clear();
        }
        public void SetPosition(int actor, NativeBattleVector position) => Models[actor] = Models[actor] with { Transform = Models[actor].Transform! with { Position28 = position } };
        private NativeBattleEffectArena Position8(int actor)
        {
            NativeBattleModelTransform transform = Models[actor].Transform!;
            return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Vertical34));
        }
        private NativeBattleEffect101Frame Execute(NativeBattleEffect101Frame current, NativeBattleEffect101Operation operation)
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleEffect101ActorFlags flags:
                    Models[flags.Actor] = Models[flags.Actor] with { Flags0 = (ushort)((Models[flags.Actor].Flags0 & ~flags.Mask) | (flags.Bits & flags.Mask)) };
                    break;
                case NativeBattleEffect101PositionXYZ point:
                    SetPosition(point.Actor, point.Position);
                    break;
                case NativeBattleEffect101PositionY point:
                    SetPosition(point.Actor, Models[point.Actor].Transform!.Position28 with { Y = point.Y });
                    break;
                case NativeBattleEffect101Position8 point:
                    SetPosition(point.Actor, new(BitConverter.ToInt16(point.Data.Read(0, 2)), BitConverter.ToInt16(point.Data.Read(2, 2)), BitConverter.ToInt16(point.Data.Read(4, 2))));
                    Models[point.Actor] = Models[point.Actor] with { Transform = Models[point.Actor].Transform! with { Vertical34 = BitConverter.ToInt16(point.Data.Read(6, 2)) } };
                    break;
                case NativeBattleEffect101Color44 color:
                    Models[color.Actor] = Models[color.Actor] with { Color44 = color.Value };
                    break;
            }
            return current;
        }
    }
}
