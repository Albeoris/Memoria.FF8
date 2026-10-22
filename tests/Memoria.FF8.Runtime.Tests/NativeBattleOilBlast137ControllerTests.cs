using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleOilBlast137Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilBlast137ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_selects_native_old_DWORD_toggle_before_first_child_initialization(uint old, int offset, uint next)
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetRoot(f.Frame, 0, 0, old);
        (frame, int result) = NativeBattleOilBlast137Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, result);
        Assert.Equal(Fixture.Working + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, NativeBattleOilBlast137Controller.Read(frame, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, NativeBattleOilBlast137Controller.Read(frame, RootBank).ReadWord(12));
        Assert.Empty(f.Draws);
    }

    [Fact]
    public void Root_old1_initializes_all_three_actual_pools_and_captures_first_contact_F1()
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetRoot(f.Frame, 1, 0, 0);
        (frame, int result) = NativeBattleOilBlast137Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, result);
        Assert.Equal(new ushort[] { 20, 0x5A0, 44, 36 }, frame.Effect.Pools.Select(pool => pool.Stride).ToArray());
        Assert.Equal(new[] { 1, 1, 32, 60 }, frame.Effect.Pools.Select(pool => pool.Slots.Length).ToArray());
        NativeBattleModulePoolTask main = f.Main(frame);
        Assert.Equal(Fixture.TargetPointer, main.ReadDword(16));
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal(new NativeBattleVector(100, 700, -300), NativeBattleOilBlast137Controller.Vector(main, 0x1C));
        Assert.Equal((ushort)0, main.ReadWord(0x22));
        Assert.Equal((Fixture.TargetPointer, (byte)0xF1), Assert.Single(f.Bones));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(MainOffset + 12, 4));
    }

    [Fact]
    public void Root_scans_secondary_and_particles_even_when_main_retires_then_gates_only_on_main()
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetRoot(f.SetAge(f.Frame, 60), 8, 1, 0);
        (frame, uint secondary) = NativeBattleOilBlast137Controller.Append(frame, 2, SecondaryCallback);
        frame = NativeBattleOilBlast137Controller.Write(frame, secondary, NativeBattleOilBlast137Controller.Read(frame, secondary).WriteWord(18, 0));
        (frame, uint particle) = NativeBattleOilBlast137Controller.Append(frame, 3, ParticleCallback);
        frame = NativeBattleOilBlast137Controller.Write(frame, particle, NativeBattleOilBlast137Controller.Read(frame, particle).WriteWord(18, 0).WriteWord(0x1A, 1000));
        (frame, int result) = NativeBattleOilBlast137Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(2, result);
        Assert.Equal((ushort)8, NativeBattleOilBlast137Controller.Read(frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)60, f.Main(frame).ReadWord(12));
        Assert.Equal(new[] { nameof(NativeBattleOilBlast137DrawStatic88), nameof(NativeBattleOilBlast137DrawSprite180) }, f.Draws.Select(draw => draw.GetType().Name).ToArray());
        Assert.Equal((ushort)1, NativeBattleOilBlast137Controller.Read(frame, secondary).ReadWord(18));
        Assert.Equal((ushort)1, NativeBattleOilBlast137Controller.Read(frame, particle).ReadWord(18));
    }

    [Theory]
    [InlineData(10, 1, 0, 0)]
    [InlineData(45, 0, 1, 0)]
    [InlineData(60, 0, 0, 2)]
    public void Main_uses_literal_sound_contact_and_signed_terminal_ages(int age, int sounds, int contacts, int expected)
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetAge(f.Frame, age);
        (frame, int result) = NativeBattleOilBlast137Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(expected, result);
        Assert.Equal(sounds, f.Operations.OfType<NativeBattleOilBlast137Sound>().Count());
        Assert.Equal(contacts, f.Operations.OfType<NativeBattleOilBlast137Contact>().Count());
        if (contacts != 0)
        {
            Assert.Equal(1, Assert.Single(f.Operations.OfType<NativeBattleOilBlast137Contact>()).Index);
        }
        Assert.Equal((ushort)(expected == 2 ? age : age + 1), f.Main(frame).ReadWord(12));
    }

    [Fact]
    public void Opening_old18_captures_exact_bones_zeroes_Y_and_keeps_context_depth_unknown()
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetAge(f.Frame, 18);
        (frame, _) = NativeBattleOilBlast137Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(new byte[] { 0x1E, 0x1C }, f.Bones.Select(bone => bone.Selector).ToArray());
        Assert.Equal(new[] { (0x84, 0x40), (0xC4, 0x40) }, f.Acts.Select(act => (act.Offset, act.Size)).ToArray());
        Assert.Equal(2, f.Operations.OfType<NativeBattleOilBlast137InitializeAct>().Count());
        Assert.Equal((ushort)0, f.Main(frame).ReadWord(0x2E));
        Assert.Equal((ushort)0, f.Main(frame).ReadWord(0x36));
        foreach (var act in f.Acts)
        {
            Assert.Throws<NotSupportedException>(() => act.Context.Read(32, 4));
            Assert.Equal(0u, U32(act.Context, 36));
            Assert.Equal(Fixture.Working + MorphOffset, U32(act.Context, 40));
        }
        Assert.Equal(77u, frame.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Main_old21_publishes_custom_window_and_actual_diagonal_then_births_two_secondary_rows()
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetAge(f.Frame, 21);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 18; index++)
        {
            expected = expected.Draw(out _);
        }
        (frame, _) = NativeBattleOilBlast137Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(2, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleEffectArena custom = Assert.Single(f.CustomContexts);
        Assert.Equal(CustomMeshAddress, U32(custom, 0));
        Assert.Equal((ushort)0, U16(custom, 4));
        Assert.Equal(new ushort[] { 0, 0, 256, 256, 0, 0, 128, 256 }, Enumerable.Range(0, 8).Select(index => U16(custom, 8 + index * 2)).ToArray());
        Assert.Equal((ushort)1024, frame.State.ReadWord(MatrixAddress));
        Assert.Equal((ushort)1024, frame.State.ReadWord(MatrixAddress + 8));
        Assert.Equal((ushort)1024, frame.State.ReadWord(MatrixAddress + 16));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Main_old28_emits_ten_particles_and_actual_impact_context_with_depth_minus256()
    {
        Fixture f = new();
        NativeBattleOilBlast137Frame frame = f.SetAge(f.Frame, 28);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 58; index++)
        {
            expected = expected.Draw(out _);
        }
        (frame, _) = NativeBattleOilBlast137Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(10, frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        var impact = Assert.Single(f.Acts, act => act.Offset == 0x2A4);
        Assert.Equal(0x2FC, impact.Size);
        Assert.Equal(unchecked((uint)-256), U32(impact.Context, 32));
        Assert.Equal(1u, U32(impact.Context, 36));
        Assert.Equal((ushort)96, U16(Assert.Single(f.CustomContexts), 4));
    }

    [Fact]
    public void Particle_draw_precedes_acceleration_and_terminal_phase_increment()
    {
        Fixture f = new();
        (NativeBattleOilBlast137Frame frame, uint child) = NativeBattleOilBlast137Controller.Append(f.Frame, 3, ParticleCallback);
        NativeBattleModulePoolTask task = NativeBattleOilBlast137Controller.Read(frame, child).WriteWord(18, 11).WriteWord(0x1A, 1024);
        task = NativeBattleOilBlast137Controller.PutVector(task, 12, new(10, 20, 30));
        task = NativeBattleOilBlast137Controller.PutVector(task, 0x14, new(5, -6, 7));
        task = NativeBattleOilBlast137Controller.PutVector(task, 0x1C, new(-1, 2, -3));
        frame = NativeBattleOilBlast137Controller.Write(frame, child, task);
        (frame, int result) = NativeBattleOilBlast137Controller.AdvanceParticle(frame, child, f.Resources, f.Inputs);
        Assert.Equal(2, result);
        Assert.Equal((ushort)11, U16(Assert.Single(f.SpriteContexts), 4));
        task = NativeBattleOilBlast137Controller.Read(frame, child);
        Assert.Equal(new NativeBattleVector(14, 16, 34), NativeBattleOilBlast137Controller.Vector(task, 12));
        Assert.Equal((ushort)12, task.ReadWord(18));
    }

    [Fact]
    public void Secondary_bounces_after_position_update_and_preserves_unknown_unwritten_suffix_through_JSON()
    {
        Fixture f = new();
        (NativeBattleOilBlast137Frame frame, uint child) = NativeBattleOilBlast137Controller.Append(f.Frame, 2, SecondaryCallback);
        NativeBattleModulePoolTask task = NativeBattleOilBlast137Controller.Read(frame, child).WriteWord(18, 29).WriteWord(14, unchecked((ushort)-1)).WriteWord(0x16, 0).MarkPayloadUnknown(0x22, 2);
        frame = NativeBattleOilBlast137Controller.Write(frame, child, task);
        NativeBattleOilBlast137Frame restored = JsonSerializer.Deserialize<NativeBattleOilBlast137Frame>(JsonSerializer.Serialize(frame))!;
        (restored, int result) = NativeBattleOilBlast137Controller.AdvanceSecondary(restored, child, f.Resources, f.Inputs);
        task = NativeBattleOilBlast137Controller.Read(restored, child);
        Assert.Equal(2, result);
        Assert.Equal((ushort)15, task.ReadWord(14));
        Assert.Equal(unchecked((ushort)-16), task.ReadWord(0x16));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x22));
        Assert.Equal((ushort)30, task.ReadWord(18));
        Assert.Throws<NotSupportedException>(() => Assert.Single(f.StaticContexts).Read(8, 4));
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));

    private sealed class Fixture
    {
        public const uint Working = NativeBattleEffectNodeMemory.ArenaAddress;
        public const uint MainAddress = Working + MainOffset;
        public const uint TargetPointer = 0x1D972C0 + 3 * 156;
        public NativeBattleOilBlast137Frame Frame { get; }
        public NativeBattleOilBlast137Inputs Inputs { get; }
        public NativeBattleOilBlast137Resources Resources { get; }
        public List<NativeBattleOilBlast137Operation> Operations { get; } = [];
        public List<NativeBattleOilBlast137Operation> Draws { get; } = [];
        public List<(uint Model, byte Selector)> Bones { get; } = [];
        public List<(int Offset, int Size, NativeBattleEffectArena Context)> Acts { get; } = [];
        public List<NativeBattleEffectArena> CustomContexts { get; } = [];
        public List<NativeBattleEffectArena> StaticContexts { get; } = [];
        public List<NativeBattleEffectArena> SpriteContexts { get; } = [];

        public Fixture()
        {
            NativeBattleGfAnimationSource opening = new(OpeningActAddress, []);
            NativeBattleGfAnimationSource middle = new(MainActAddress, []);
            NativeBattleGfAnimationSource impact = new(ImpactActAddress, []);
            Resources = new(opening, middle, impact, new(0, SpriteAddress, 0, 0, []), new(0, StaticMeshAddress, []), new(0, CustomMeshAddress, []), new("unit", []), 0x641B2, new(0, MatrixAddress, 0, 0, []));
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
            (root, _) = root.Append(RootCallback);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1);
            (main, _) = main.Append(MainCallback);
            NativeBattleEffectTaskPool secondary = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, Working + SecondaryOffset, SecondaryStride, SecondaryCount);
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, Working, ParticleStride, ParticleCount);
            NativeBattleOriginalEffectState effect = new(137, 136, new(137, Entry, Initializer), new(0, 0, 1, 1, 0, 0), null) { Pools = [root, main, secondary, particles], ReturnedDescriptor = RootDescriptor };
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            NativeBattleOilBlast137State state = NativeBattleOilBlast137State.FromColdProcess() with { ColdMatrixSourcePending = false, Matrix32 = NativeBattleEffectArena.Clear().Write(0, NativeBattlePackedMatrix32.FromMatrix(identity, 0).Bytes.AsSpan()) };
            state = state.WriteDword(WorkingGlobal, Working).WriteDword(CasterGlobal, 0x1D972C0);
            Frame = new(memory, effect, new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Frame = NativeBattleOilBlast137Controller.Write(Frame, MainAddress, Main(Frame).WriteDword(16, TargetPointer));
            NativeBattleModulePoolTask task = Main(Frame);
            task = NativeBattleOilBlast137Controller.PutVector(task, 0x1C, new(1000, 900, 2000));
            task = NativeBattleOilBlast137Controller.PutVector(task, 0x3C, new(100, 0, -300));
            task = task.WriteBytes(0x44, NativeBattlePackedMatrix32.FromMatrix(identity, 0).Bytes.AsSpan()).WriteBytes(0x64, NativeBattlePackedMatrix32.FromMatrix(identity, 0).Bytes.AsSpan());
            Frame = NativeBattleOilBlast137Controller.Write(Frame, MainAddress, task);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            for (int header = 0; header < 2; header++)
            {
                (packets, _) = packets.Prepare(new(0, 8, 12, 0, 181, 137, null));
                (packets, _) = packets.AppendContact(new(new((byte)(header == 0 ? 0 : 3), 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
                (packets, _) = packets.Publish(1);
            }
            Inputs = new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, (frame, model, selector, _) =>
            {
                Bones.Add((model, selector));
                return new(frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { Material = 77 } } }, new(100, 700, -300));
            }, _ => NativeBattlePackedMatrix32.FromMatrix(identity with { X = 10, Y = 20, Z = 2000 }, null), _ => 123,
            (frame, _, offset, size, _, context) =>
            {
                Acts.Add((offset, size, context));
                return new(frame, 1);
            }, (frame, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleOilBlast137DrawCustom84 custom)
                {
                    Draws.Add(operation);
                    CustomContexts.Add(View(frame, custom.Temporary84, 84));
                }
                if (operation is NativeBattleOilBlast137DrawStatic88 mesh)
                {
                    Draws.Add(operation);
                    StaticContexts.Add(View(frame, mesh.Temporary88, 88));
                }
                if (operation is NativeBattleOilBlast137DrawSprite180 sprite)
                {
                    Draws.Add(operation);
                    SpriteContexts.Add(View(frame, sprite.Temporary180, 180));
                }
                return frame;
            });
        }

        public NativeBattleModulePoolTask Main(NativeBattleOilBlast137Frame frame) => NativeBattleOilBlast137Controller.Read(frame, MainAddress);
        public NativeBattleOilBlast137Frame SetAge(NativeBattleOilBlast137Frame frame, int age) => NativeBattleOilBlast137Controller.Write(frame, MainAddress, Main(frame).WriteWord(12, unchecked((ushort)age)));
        public NativeBattleOilBlast137Frame SetRoot(NativeBattleOilBlast137Frame frame, ushort age, ushort initialized, uint toggle) => NativeBattleOilBlast137Controller.Write(frame, RootBank, NativeBattleOilBlast137Controller.Read(frame, RootBank).WriteWord(12, age).WriteWord(14, initialized).WriteDword(16, toggle));
        private static NativeBattleEffectArena View(NativeBattleOilBlast137Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(frame.Staging!, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
