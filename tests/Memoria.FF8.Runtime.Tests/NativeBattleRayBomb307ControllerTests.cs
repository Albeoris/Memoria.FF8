using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRayBomb307ControllerTests
{
    [Fact]
    public void Paused_main_publishes_current_bone_world_origin_without_GTE_or_age_changes()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame before = fixture.Task(MainCallback, 0);
        string registers = NativeBattleTestSnapshots.Json(before.Memory.GeometryRegisters);
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceMain(before, ChildBank, fixture.Resources, fixture.Inputs(0x201));
        Assert.Equal(0, step.Result);
        Assert.Equal((ushort)0, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)3510, step.Frame.State.ReadWord(0x2242580));
        Assert.Equal((ushort)20, step.Frame.State.ReadWord(0x2242582));
        Assert.Equal(unchecked((ushort)-970), step.Frame.State.ReadWord(0x2242584));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadWord(0x2242586));
        Assert.Equal(before.Random, step.Frame.Random);
        Assert.Equal(registers, NativeBattleTestSnapshots.Json(step.Frame.Memory.GeometryRegisters));
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void First_main_uses_CPU_target_center_and_preserves_both_unwritten_fourth_words()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceMain(fixture.Task(MainCallback, 0), ChildBank, fixture.Resources, fixture.Inputs());
        Assert.Equal((ushort)111, step.Frame.State.ReadWord(0x2242568));
        Assert.Equal((ushort)0, step.Frame.State.ReadWord(0x224256A));
        Assert.Equal((ushort)333, step.Frame.State.ReadWord(0x224256C));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadWord(0x224256E));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadWord(0x2242586));
        Assert.Equal((ushort)1900, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(0x16));
        Assert.Equal(new NativeBattleRayBomb307StageFade(0, 0), Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Age62_appends_actual_root_wait_and_consumes_the_exact_deferred_birth_stream()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame before = fixture.Task(MainCallback, 62);
        NativeCrtRandomState expected = before.Random;
        int children = 0;
        for (int index = 0; index < 5; index++)
        {
            expected = expected.Draw(out ushort count);
            int births = (count & 1) + 1;
            children += births;
            for (int draw = 0; draw < births * 6; draw++)
            {
                expected = expected.Draw(out _);
            }
        }
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceMain(before, ChildBank, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask wait = Assert.Single(step.Frame.Effect.Pools[0].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == TextureCallback));
        Assert.Equal((ushort)0, wait.ReadWord(12));
        Assert.DoesNotContain(step.Frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == TextureCallback);
        Assert.Equal(children, step.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x5A1D90));
        Assert.Equal(expected, step.Frame.Random);
        string json = NativeBattleTestSnapshots.Json(step.Frame);
        Fixture restoredFixture = new();
        NativeBattleRayBomb307TaskStep next = NativeBattleRayBomb307Controller.AdvanceMain(step.Frame, ChildBank, fixture.Resources, fixture.Inputs());
        NativeBattleRayBomb307TaskStep restored = NativeBattleRayBomb307Controller.AdvanceMain(NativeBattleTestSnapshots.RestoreJson<NativeBattleRayBomb307Frame>(json)!, ChildBank, restoredFixture.Resources, restoredFixture.Inputs());
        Assert.Equal(NativeBattleTestSnapshots.Json(next.Frame), NativeBattleTestSnapshots.Json(restored.Frame));
    }

    [Fact]
    public void Texture_wait_clears_its_actual_root_completion_before_the_queue_and_reads_reached_completion()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame frame = fixture.Task(MainCallback, 40);
        (frame, uint wait) = NativeBattleRayBomb307Controller.Append(frame, 0, TextureCallback);
        frame = NativeBattleRayBomb307Controller.Write(frame, wait, NativeBattleRayBomb307Controller.Read(frame, wait).WriteWord(12, 0).WriteWord(14, 0xFFFF));
        fixture.Complete = true;
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceChild(frame, wait, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)1, NativeBattleRayBomb307Controller.Read(step.Frame, wait).ReadWord(12));
        Assert.Equal(new NativeBattleRayBomb307RestoreTexture(ArenaAddress + TextureRestoreOffset, wait + 14), Assert.Single(fixture.Operations));
        Assert.Equal((ushort)1, NativeBattleRayBomb307Controller.Read(step.Frame, wait).ReadWord(14));
    }

    [Fact]
    public void Delayed_sprite_has_no_temporary_or_geometry_reads_until_delay_finishes()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame frame = fixture.Task(0x5A24D0, 0);
        frame = NativeBattleRayBomb307Controller.Write(frame, ChildBank, NativeBattleRayBomb307Controller.Read(frame, ChildBank).WriteWord(14, 2));
        frame = frame with { Staging = null, Memory = frame.Memory with { GeometryRegisters = null, ScriptedColorRegisters = null } };
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceSprite(frame, ChildBank, fixture.Inputs());
        Assert.Equal((ushort)1, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(14));
        Assert.Equal((ushort)0, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Sprite_angle_is_signed_DWORD_and_uncolored_middle_phase_retains_actual_temporary_RGB()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame frame = fixture.Task(0x5A24D0, 10);
        NativeBattleModulePoolTask task = NativeBattleRayBomb307Controller.Read(frame, ChildBank).WriteWord(14, 0).WriteWord(0x16, 100).WriteWord(0x18, unchecked((ushort)-321)).WriteWord(0x1C, 1024).WriteWord(0x1E, 64).WriteWord(0x20, 9);
        frame = NativeBattleRayBomb307Controller.Write(frame, ChildBank, task);
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceSprite(frame, ChildBank, fixture.Inputs());
        Draw draw = Assert.Single(fixture.Draws);
        Assert.Equal(0xCE13E0u, draw.Source);
        Assert.Equal((ushort)1, draw.Flags);
        Assert.Equal((ushort)9, draw.Phase);
        Assert.Equal(-321, BinaryPrimitives.ReadInt32LittleEndian(draw.Context.Read(8, 4)));
        Assert.Throws<NotSupportedException>(() => draw.Context.Read(0x1C, 3));
        Assert.Equal((ushort)10, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(0x20));
        Assert.Equal((ushort)1088, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(18, 0u, false)]
    [InlineData(22, 0xC0u, true)]
    public void Central_mesh_uses_billboard_matrix_padding_and_only_reached_fade_fields(ushort age, uint flags, bool cueKnown)
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame frame = fixture.Task(0x5A23E0, age);
        frame = NativeBattleRayBomb307Controller.Write(frame, ChildBank, NativeBattleRayBomb307Controller.Read(frame, ChildBank).WriteWord(0x16, 100).WriteWord(0x1C, 4096).WriteWord(0x1E, 0));
        NativeBattleRayBomb307TaskStep step = NativeBattleRayBomb307Controller.AdvanceMesh(frame, ChildBank, fixture.Resources, fixture.Inputs(0x201));
        Draw draw = Assert.Single(fixture.Draws);
        Assert.Equal(0xCE350Cu, draw.Source);
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(draw.Context.Read(0x1C, 4)));
        Assert.Equal(new byte[3], draw.Context.Read(8, 3));
        if (cueKnown)
        {
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(draw.Context.Read(12, 4)));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => draw.Context.Read(12, 4));
        }
        Assert.Equal((byte)0xFE, step.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal((byte)0xCA, step.Frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(age, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void First_slot_phase_skips_drawing_then_applies_signed_shift_physics_preserving_W_and_tail()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame frame = fixture.Task(MainCallback, 1);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().MarkUnknown(0, 24).Write(0, new byte[] { 1 }).Write(4, BitConverter.GetBytes((short)0)).Write(6, BitConverter.GetBytes((short)-64)).Write(8, BitConverter.GetBytes((short)100)).Write(10, BitConverter.GetBytes((short)200)).Write(12, BitConverter.GetBytes((short)1000)).Write(16, BitConverter.GetBytes((short)-9)).Write(18, BitConverter.GetBytes((short)8)).Write(20, BitConverter.GetBytes((short)-1));
        frame = frame with { State = frame.State with { Particles = NativeBattleEffectNodeContextMemory.Copy(row, 0, frame.State.Particles, 0, 24) } };
        (frame, int alive) = NativeBattleRayBomb307Controller.AdvanceSlots(frame, fixture.Inputs());
        Assert.Equal(1, alive);
        Assert.Empty(fixture.Draws);
        Assert.Throws<NotSupportedException>(() => frame.State.Particles.Read(0, 4));
        Assert.Equal((short)1, Word(frame.State.Particles, 4));
        Assert.Equal((short)-66, Word(frame.State.Particles, 6));
        Assert.Equal((short)91, Word(frame.State.Particles, 8));
        Assert.Equal((short)208, Word(frame.State.Particles, 10));
        Assert.Equal((short)999, Word(frame.State.Particles, 12));
        Assert.Equal((short)-7, Word(frame.State.Particles, 16));
        Assert.Equal((short)7, Word(frame.State.Particles, 18));
        Assert.Equal((short)0, Word(frame.State.Particles, 20));
        Assert.Throws<NotSupportedException>(() => frame.State.Particles.Read(14, 2));
        Assert.Throws<NotSupportedException>(() => frame.State.Particles.Read(22, 2));
        string json = NativeBattleTestSnapshots.Json(frame);
        (NativeBattleRayBomb307Frame next, int nextAlive) = NativeBattleRayBomb307Controller.AdvanceSlots(frame, fixture.Inputs());
        Fixture restoredFixture = new();
        (NativeBattleRayBomb307Frame restored, int restoredAlive) = NativeBattleRayBomb307Controller.AdvanceSlots(NativeBattleTestSnapshots.RestoreJson<NativeBattleRayBomb307Frame>(json)!, restoredFixture.Inputs());
        Assert.Equal(nextAlive, restoredAlive);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Paused_slots_still_draw_and_keep_native_alive_count_zero_without_reading_feedback()
    {
        Fixture fixture = new();
        NativeBattleRayBomb307Frame frame = fixture.Task(MainCallback, 1);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().MarkUnknown(0, 24).Write(0, BitConverter.GetBytes(1u)).Write(4, BitConverter.GetBytes((short)7)).Write(6, BitConverter.GetBytes((short)1024)).Write(8, BitConverter.GetBytes((short)100)).Write(10, BitConverter.GetBytes((short)200)).Write(12, BitConverter.GetBytes((short)1000));
        frame = frame with { State = frame.State with { Particles = NativeBattleEffectNodeContextMemory.Copy(row, 0, frame.State.Particles, 0, 24) } };
        string before = NativeBattleTestSnapshots.Json(frame.State.Particles);
        (frame, int alive) = NativeBattleRayBomb307Controller.AdvanceSlots(frame, fixture.Inputs(0x201));
        Draw draw = Assert.Single(fixture.Draws);
        Assert.Equal(0xCE1760u, draw.Source);
        Assert.Equal((ushort)7, draw.Phase);
        Assert.Equal(0, alive);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(frame.State.Particles));
        Assert.False(frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Group21_real_particle_row_births_retain_bank_owner_and_change_only_on_native_reuse()
    {
        (NativeBattleRayBomb307Frame Frame, List<NativeBattlePilotVisualIdentity> First, List<NativeBattlePilotVisualIdentity> Second) Run(bool capture)
        {
            Fixture fixture = new();
            var frame = fixture.Task(MainCallback, 13);
            var inputs = fixture.Inputs() with { CapturePresentation = capture };
            List<NativeBattlePilotVisualIdentity> identities = [];
            var execute = inputs.Execute;
            inputs = inputs with { Execute = (current, operation) =>
            {
                if (operation.PilotIdentity is { } identity)
                {
                    identities.Add(identity);
                }
                current = execute(current, operation);
                if (operation is NativeBattleRayBomb307DrawSprite180 sprite)
                {
                    current = current with { Staging = current.Staging!.Write(sprite.TemporaryAddress + 0x28, BitConverter.GetBytes((short)-1)) };
                }
                return current;
            } };
            frame = NativeBattleRayBomb307Controller.AdvanceMain(frame, ChildBank, fixture.Resources, inputs).Frame;
            frame = NativeBattleRayBomb307Controller.AdvanceSlots(frame, inputs).Frame;
            Assert.Empty(identities);
            frame = NativeBattleRayBomb307Controller.AdvanceSlots(frame, inputs).Frame;
            var first = identities.ToList();
            identities.Clear();
            frame = NativeBattleRayBomb307Controller.AdvanceMain(frame, ChildBank, fixture.Resources, inputs).Frame;
            frame = NativeBattleRayBomb307Controller.AdvanceSlots(frame, inputs).Frame;
            frame = NativeBattleRayBomb307Controller.AdvanceSlots(frame, inputs).Frame;
            return (frame, first, identities);
        }
        var native = Run(false);
        string before = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(4, captured.First.Count);
        Assert.Equal(4, captured.Second.Count);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(ParticlesAddress, captured.First[index].Bank);
            Assert.Equal(captured.First[index].Slot, captured.Second[index].Slot);
            Assert.NotEqual(captured.First[index].Generation, captured.Second[index].Generation);
            Assert.Equal(captured.First[index].ResourceGeneration, captured.Second[index].ResourceGeneration);
        }
    }

    [Fact]
    public void Group21_restored_live_rows_use_real_bank_ownership_and_discontinue_prior_capture_epochs()
    {
        Fixture fixture = new();
        var inputs = fixture.Inputs() with { CapturePresentation = true };
        var frame = NativeBattleRayBomb307Controller.AdvanceMain(fixture.Task(MainCallback, 13), ChildBank, fixture.Resources, inputs).Frame;
        Assert.Equal(1u, BitConverter.ToUInt32(frame.State.Particles.Read(0, 4)));
        var original = NativeBattleRayBomb307Visuals.Particle(frame, 0);
        string native = NativeBattleTestSnapshots.Json(frame);
        Assert.Equal(original, NativeBattleRayBomb307Visuals.Particle(frame, 0));
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRayBomb307Frame>(native)!;
        var restoredIdentity = NativeBattleRayBomb307Visuals.Particle(restored, 0);
        Assert.Equal(ParticlesAddress, restoredIdentity.Bank);
        Assert.Equal(0, restoredIdentity.Slot);
        Assert.Equal(0xCE1760, restoredIdentity.ResourceGeneration);
        Assert.NotEqual(original.Generation, restoredIdentity.Generation);
        Assert.Equal(native, NativeBattleTestSnapshots.Json(restored));
        NativeBattlePilotVisualGeometry geometry = new(original, default, default, default, default, 4);
        Assert.Same(geometry, NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { Identity = restoredIdentity, A = System.Numerics.Vector3.One }, 0.5f));
        Fixture fresh = new();
        var next = NativeBattleRayBomb307Controller.AdvanceMain(fresh.Task(MainCallback, 13), ChildBank, fresh.Resources, fresh.Inputs() with { CapturePresentation = true }).Frame;
        var newBank = NativeBattleRayBomb307Visuals.Particle(next, 0);
        Assert.NotEqual(original.Generation, newBank.Generation);
        Assert.NotEqual(restoredIdentity.Generation, newBank.Generation);
        Assert.Equal(native, NativeBattleTestSnapshots.Json(frame));
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private sealed record Draw(uint Source, ushort Phase, ushort Flags, NativeBattleEffectArena Context);
    private sealed class Fixture
    {
        public List<NativeBattleRayBomb307Operation> Operations { get; } = [];
        public List<Draw> Draws { get; } = [];
        public bool Complete { get; set; }
        public NativeBattleRayBomb307Resources Resources { get; } = new(new("unused", []), new("unused", []), [], [], [], Enumerable.Repeat(0, 10).ToImmutableArray(), Enumerable.Repeat(0, 10).ToImmutableArray(), [new(0, 0, 0, 0), new(0, 0, 0, 0)], [new(4096, 0, 0, 0), new(0, 0, 4096, 0)], []);
        public NativeBattleRayBomb307Frame Task(uint callback, ushort age)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
            (root, _) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 0)) });
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            (children, _) = children.Append(callback);
            children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age)) });
            NativeBattleOriginalEffectState effect = new(307, 307, new(307, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
            NativeBattleEffectArena particles = NativeBattleEffectArena.Clear().MarkUnknown(0, ParticlesLength);
            for (int row = 0; row < 100; row++)
            {
                particles = particles.Write(row * 24, BitConverter.GetBytes(0u));
            }
            NativeBattleRayBomb307State state = NativeBattleRayBomb307State.FromUnknown().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(0x2242574, 0).WriteWord(0x2242580, 100).WriteWord(0x2242582, 200).WriteWord(0x2242584, 1000).WriteWord(0x2242568, 111).WriteWord(0x224256A, 0).WriteWord(0x224256C, 333) with { Particles = particles };
            return new(NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = Camera() }, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        }
        public NativeBattleRayBomb307Inputs Inputs(uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), flags, actor => new(actor, 0) { GeometryWorldMatrix = Identity() with { X = 10, Y = 20, Z = 30 }, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096) }, (_, _, _) => NativeBattlePackedMatrix32.FromMatrix(Identity(), null), (frame, _) => new(frame, new(111, 0, 333)), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), Execute) { ReadView = _ => Camera() };
        private NativeBattleRayBomb307Frame Execute(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattleRayBomb307RestoreTexture restore)
            {
                uint address = restore.CompletionAddress - 14;
                Assert.Equal((ushort)0, NativeBattleRayBomb307Controller.Read(frame, address).ReadWord(14));
                if (Complete)
                {
                    frame = NativeBattleRayBomb307Controller.Write(frame, address, NativeBattleRayBomb307Controller.Read(frame, address).WriteWord(14, 1));
                }
            }
            if (operation is NativeBattleRayBomb307DrawSprite180 sprite)
            {
                NativeBattleEffectArena context = View(frame.Staging!, sprite.TemporaryAddress, 180);
                Draws.Add(new(sprite.SourceAddress, sprite.Phase, sprite.Flags, context));
                frame = frame with { Staging = frame.Staging!.Write(sprite.TemporaryAddress + 0x28, BitConverter.GetBytes((short)0)) };
            }
            if (operation is NativeBattleRayBomb307DrawMesh88 mesh)
            {
                Draws.Add(new(mesh.SourceAddress, 0, 0, View(frame.Staging!, mesh.TemporaryAddress, 88)));
            }
            return frame;
        }
    }
    private static NativeBattleEffectArena View(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(Identity(), (ushort)0xCAFE);
}
