using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect323Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect323ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Fact]
    public void First_root_visit_uses_second_packet_bank_without_consuming_children()
    {
        Harness harness = new();
        NativeBattleEffect323Step result = AdvanceRoot659B30(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(1u, Read(result.Frame, RootBank).ReadDword(16));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Root_waits_for_current_main_survivor_and_scans_it_before_retirement()
    {
        Harness harness = new();
        NativeBattleEffect323Frame frame = AddMain(Frame(20), 0);
        NativeBattleEffect323Step result = AdvanceRoot659B30(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(1, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(21, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Retirement_restores_exclusive_banks_and_current_common_unknownness()
    {
        NativeBattleEffect323Frame frame = Frame(70);
        frame = WriteBytes(frame, Arena + 0xE64, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        NativeBattleEffect323Frame result = Advance(frame, null!, new Harness().Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        (byte[] bytes, bool[] known) = result.Memory.Arena!.ReadSnapshot(0xE64, 4);
        Assert.Equal(new[] { true, true, false, true }, known);
        Assert.Equal(new byte[] { 1, 2, 4 }, bytes.Where((_, index) => known[index]).ToArray());
        NativeBattleEffect323Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect323Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(known, restored.Memory.Arena!.ReadSnapshot(0xE64, 4).Known);
    }

    [Fact]
    public void Recipients_use_native_pairwise_sort_and_word_wrapped_centroid()
    {
        Harness harness = new();
        harness.SetContacts(7, 0, 1, 2);
        harness.ModelXs = [100, 100, 200, 0, 0, 0, 0];
        harness.Anchors = [new(100, 30000, 30000), new(100, 30000, 30000), new(200, 30000, 30000)];
        NativeBattleEffect323Frame result = CaptureRecipients(AddMain(Frame(0), 0), Arena, harness.Inputs());
        NativeBattleModulePoolTask task = Read(result, Arena);
        Assert.Equal(new[] { ModelAddress(2), ModelAddress(1), ModelAddress(0) }, Enumerable.Range(0, 3).Select(index => task.ReadDword(0x20 + index * 4)).ToArray());
        Assert.Equal(new[] { ModelAddress(2), ModelAddress(1), ModelAddress(0) }, harness.AnchorModels);
        Assert.Equal((short)8154, Signed(task, 0x16));
        Assert.Equal((short)7154, Signed(task, 0x18));
        Assert.Equal((short)-2000, Signed(task, 0x12));
        Assert.Equal((short)2000, Signed(task, 0x14));
        Assert.Equal(777u, result.State.ReadDword(InitializerGlobal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Unsupported_recipient_count_cannot_resize_native_arrays(int count)
    {
        Harness harness = new();
        harness.SetContacts(0, Enumerable.Range(0, count).ToArray());
        Assert.Throws<NotSupportedException>(() => CaptureRecipients(AddMain(Frame(0), 0), Arena, harness.Inputs()));
    }

    [Theory]
    [InlineData(12, 1, 0, 136)]
    [InlineData(26, 1, 0, 3958)]
    [InlineData(27, 1, 1, 256)]
    [InlineData(34, 1, 1, 3840)]
    [InlineData(35, 3, 2, 204)]
    [InlineData(44, 3, 4, 3071)]
    [InlineData(45, 3, 5, 204)]
    [InlineData(63, 3, 6, 3480)]
    public void Path_parameters_preserve_all_four_native_ranges(int age, int count, int segment, int parameter)
    {
        Assert.Equal((segment, parameter), PathParameter(age, count));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void All_52_samples_stay_within_actual_initialized_controls(int count)
    {
        int maximum = 0;
        for (int age = 12; age <= 63; age++)
        {
            (int segment, int parameter) = PathParameter(age, count);
            Assert.InRange(parameter, 0, 4095);
            maximum = Math.Max(maximum, segment + 3);
        }
        Assert.Equal(count + 6, maximum);
    }

    [Fact]
    public void Controls_preserve_partial_writers_and_native_truncated_midpoint()
    {
        Harness harness = new();
        harness.SetContacts(0, 0, 1, 2);
        harness.Anchors = [new(0, 30, 60), new(0, 30, 60), new(0, 30, 60)];
        NativeBattleEffect323Frame frame = CaptureRecipients(AddMain(Frame(0), 0), Arena, harness.Inputs());
        NativeBattleEffectArena caster = PointView(new(-301, 40, -51)).Write(6, BitConverter.GetBytes((ushort)0xCAFE));
        frame = Write(frame, Arena, CopyTask(Read(frame, Arena), 0x9C, caster, 8));
        frame = WriteBytes(frame, Arena + 0x5A, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        NativeBattleEffect323Frame result = InitializeControls(frame, Arena);
        NativeBattleModulePoolTask task = Read(result, Arena);
        Assert.Equal(new NativeBattleVector(-301, 40, -551), Vector(TaskView(task, 0x44, 8)));
        Assert.Equal(new NativeBattleVector(2000, -970, -940), Vector(TaskView(task, 0x54, 8)));
        Assert.Equal(new[] { false, false }, TaskView(task, 0x5A, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(new NativeBattleVector(-1150, -1470, -495), Vector(TaskView(task, 0x7C, 8)));
        Assert.Equal(caster.Read(0, 8), TaskView(task, 0x8C, 8).Read(0, 8));
    }

    [Fact]
    public void Bone_refresh_at_29_occurs_after_sampling_and_tilt_store()
    {
        Harness harness = new();
        NativeBattleEffect323Frame frame = AddMain(Frame(10), 29);
        harness.OnSample = (current, _, _, _) =>
        {
            Assert.Equal(0, harness.PointQueries);
            Assert.Equal(77, Read(current, Arena).ReadWord(0x1C));
            throw new SampleObservedException();
        };
        Assert.Throws<SampleObservedException>(() => AdvanceMain659DD0(frame, Arena, null!, harness.Inputs()));
    }

    [Fact]
    public void Age_11_uses_current_bone_full_frame_and_full_packed_history()
    {
        Harness harness = new();
        NativeBattleEffect323Step result = AdvanceMain659DD0(AddMain(Frame(10), 11), Arena, null!, harness.Inputs());
        Assert.Equal(1, harness.PointQueries);
        Assert.Equal(12, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(1024, Read(result.Frame, Arena).ReadWord(0x1C));
        Assert.Equal(new NativeBattleVector(100, -200, 300), Vector(TaskView(Read(result.Frame, Arena), 0xA4, 8)));
        Assert.Equal(TaskView(Read(result.Frame, Arena), 0xA4, 8).Read(0, 8), TaskView(Read(result.Frame, Arena), 0xAC, 8).Read(0, 8));
        Assert.Equal(888u, result.Frame.State.ReadDword(InitializerGlobal));
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(27, 3840)]
    [InlineData(28, 4096)]
    [InlineData(61, 4096)]
    [InlineData(62, 3840)]
    [InlineData(63, 3584)]
    public void Mesh_strength_preserves_full_plateau_and_actual_final_value(int age, int strength)
    {
        Assert.Equal(strength, MeshStrength(age));
    }

    [Fact]
    public void World_rotation_keeps_both_noncommuting_products_and_actual_translation()
    {
        NativeBattleVector previous = new(100, 4, 900);
        NativeBattleVector current = new(20, -10, 500);
        NativeBattleActionMatrix x = NativeBattlePandemona291Restoration.Rotation701220(700);
        NativeBattleActionMatrix z = NativeBattleGfMatrices.Euler701310(new(0, 0, 1100));
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(80, 0, -400).Matrix;
        NativeBattleActionMatrix actual = BuildWorld(previous, current, 700, 1100);
        Assert.Equal(NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Multiply56C090(z, x)).Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.NotEqual(NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(basis, x), z).Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((20, -10, 500), (actual.X, actual.Y, actual.Z));
    }

    [Fact]
    public void Failed_act_allocation_still_delivers_every_matching_contact_without_particle_rng()
    {
        Harness harness = new();
        harness.SetContacts(7, 0, 0, 1);
        NativeBattleEffect323Frame frame = AddMain(Frame(10), 35);
        frame = Write(frame, Arena, Read(frame, Arena).WriteWord(14, 3).WriteDword(0x20, ModelAddress(0)));
        for (int index = 0; index < 3; index++)
        {
            (frame, _) = Append(frame, ProgrammeDescriptor, 0x65A720);
        }
        for (int index = 0; index < 64; index++)
        {
            (frame, _) = Append(frame, ParticleDescriptor, 0x65A660);
        }
        NativeBattleEffect323Frame result = TriggerRecipient(frame, Arena, 35, PointView(new(-1, 0, 0)), null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect323Operation[] { new NativeBattleEffect323Contact506690(7), new NativeBattleEffect323Contact506690(8) }, harness.Operations);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(1, Read(result, Arena).ReadWord(0x10));
        Assert.Empty(harness.AnchorModels);
    }

    [Fact]
    public void Equal_x_does_not_trigger_contact_or_advance_recipient_cursor()
    {
        Harness harness = new();
        NativeBattleEffect323Frame frame = AddMain(Frame(10), 35);
        frame = Write(frame, Arena, Read(frame, Arena).WriteDword(0x20, ModelAddress(0)));
        NativeBattleEffect323Frame result = TriggerRecipient(frame, Arena, 35, PointView(new(0, 0, 0)), null!, harness.Inputs());
        Assert.Equal(0, Read(result, Arena).ReadWord(0x10));
        Assert.Equal(frame.Random, result.Random);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Single_free_particle_uses_two_draws_and_retains_velocity_w()
    {
        NativeBattleEffect323Frame frame = AddMain(Frame(10), 35);
        for (int index = 0; index < 63; index++)
        {
            (frame, _) = Append(frame, ParticleDescriptor, 0x65A660);
        }
        uint final = Arena + 0xE54 + 63 * 0x24;
        frame = WriteBytes(frame, final + 0x1A, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x12 }).MarkUnknown(1, 1), 2);
        NativeBattleEffect323Frame result = BirthParticles(frame, Arena, PointView(new(0, 0, 0)));
        Assert.Equal(frame.Random.Draw(out _).Draw(out _), result.Random);
        Assert.Equal(new[] { true, false }, TaskView(Read(result, final), 0x1A, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal((byte)0x34, TaskView(Read(result, final), 0x1A, 2).ReadSnapshot(0, 2).Data[0]);
        Assert.Equal(0, Read(result, final).ReadWord(0x12));
    }

    private static NativeBattleEffect323Frame AddMain(NativeBattleEffect323Frame frame, int age)
    {
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x2C);
        return Write(frame, main, Read(frame, main).WriteWord(12, (ushort)age).WriteWord(14, 1));
    }

    private static NativeBattleEffect323Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(15, initialized ? (byte)1 : (byte)0));
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                pools = pools.Add(NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity));
            }
        }
        NativeBattleEffect323State state = NativeBattleEffect323State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(4));
        NativeBattleOriginalEffectState effect = new(323, 322, new(323, Entry, Initializer), new(4, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools), effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class SampleObservedException : Exception
    {
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect323Operation> Operations { get; } = [];
        public List<uint> AnchorModels { get; } = [];
        public short[] ModelXs { get; set; } = new short[7];
        public NativeBattleVector[] Anchors { get; set; } = new NativeBattleVector[7];
        public int PointQueries { get; private set; }
        public Action<NativeBattleEffect323Frame, NativeBattleEffectArena, int, int>? OnSample { get; set; }
        public NativeBattleActionPacketsState Packets { get; private set; } = NativeBattleActionPacketsState.Fresh();

        public void SetContacts(int first, params int[] actors)
        {
            byte[] header = new byte[20];
            header[16] = (byte)actors.Length;
            ImmutableArray<NativeBattleContactPacket?> contacts = Packets.Contacts;
            for (int index = 0; index < actors.Length; index++)
            {
                contacts = contacts.SetItem(first + index, new(new((byte)actors[index], 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            Packets = Packets with { Headers = Packets.Headers.SetItem(0, new(header.ToImmutableArray(), 0, first, null)), Contacts = contacts };
        }

        public NativeBattleEffect323Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets, (_, model) => NativeBattleEffectArena.Clear().Write(0x1C, BitConverter.GetBytes(ModelXs[(model - ModelAddress(0)) / 156])), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, selector, fraction, retained) =>
        {
            Assert.Equal(24, selector);
            Assert.Equal(2048, fraction);
            PointQueries++;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 888) }, PutPoint(retained, 0, new(100, -200, 300)));
        }, (frame, model, retained) =>
        {
            AnchorModels.Add(model);
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 777) }, PutPoint(retained, 0, Anchors[(model - ModelAddress(0)) / 156]));
        }, (frame, controls, segment, parameter, retained) =>
        {
            OnSample?.Invoke(frame, controls, segment, parameter);
            return new(frame, retained);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
