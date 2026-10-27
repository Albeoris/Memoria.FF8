using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect313ControllerTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(512u)]
    [InlineData(513u)]
    public void Paused_main_has_no_birth_random_service_or_age_side_effect(uint flags)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect313Frame frame = Frame(0, 26);
        NativeBattleEffect313Step result = AdvanceMain59B180(frame, ChildBank, null!, harness.Inputs());
        Assert.Same(frame, result.Frame);
        Assert.Equal(0, result.Result);
        Assert.Empty(harness.Operations);
    }

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(1, 0x8000u)]
    [InlineData(2, 0u)]
    public void Root_alternates_current_common_even_when_main_is_paused(int age, uint offset)
    {
        NativeBattleEffect313Step result = AdvanceRoot59CA20(Frame(age, 80), RootBank, null!, new Harness { Flags = 0x201 }.Inputs());
        Assert.Equal(0x20DFAB8u + offset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(age + 1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(80, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Zero_uses_actual_f0_fraction_and_current_rotation_preserving_individual_w_bytes()
    {
        Harness harness = new();
        NativeBattleEffect313Frame frame = Frame(0, 0);
        frame = frame with { State = frame.State.WriteView(CenterAddress + 6, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x56 }).MarkUnknown(1, 1), 2) };
        NativeBattleEffect313Frame result = AdvanceMain59B180(frame, ChildBank, null!, harness.Inputs()).Frame;
        Assert.Equal((ModelAddress(3), 0xF0, 4096), Assert.Single(harness.Points));
        Assert.Equal((ushort)0x15, harness.TargetMask);
        Assert.Equal((ushort)100, result.State.ReadWord(CenterAddress));
        Assert.Equal((ushort)400, result.State.ReadWord(CenterAddress + 2));
        Assert.Equal((ushort)350, result.State.ReadWord(CenterAddress + 4));
        Assert.Equal(new[] { true, false }, result.State.ReadView(CenterAddress + 6, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(789u, result.State.ReadDword(InitializerGlobal));
        Assert.Equal(new NativeBattleEffect313Stage5713E0(0, 0), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Paired_emitters_rotate_horizontal_target_vector_and_keep_target_y_without_random()
    {
        NativeBattleEffect313Frame frame = Frame(0, 74);
        frame = frame with { State = frame.State.WriteView(CenterAddress, PutPoint(NativeBattleEffectArena.Clear(), 0, new(100, 5, 300)), 8).WriteView(TargetPointAddress, PutPoint(NativeBattleEffectArena.Clear(), 0, new(1100, 70, 2300)), 8) };
        NativeBattleEffect313Frame result = AdvanceMain59B180(frame, ChildBank, null!, new Harness().Inputs()).Frame;
        NativeBattleModulePoolTask[] tasks = result.Effect.Pools[1].Slots.Where(task => task.Callback == 0x59BDF0).ToArray();
        Assert.Equal(2, tasks.Length);
        Assert.All(tasks, task => Assert.Equal(70, Signed(task, 0x12)));
        Assert.Equal(200, Signed(tasks[0], 0x10) + Signed(tasks[1], 0x10));
        Assert.Equal(600, Signed(tasks[0], 0x14) + Signed(tasks[1], 0x14));
        Assert.Equal(new short[] { 70, -70 }, tasks.Select(task => Signed(task, 0x18)).ToArray());
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Age99_births_five_groups_and_appends_real_root_upload_task_after_them()
    {
        NativeBattleEffect313Frame frame = Frame(0, 99);
        NativeBattleEffect313Frame result = AdvanceMain59B180(frame, ChildBank, Resources(), new Harness().Inputs()).Frame;
        NativeBattleModulePoolTask[] children = result.Effect.Pools[1].Slots.Where(task => task.Callback == 0x59BCA0).ToArray();
        Assert.InRange(children.Length, 5, 10);
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 5 + children.Length * 6; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, result.Random);
        Assert.All(children, task =>
        {
            Assert.Equal(0, task.ReadWord(0x12));
            Assert.InRange(task.ReadWord(0x20), (ushort)4, (ushort)7);
        });
        Assert.Equal(CleanupCallback, result.Effect.Pools[0].Slots[1].Callback);
        Assert.Equal(0, result.Effect.Pools[0].Slots[1].ReadWord(12));
    }

    [Fact]
    public void Party_births_use_first_current_matching_contact_and_keep_unmatched_eligible_actor()
    {
        Harness harness = new();
        harness.SetContacts(6, 2, 0, 2);
        NativeBattleEffect313Frame result = AdvanceMain59B180(Frame(0, 122), ChildBank, Resources(), harness.Inputs()).Frame;
        NativeBattleModulePoolTask[] actors = result.Effect.Pools[1].Slots.Where(task => task.Callback == 0x59C4F0).ToArray();
        Assert.Equal(new ushort[] { 0, 1, 2 }, actors.Select(task => task.ReadWord(14)).ToArray());
        Assert.Equal(new short[] { 1, -1, 0 }, actors.Select(task => Signed(task, 0x1A)).ToArray());
        Assert.InRange(Signed(actors[1], 0x20), (short)-1750, (short)-1250);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Mandatory_append_fails_without_random_or_resizing()
    {
        NativeBattleEffect313Frame frame = Frame(0, 56);
        for (int slot = 1; slot < 100; slot++)
        {
            (frame, _) = Append(frame, ChildDescriptor, 0x59C250);
        }
        NativeCrtRandomState random = frame.Random;
        Assert.Throws<InvalidDataException>(() => AdvanceMain59B180(frame, ChildBank, null!, new Harness().Inputs()));
        Assert.Equal(random, frame.Random);
        Assert.Equal(100, frame.Effect.Pools[1].Slots.Length);
    }

    [Fact]
    public void Full_record_table_consumes_no_random_and_needs_no_static_or_model_fallback()
    {
        NativeBattleEffect313Frame frame = Frame(0, 30);
        for (int record = 0; record < RecordCount; record++)
        {
            frame = frame with { State = frame.State.WriteDword(RecordsAddress + (uint)(record * RecordStride), 1) };
        }
        Assert.Same(frame, BirthRecords313(frame, null!, null!));
    }

    [Fact]
    public void Paused_record_draws_but_does_not_move_age_expire_or_contribute_survivor_count()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect313Frame frame = ActiveRecord(0);
        (NativeBattleEffect313Frame result, int count) = AdvanceRecords59CA70(frame, Resources(), harness.Inputs());
        Assert.Equal(0, count);
        Assert.Equal(frame.State.ReadView(RecordsAddress, 24).Read(0, 24), result.State.ReadView(RecordsAddress, 24).Read(0, 24));
        Assert.IsType<NativeBattleEffect313DrawBatch>(Assert.Single(harness.Operations));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Record_motion_uses_old_velocity_then_arithmetic_sixteenth_damping()
    {
        NativeBattleEffect313Frame frame = ActiveRecord(0);
        (NativeBattleEffect313Frame result, int count) = AdvanceRecords59CA70(frame, Resources(), new Harness().Inputs());
        Assert.Equal(1, count);
        Assert.Equal(1, result.State.ReadWord(RecordsAddress + 4));
        Assert.Equal(83, result.State.ReadWord(RecordsAddress + 8));
        Assert.Equal(216, result.State.ReadWord(RecordsAddress + 10));
        Assert.Equal(-15, unchecked((short)result.State.ReadWord(RecordsAddress + 16)));
        Assert.Equal(15, result.State.ReadWord(RecordsAddress + 18));
    }

    [Fact]
    public void Last_sprite_frame_clears_only_marker_after_draw_and_phase_increment()
    {
        NativeBattleEffect313Frame frame = ActiveRecord(15);
        (NativeBattleEffect313Frame result, int count) = AdvanceRecords59CA70(frame, Resources(), new Harness().Inputs());
        Assert.Equal(0, count);
        Assert.Equal(0u, result.State.ReadDword(RecordsAddress));
        Assert.Equal(16, result.State.ReadWord(RecordsAddress + 4));
        Assert.Equal(frame.State.Read(RecordsAddress + 6, 18), result.State.Read(RecordsAddress + 6, 18));
    }

    [Theory]
    [InlineData(16, 2048, 0)]
    [InlineData(181, -1, 0)]
    [InlineData(182, 2048, 0)]
    [InlineData(190, 0, 2)]
    public void Stage_windows_and_retirement_follow_actual_old_age(int age, int intensity, int expected)
    {
        Harness harness = new();
        NativeBattleEffect313Step result = AdvanceMain59B180(Frame(0, age), ChildBank, null!, harness.Inputs());
        Assert.Equal(expected, result.Result);
        Assert.Equal(age + 1, Read(result.Frame, ChildBank).ReadWord(12));
        if (intensity < 0)
        {
            Assert.Empty(harness.Operations);
        }
        else
        {
            Assert.All(harness.Operations, operation => Assert.Equal(new NativeBattleEffect313Stage5713E0(intensity, 0), operation));
            Assert.Equal(age == 190 ? 2 : 1, harness.Operations.Count);
        }
    }

    [Fact]
    public void Cleanup_runs_same_pass_and_retains_real_completion_after_root_retires()
    {
        Harness harness = new();
        NativeBattleEffect313Frame frame = Frame(0, null);
        (frame, uint cleanup) = AppendRequired(frame, CleanupCallback);
        frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0).WriteWord(14, 0xFFFF));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect313AppendTextureLoad508630(0x20EFAB8, cleanup + 14), Assert.Single(harness.Operations));
        Assert.Equal(0, Read(frame, cleanup).ReadWord(14));
        Assert.Equal(1, Read(frame, cleanup).ReadWord(12));
        Assert.Equal(1, frame.Effect.Pools[0].Head);
        frame = WriteCompletionByte(frame, cleanup + 14, 1);
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.FixedBanks[RootBank]);
        Assert.NotNull(frame.State.FixedBanks[ChildBank]);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Final_exclusive_bank_transfer_retains_unknown_payload_across_json()
    {
        NativeBattleEffect313Frame frame = Frame(0, null);
        frame = WriteBytes(frame, ChildBank + 12, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        frame = Advance(frame, null!, new Harness().Inputs());
        NativeBattleEffect313State copy = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect313State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame.State)))!);
        Assert.Equal(new[] { true, true, false, true }, copy.ReadBank(ChildBank).ReadSnapshot(12, 4).Known);
        Assert.Equal(new byte[] { 1, 2 }, copy.ReadBank(ChildBank).Read(12, 2));
        Assert.Equal(new byte[] { 4 }, copy.ReadBank(ChildBank).Read(15, 1));
    }

    private static NativeBattleEffect313Frame ActiveRecord(int phase)
    {
        NativeBattleEffect313Frame frame = Frame(0, null);
        NativeBattleEffectArena record = PutPoint(NativeBattleEffectArena.Clear(), 8, new(100, 200, 4000)).Write(0, BitConverter.GetBytes(1u)).Write(4, BitConverter.GetBytes((ushort)phase)).Write(6, BitConverter.GetBytes((short)4096)).Write(16, BitConverter.GetBytes((short)-17)).Write(18, BitConverter.GetBytes((short)16));
        return frame with { State = frame.State.WriteView(RecordsAddress, record, 24) };
    }

    private static NativeBattleEffect313Frame Frame(int rootAge, int? mainAge)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteWord(12, (ushort)rootAge));
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        if (mainAge is int age)
        {
            (child, _) = child.Append(MainCallback);
            child = child.WriteSlot(0, child.Slots[0].WriteBytes(12, new byte[24]).WriteWord(12, (ushort)age));
        }
        NativeBattleEffect313State state = NativeBattleEffect313State.FromColdProcess().DetachBank(RootBank).DetachBank(ChildBank).WriteDword(CasterGlobal, 3).WriteDword(ArenaGlobal, 0x20DFAB8);
        NativeBattleOriginalEffectState effect = new(313, 312, new(313, Entry, Initializer), new(3, 0, 0x15, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        return new(memory, effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
    }

    private static NativeBattleEffect313Resources Resources()
    {
        byte[] sprite = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
        for (int frame = 0; frame < 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 44);
        }
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(42), -1);
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(RecordSpriteAddress, new(5, RecordSpriteAddress, 0, 0, sprite.ToImmutableArray())), new uint[] { 0xCD8CF0, 0xCD8D04, 0xCD8D18, 0xCD8D2C }.ToImmutableDictionary(address => address, _ => new byte[20].ToImmutableArray()));
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public ushort TargetMask;
        public List<(uint, int, int)> Points = [];
        public List<NativeBattleEffect313Operation> Operations = [];
        public NativeBattleActionPacketsState Packets = NativeBattleActionPacketsState.Fresh();

        public void SetContacts(int first, params int[] actors)
        {
            byte[] bytes = new byte[20];
            bytes[16] = (byte)actors.Length;
            var contacts = Packets.Contacts;
            for (int index = 0; index < actors.Length; index++)
            {
                contacts = contacts.SetItem(first + index, new(new((byte)actors[index], 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            Packets = Packets with { Headers = Packets.Headers.SetItem(0, new(bytes.ToImmutableArray(), 0, first, null)), Contacts = contacts };
        }

        public NativeBattleEffect313Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets, (_, _) => NativeBattleEffectArena.Clear().Write(0, new byte[] { 2 }).Write(0x40, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)).ReadSnapshot(0, 32).Data), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, _ => NativeBattleEffectArena.Clear(), (frame, model, selector, fraction, retained) =>
        {
            Points.Add((model, selector, fraction));
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 456) }, PutPoint(retained, 0, new(100, 200, 300)));
        }, (frame, _, retained) => new(frame, retained), (frame, mask, retained) =>
        {
            TargetMask = mask;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 789) }, PutPoint(retained, 0, new(10, 20, 30)));
        }, (_, _) => 0, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
