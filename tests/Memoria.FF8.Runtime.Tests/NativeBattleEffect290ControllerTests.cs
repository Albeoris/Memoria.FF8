using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect290ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0x1CDC;
    private const uint Script = Arena;

    [Fact]
    public void Root_first_visit_selects_second_bank_before_initialization_and_next_visit_keeps_retained_lcg()
    {
        Harness harness = new();
        NativeBattleEffect290Frame frame = Frame(rootAge: 0, mainAge: null);
        foreach (uint descriptor in frame.Effect.Pools.Skip(1).Select(pool => pool.DescriptorAddress).ToArray())
        {
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 0));
        frame = frame with { State = frame.State.WriteDword(InternalRandomGlobal, 777) };
        NativeBattleEffect290Step first = AdvanceRoot675340(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Single(first.Frame.Effect.Pools);
        Assert.Empty(harness.Operations);
        NativeBattleEffect290Step initialized = AdvanceRoot675340(first.Frame, RootBank, Resources(), harness.Inputs());
        Assert.Equal(777u, initialized.Frame.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(Arena + FirstPacketOffset, initialized.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(6, initialized.Frame.Effect.Pools.Length);
        Assert.Equal(19, initialized.Frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ScriptDescriptor).Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(1, Read(initialized.Frame, Main).ReadWord(12));
        Assert.Equal(new NativeBattleEffect290Music47E3C0(240), harness.Operations[0]);
        Assert.Contains(new NativeBattleEffect290InitializeIo534110(IoOffset), harness.Operations);
    }

    [Fact]
    public void Script_vector_words_wrap_and_yield_persists_only_the_actual_next_cursor()
    {
        NativeBattleEffect290Resources resources = Resources(0x106E648, [1, 0x1000, 32760, -10, 3, 0x1001, 20, 20, -4, 0x10FF, 0x1100]);
        NativeBattleEffect290Frame frame = BirthScript675650(Frame(), 0x106E648, resources);
        uint point = Arena + 0x1D04;
        frame = WriteBytes(frame, point + 6, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x12 }).MarkUnknown(1, 1), 2);
        NativeBattleEffect290Step step = AdvanceScript6756A0(frame, Script, resources, new Harness().Inputs());
        Assert.Equal(0, step.Result);
        Assert.Equal(-32756, Word290(ReadBytes(step.Frame, point, 2), 0));
        Assert.Equal(10, Word290(ReadBytes(step.Frame, point + 2, 2), 0));
        Assert.Equal(-1, Word290(ReadBytes(step.Frame, point + 4, 2), 0));
        Assert.Equal(new[] { true, false }, ReadBytes(step.Frame, point + 6, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(0x106E65Cu, Read(step.Frame, Script).ReadDword(16));
        step = AdvanceScript6756A0(step.Frame, Script, resources, new Harness().Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(0x106E65Cu, Read(step.Frame, Script).ReadDword(16));
    }

    [Fact]
    public void Loop_decrements_canonical_source_word_once_and_warm_invocation_keeps_zero_after_json()
    {
        NativeBattleEffect290Resources resources = Resources(0x106E648, [1, 0x10FF, 0x10F0, 2, -1, 0x1100]);
        NativeBattleEffect290Frame frame = BirthScript675650(Frame(), 0x106E648, resources);
        Harness harness = new();
        for (int visit = 0; visit < 3; visit++)
        {
            NativeBattleEffect290Step step = AdvanceScript6756A0(frame, Script, resources, harness.Inputs());
            Assert.Equal(0, step.Result);
            frame = step.Frame;
        }
        NativeBattleEffect290Step complete = AdvanceScript6756A0(frame, Script, resources, harness.Inputs());
        Assert.Equal(2, complete.Result);
        Assert.Equal(new byte[2], complete.Frame.Staging!.Read(0x106E64E, 2));
        Assert.Equal(new byte[] { 2, 0 }, resources.OriginalScriptCount(0x106E64E));
        NativeBattleFileStaging staging = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(complete.Frame.Staging)))!);
        NativeBattleEffect290State state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect290State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(complete.Frame.State)))!);
        frame = BirthScript675650(complete.Frame with { Staging = staging, State = state }, 0x106E648, resources);
        uint warm = Script + 36;
        NativeBattleEffect290Step warmYield = AdvanceScript6756A0(frame, warm, resources, harness.Inputs());
        Assert.Equal(0, warmYield.Result);
        Assert.Equal(2, AdvanceScript6756A0(warmYield.Frame, warm, resources, harness.Inputs()).Result);
        Assert.False(state.ScriptSourcePending);
        Assert.Equal(new byte[2], staging.Read(0x106E64E, 2));
    }

    [Fact]
    public void Script_bounce_uses_signed_quarter_minus_velocity_and_separate_angular_velocity()
    {
        NativeBattleEffect290Resources resources = Resources(0x106E664, [2, 0x1000, 1, -2, 3, 0x1002, 4, 7, -5, 0x1004, 0x1010, 1, 2, 3, 0x1012, 4, 5, 6, 0x1014, 0x1100]);
        NativeBattleEffect290Frame frame = BirthScript675650(Frame(), 0x106E664, resources);
        NativeBattleEffect290Step step = AdvanceScript6756A0(frame, Script, resources, new Harness().Inputs());
        uint point = Arena + 0x1D14;
        Assert.Equal(2, step.Result);
        Assert.Equal(5, Word290(ReadBytes(step.Frame, point, 2), 0));
        Assert.Equal(0, Word290(ReadBytes(step.Frame, point + 2, 2), 0));
        Assert.Equal(-2, Word290(ReadBytes(step.Frame, point + 4, 2), 0));
        Assert.Equal(-6, Signed(Read(step.Frame, Script), 22));
        Assert.Equal(new short[] { 5, 7, 9 }, Enumerable.Range(0, 3).Select(axis => Word290(ReadBytes(step.Frame, point + 8 + (uint)(axis * 2), 2), 0)).ToArray());
    }

    [Theory]
    [InlineData(0x1080, 16, 0)]
    [InlineData(0x1081, 8, 0)]
    [InlineData(0x1082, 8, 8)]
    public void Script_copy_preserves_both_padding_words_and_per_byte_knownness(short opcode, int length, int offset)
    {
        NativeBattleEffect290Resources resources = Resources(0x106E648, [1, opcode, 2, 0x1100]);
        NativeBattleEffect290Frame frame = BirthScript675650(Frame(), 0x106E648, resources);
        uint source = Arena + 0x1D14;
        uint destination = Arena + 0x1D04;
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(1, 16).Select(value => (byte)value).ToArray()).MarkUnknown(7, 1).MarkUnknown(14, 1);
        frame = WriteBytes(frame, source, bytes, 16);
        frame = AdvanceScript6756A0(frame, Script, resources, new Harness().Inputs()).Frame;
        Assert.Equal(bytes.ReadSnapshot(offset, length).Known, ReadBytes(frame, destination + (uint)offset, length).ReadSnapshot(0, length).Known);
        Assert.Equal(bytes.Read(offset, Math.Min(length, 6)), ReadBytes(frame, destination + (uint)offset, Math.Min(length, 6)).Read(0, Math.Min(length, 6)));
    }

    [Fact]
    public void Inline_calls_keep_signed_fields_and_never_read_target_skipped_word()
    {
        const uint source = 0x106E6A4;
        NativeBattleEffect290Resources resources = Resources(source, [1, 0x1090, 1, 3, -1, 100, -100, 5000, 10922, 0x10A0, 2, 4096, 123, 1024, 0x1100], source + 24);
        Harness harness = new();
        NativeBattleEffect290Frame frame = BirthScript675650(Frame(), source, resources);
        NativeBattleEffect290Step step = AdvanceScript6756A0(frame, Script, resources, harness.Inputs());
        Assert.Equal(2, step.Result);
        NativeBattleEffect290SelectFragments675AA0 select = Assert.IsType<NativeBattleEffect290SelectFragments675AA0>(harness.Operations[0]);
        Assert.Equal(new short[] { 1, 3 }, select.BoneSelectors);
        Assert.Equal(-100, select.NormalSpeed);
        Assert.Equal(100, select.ChunkLimit);
        Assert.Equal(new NativeBattleEffect290BirthTarget676340(ModelAddress(3), Arena + 0x1CF4, 2, 4096, 1024), harness.Operations[1]);
    }

    [Fact]
    public void Record_allocator_uses_free_hint_before_earlier_free_rows_and_retains_all_unwritten_bytes()
    {
        NativeBattleEffect290Frame frame = Frame();
        uint row = Main + 0x1D8 + 7 * 124;
        frame = Write(frame, Main, Read(frame, Main).WriteDword(0x64D8, row));
        frame = WriteBytes(frame, row + 28, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(1, 1), 4);
        (NativeBattleEffect290Frame result, uint selected) = AllocateRecord675FD0(frame, Main);
        Assert.Equal(row, selected);
        Assert.Equal(row + 124, Read(result, Main).ReadDword(0x64D8));
        Assert.Equal(14u, result.State.ReadDword(InternalRandomGlobal));
        Assert.Equal(0x1067400u, Dword290(ReadBytes(result, row + 24, 4), 0));
        Assert.Equal(new[] { true, false, true, true }, ReadBytes(result, row + 28, 4).ReadSnapshot(0, 4).Known);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Full_record_bank_does_not_advance_internal_or_crt_random()
    {
        NativeBattleEffect290Frame frame = Frame();
        for (int row = 0; row < 192; row++)
        {
            frame = WriteBytes(frame, Main + 0x1DE + (uint)(row * 124), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)1)), 2);
        }
        frame = frame with { State = frame.State.WriteDword(InternalRandomGlobal, 12345) };
        (NativeBattleEffect290Frame result, uint selected) = AllocateRecord675FD0(frame, Main);
        Assert.Equal(0u, selected);
        Assert.Same(frame, result);
        Assert.Equal(12345u, result.State.ReadDword(InternalRandomGlobal));
    }

    [Theory]
    [InlineData(0u, 14)]
    [InlineData(12345u, 3043)]
    public void Internal_generator_is_independent_of_crt_and_has_no_per_entry_reset(uint seed, int expected)
    {
        NativeBattleEffect290Frame frame = Frame() with { State = Frame().State.WriteDword(InternalRandomGlobal, seed) };
        NativeBattleEffect290RandomStep result = InternalRandom676080(frame);
        Assert.Equal(expected, result.Value);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Main_late_pair_passes_caller56_with_unknown_unconsumed_scale_lanes_and_reload_keeps_body_mutations()
    {
        Harness harness = new() { MutateMain = true };
        NativeBattleEffect290Frame frame = Frame(mainAge: 30);
        NativeBattleEffect290Frame result = AdvanceMain6767B0(frame, Main, null!, harness.Inputs()).Frame;
        Assert.Equal(new[] { Main + 0x65F0, Main + 0x681C }, harness.Programmes.Select(call => call.Workspace).ToArray());
        Assert.All(harness.Programmes, call =>
        {
            Assert.Equal(0u, Dword290(call.Caller, 0x2C));
            Assert.Equal(0u, Dword290(call.Caller, 0x30));
            Assert.Equal(Arena + MorphOffset, Dword290(call.Caller, 0x34));
            Assert.All(call.Caller.ReadSnapshot(0x20, 12).Known, known => Assert.False(known));
        });
        Assert.Equal((ModelAddress(3), 1, 1024), Assert.Single(harness.Points));
        Assert.Equal(0x12345678u, Read(result, Main).ReadDword(0x64D8));
        Assert.Equal(31, Read(result, Main).ReadWord(12));
        Assert.Equal(new NativeBattleEffect290AdvanceFragments6774C0(Main), harness.Operations[^1]);
    }

    [Fact]
    public void First_caller_preserves_dword_y_subtraction_and_main_sprite_draws_before_body()
    {
        Harness harness = new() { Point = new(100, -32760, 4000) };
        NativeBattleEffect290Frame result = AdvanceMain6767B0(Frame(mainAge: 0), Main, Resources(), harness.Inputs()).Frame;
        var call = Assert.Single(harness.Programmes);
        Assert.Equal(Main + 0x64E0, call.Workspace);
        Assert.Equal(-33760, BinaryPrimitives.ReadInt32LittleEndian(call.Caller.Read(24, 4)));
        Assert.Equal(-512, BinaryPrimitives.ReadInt32LittleEndian(call.Caller.Read(0x30, 4)));
        Assert.IsType<NativeBattleEffect290DrawBatch>(harness.Operations[0]);
        Assert.Equal(1, Read(result, Main).ReadWord(12));
    }

    [Fact]
    public void Main_color_blend_does_not_require_opaque_saved_alpha_to_be_known()
    {
        Harness harness = new();
        NativeBattleEffect290Frame frame = Frame(mainAge: 19);
        frame = WriteBytes(frame, Main + 20, NativeBattleEffectArena.Clear().Write(0, new byte[] { 80, 100, 120, 4 }).MarkUnknown(3, 1), 4);
        NativeBattleEffect290Step step = AdvanceMain6767B0(frame, Main, null!, harness.Inputs());
        NativeBattleEffect290WriteModelBytes[] colors = harness.Operations.OfType<NativeBattleEffect290WriteModelBytes>().ToArray();
        Assert.Equal(3, colors[0].Length);
        Assert.Equal(44, colors[0].Offset);
        Assert.Equal(47, colors[1].Offset);
        Assert.Equal(new byte[] { 0x32 }, colors[1].Data.Read(0, 1));
        Assert.False(TaskView(Read(step.Frame, Main), 23, 1).ReadSnapshot(0, 1).Known[0]);
    }

    [Fact]
    public void Failed_particle_append_still_queries_point_and_failed_children_consume_no_crt()
    {
        Harness harness = new();
        NativeBattleEffect290Frame frame = Frame();
        for (int index = 0; index < 16; index++)
        {
            (frame, _) = Append(frame, ParticleDescriptor, 0x6775F0);
        }
        for (int index = 0; index < 32; index++)
        {
            (frame, _) = Append(frame, SpriteDescriptor, 0x677550);
        }
        NativeBattleEffect290Frame result = BirthSmallChildren290(frame, ModelAddress(3), NativeBattleEffectArena.Clear().MarkUnknown(0, 8), harness.Inputs());
        Assert.Single(harness.Points);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(321u, result.State.ReadDword(InitializerGlobal));
    }

    [Theory]
    [InlineData(89, 0)]
    [InlineData(90, 2)]
    public void Main_retires_at_old_ninety_before_any_operations(int age, int expected)
    {
        Harness harness = new();
        NativeBattleEffect290Frame frame = Frame(mainAge: age);
        NativeBattleEffect290Step result = AdvanceMain6767B0(frame, Main, null!, harness.Inputs());
        Assert.Equal(expected, result.Result);
        if (age == 90)
        {
            Assert.Same(frame, result.Frame);
            Assert.Empty(harness.Operations);
        }
        else
        {
            Assert.Equal(90, Read(result.Frame, Main).ReadWord(12));
        }
    }

    [Fact]
    public void Real_io_wait_holds_age_and_ignores_auxiliary_survivors_while_banks_keep_alternating()
    {
        Harness harness = new();
        NativeBattleEffect290Frame frame = Frame(rootAge: 91, mainAge: null);
        (frame, _) = Append(frame, ParticleDescriptor, 0x6775F0);
        NativeBattleEffect290Step wait = AdvanceRoot675340(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, wait.Result);
        Assert.Equal(91, Read(wait.Frame, RootBank).ReadWord(12));
        Assert.Equal(Arena + SecondPacketOffset, wait.Frame.State.ReadDword(CursorGlobal));
        harness.Complete = true;
        NativeBattleEffect290Step complete = AdvanceRoot675340(wait.Frame, RootBank, null!, harness.Inputs());
        Assert.Equal(2, complete.Result);
        Assert.Equal(Arena + FirstPacketOffset, complete.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(91, Read(complete.Frame, RootBank).ReadWord(12));
        Assert.Single(complete.Frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ParticleDescriptor).Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal(2, harness.Operations.OfType<NativeBattleEffect290AdvanceIo534210>().Count());
    }

    [Fact]
    public void Root_queue_at_eighty_follows_scans_and_final_orphans_return_knownness_to_common()
    {
        Harness harness = new() { Complete = true };
        NativeBattleEffect290Frame frame = Frame(rootAge: 80, mainAge: null);
        (frame, uint particle) = Append(frame, ParticleDescriptor, 0x6775F0);
        frame = WriteBytes(frame, particle + 12, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        NativeBattleEffect290Frame result = Advance(frame, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect290QueueFile5341D0(360, Arena + File360Offset, 0), harness.Operations[0]);
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        Assert.Equal(new[] { true, true, false, true }, result.Memory.Arena!.ReadSnapshot((int)(particle - Arena + 12), 4).Known);
        Assert.Equal(4, result.Memory.Arena.Read((int)(particle - Arena + 15), 1)[0]);
    }

    private static NativeBattleEffect290Frame Frame(int rootAge = 2, int? mainAge = 50)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteDword(12, (uint)rootAge | 0x01000000).WriteDword(16, 0));
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>();
        pools.Add(root);
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity);
            for (int slot = 0; slot < shape.Capacity; slot++)
            {
                pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[shape.Stride - 12]));
            }
            if (shape.Descriptor == MainDescriptor && mainAge is int age)
            {
                (pool, _) = pool.Append(MainCallback);
                pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, (ushort)age).WriteDword(0x64D8, Main + 0x1D8));
            }
            pools.Add(pool);
        }
        NativeBattleEffect290State state = NativeBattleEffect290State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        NativeBattleOriginalEffectState effect = new(290, 289, new(290, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutable() };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools.Skip(1).ToImmutableArray());
        return new(memory, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
    }

    private static NativeBattleEffect290Resources Resources(uint scriptAddress = 0x106E648, short[]? words = null, uint? unreadWord = null)
    {
        const uint first = 0x106E648;
        byte[] source = new byte[0x24C];
        if (words is not null)
        {
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan((int)(scriptAddress - first) + index * 2), words[index]);
            }
        }
        ImmutableDictionary<uint, ImmutableArray<byte>> fronts = unreadWord is uint hole
            ? ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(first, source.AsSpan(0, (int)(hole - first)).ToArray().ToImmutableArray()).Add(hole + 2, source.AsSpan((int)(hole + 2 - first)).ToArray().ToImmutableArray())
            : ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(first, source.ToImmutableArray());
        byte[] sprite = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
        for (int frame = 0; frame < 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 44);
        }
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(42), -1);
        return new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(MainSpriteAddress, new(5, MainSpriteAddress, 0, 0, sprite.ToImmutableArray())), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, fronts);
    }

    private sealed class Harness
    {
        public bool Complete;
        public bool MutateMain;
        public NativeBattleVector Point = new(100, 200, 4000);
        public List<NativeBattleEffect290Operation> Operations = [];
        public List<(uint, int, int)> Points = [];
        public List<(uint Workspace, NativeBattleEffectArena Caller)> Programmes = [];

        public NativeBattleEffect290Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, model, selector, fraction, retained) =>
        {
            Points.Add((model, selector, fraction));
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 321) }, PutPoint(retained, 0, Point));
        }, (frame, _) => new(frame, Complete), frame => new(frame, 0), (frame, operation) =>
        {
            Operations.Add(operation);
            return MutateMain && operation is NativeBattleEffect290DrawBody676B40 ? Write(frame, Main, Read(frame, Main).WriteDword(0x64D8, 0x12345678)) : frame;
        })
        {
            AdvanceLower = (frame, _, _, _) => new(frame, 0),
            AdvanceProgramme = (frame, workspace, _, caller, _, _) =>
            {
                Programmes.Add((workspace, caller));
                return new(frame, 1);
            }
        };
    }
}
