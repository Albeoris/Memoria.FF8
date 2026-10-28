using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect173ControllerTests
{
    [Fact]
    public void Paused_main_updates_current_anchor_midpoint_and_length_before_any_clock_or_birth()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect173Frame frame = Frame(1);
        frame = frame with { State = frame.State.Write(TargetPointAddress, Point(11, 0, -11, 0xABCD).Read(0, 8)).Write(AnchorAddress, Point(1, 2, 3, 0xBEEF).Read(0, 8)) };
        NativeBattleEffect173Step step = AdvanceMain5D7570(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal((3, 0xF0, 4096), Assert.Single(harness.Points));
        Assert.Equal(new NativeBattleVector(10, 20, -120), Vector(ReadBytes(step.Frame, AnchorAddress, 8)));
        Assert.Equal((ushort)0xBEEF, step.Frame.State.ReadWord(AnchorAddress + 6));
        Assert.Equal(new NativeBattleVector(10, 0, -65), Vector(ReadBytes(step.Frame, CenterAddress, 8)));
        Assert.Equal(55u, step.Frame.State.ReadDword(LengthGlobal));
        Assert.Equal(1, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Empty(harness.Operations);
        Assert.Equal(2, step.Frame.Effect.Pools.Length);
    }

    [Fact]
    public void Age1_uses_old_target_for_prepause_geometry_then_captures_target_and_transfers_actual_COMMON_payload()
    {
        Harness harness = new();
        NativeBattleEffect173Frame frame = Frame(1);
        int offset = CommonBankOffset + 12;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, new byte[] { 0xA5, 0x5A }).MarkUnknown(offset + 2, 1) } };
        NativeBattleEffect173Step step = AdvanceMain5D7570(frame, ChildBank, null!, harness.Inputs());
        Assert.IsType<NativeBattleEffect173TargetCenter5020A0>(harness.Operations[0]);
        Assert.Equal(new NativeBattleVector(5, 0, -60), Vector(ReadBytes(step.Frame, CenterAddress, 8)));
        Assert.Null(step.Frame.State.CommonQueueStorage);
        Assert.Equal(3, step.Frame.Effect.Pools.Length);
        Assert.Equal(new byte[] { 0xA5, 0x5A }, ReadBytes(step.Frame, 0x20DFAB8 + (uint)offset, 2).Read(0, 2));
        Assert.False(ReadBytes(step.Frame, 0x20DFAB8 + (uint)offset + 2, 1).ReadSnapshot(0, 1).Known[0]);
        Assert.Equal((byte)0xA5, step.Frame.Memory.Arena!.Read(offset, 1)[0]);
        Assert.Equal(new NativeBattleEffect173Sound501330(SoundAddress, 0, 128), harness.Operations[^1]);
    }

    [Theory]
    [InlineData(2, 0x5D7BA0u)]
    [InlineData(8, 0x5D7FE0u)]
    [InlineData(37, 0x5D80C0u)]
    [InlineData(38, 0x5D8E20u)]
    [InlineData(74, 0x5D8B40u)]
    public void Literal_births_retain_unwritten_task_fields(short age, uint callback)
    {
        NativeBattleEffect173Frame frame = Frame(age);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool.WriteSlot(1, pool.Slots[1].WriteWord(14, 0xA55A).WriteWord(32, 0xBEEF));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeBattleEffect173Step step = AdvanceMain5D7570(frame, ChildBank, null!, new Harness().Inputs());
        NativeBattleModulePoolTask child = step.Frame.Effect.Pools[1].Slots.Single(task => task.Callback == callback);
        Assert.Equal(0, child.ReadWord(12));
        Assert.Equal(0xA55A, child.ReadWord(14));
        Assert.Equal(0xBEEF, child.ReadWord(32));
    }

    [Fact]
    public void Four_age14_births_use_sparse_WORD_table_fields_and_actual_current_caster_rotation()
    {
        NativeBattleEffect173Step step = AdvanceMain5D7570(Frame(14), ChildBank, Resources(), new Harness().Inputs());
        NativeBattleModulePoolTask[] children = step.Frame.Effect.Pools[1].Slots.Where(task => task.Callback == 0x5D7E70).ToArray();
        Assert.Equal(4, children.Length);
        for (int index = 0; index < children.Length; index++)
        {
            Assert.Equal(10 + index, children[index].ReadWord(14));
            Assert.Equal(20 + index, children[index].ReadWord(26));
            Assert.Equal(0x1234, children[index].ReadWord(24));
            Assert.Equal(0x1400, children[index].ReadWord(28));
            Assert.Equal(0x16D, children[index].ReadWord(30));
        }
    }

    [Fact]
    public void Age85_uses_actual_header_contact_base_and_total_count()
    {
        Harness harness = new();
        NativeBattleEffect173Step step = AdvanceMain5D7570(Frame(85), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect173AllContacts506BA0(0, 1), Assert.Single(harness.Operations));
        Assert.Equal(86, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(94, 95, 0)]
    [InlineData(95, 96, 2)]
    [InlineData(32767, -32768, 0)]
    public void Signed_retirement_zeros_stage_only_after_increment_exceeds95(short age, short next, int result)
    {
        Harness harness = new();
        NativeBattleEffect173Step step = AdvanceMain5D7570(Frame(age), ChildBank, null!, harness.Inputs());
        Assert.Equal(unchecked((ushort)next), Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(result, step.Result);
        if (result == 2)
        {
            Assert.Equal(new NativeBattleEffect173Stage5713E0(0, 0), harness.Operations[^1]);
        }
    }

    [Fact]
    public void Paused_root_increments_clock_and_reads_real_preinitialization_queue()
    {
        NativeBattleEffect173Frame frame = Frame(0);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        NativeBattleEffect173Step step = AdvanceRoot5D9BF0(frame, RootBank, null!, new Harness { Flags = 1 }.Inputs());
        Assert.Equal(0x20EBAB8u, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(2, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(0, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.NotNull(step.Frame.State.CommonQueueStorage);
    }

    [Fact]
    public void Cleanup_uses_actual_async_WORD_completion_and_keeps_native_clock()
    {
        Harness harness = new();
        NativeBattleEffect173Frame frame = Frame(0);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1).WriteWord(14, 0));
        NativeBattleEffect173Step pending = AdvanceCleanup5D9C70(frame, RootBank, harness.Inputs());
        Assert.Equal(new NativeBattleEffect173AppendCommonTextureLoad508630(0x20DFAB8, RootBank + 14), Assert.Single(harness.Operations));
        Assert.Equal(0, pending.Result);
        frame = WriteCompletionByte(pending.Frame, RootBank + 14, 1);
        NativeBattleEffect173Step completed = AdvanceCleanup5D9C70(frame, RootBank, harness.Inputs());
        Assert.Equal(2, completed.Result);
        Assert.Equal(3, Read(completed.Frame, RootBank).ReadWord(12));
    }

    [Fact]
    public void Programme_initialization_clears64_only_and_retains_full_suffix_and_unknownness()
    {
        NativeBattleEffect173Frame frame = InitializeCommonBank(Frame(0));
        (frame, uint task) = Append(frame, CommonDescriptor, 0x5D90B0);
        frame = WriteBytes(frame, task + 28, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 256).ToArray()).MarkUnknown(70, 1), 256);
        frame = NativeBattleEffect173Programme.Initialize8FB0(frame, task + 28, ProgrammeAddress, 64, Resources());
        Assert.Equal(ProgrammeAddress, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, task + 28, 4).Read(0, 4)));
        Assert.Equal(new byte[60], ReadBytes(frame, task + 32, 60).Read(0, 60));
        Assert.Equal(0xA5, ReadBytes(frame, task + 92, 1).Read(0, 1)[0]);
        Assert.False(ReadBytes(frame, task + 98, 1).ReadSnapshot(0, 1).Known[0]);
    }

    [Fact]
    public void Programme_pause_uses_bit1_only_and_current_workspace_survives_JSON()
    {
        NativeBattleEffect173Frame frame = InitializeCommonBank(Frame(0));
        (frame, uint task) = Append(frame, CommonDescriptor, 0x5D90B0);
        frame = Write(frame, task, Read(frame, task).WriteBytes(16, new byte[8]).WriteWord(24, 4096).WriteWord(26, 0));
        frame = NativeBattleEffect173Programme.Initialize8FB0(frame, task + 28, ProgrammeAddress, 64, Resources());
        Harness harness = new() { Flags = 0x200 };
        NativeBattleEffect173Step step = NativeBattleEffect173Programme.Advance90B0(frame, task, Resources(), harness.Inputs());
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(step.Frame, task + 32, 4).Read(0, 4)));
        NativeBattleEffect173Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect173Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Frame)))!);
        NativeBattleEffect173Step frozen = NativeBattleEffect173Programme.Advance90B0(restored, task, Resources(), new Harness { Flags = 1 }.Inputs());
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frozen.Frame, task + 32, 4).Read(0, 4)));
        Assert.Equal(0, frozen.Result);
    }

    [Fact]
    public void Root_retirement_detaches_actual_live_COMMON_orphan_bytes_without_waiting_or_clearing()
    {
        NativeBattleEffect173Frame frame = InitializeCommonBank(Frame(0));
        NativeBattleEffectTaskPool child = frame.Effect.Pools[1];
        child = child.WriteSlot(0, child.Slots[0].UpdateHeader(0, child.Slots[0].State, child.Slots[0].Next, child.Slots[0].Callback)).UpdateLinks(null, null);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, child) } };
        (frame, uint task) = Append(frame, CommonDescriptor, 0x5D90B0);
        frame = Write(frame, task, Read(frame, task).WriteBytes(16, new byte[8]).WriteWord(24, 4096).WriteWord(26, 0).WriteWord(100, 0xBEEF));
        frame = NativeBattleEffect173Programme.Initialize8FB0(frame, task + 28, ProgrammeAddress, 64, Resources());
        Harness harness = new() { Flags = 1, CompleteTexture = true };
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(2, frame.Effect.Pools.Length);
        Assert.Equal(task, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadCommonQueue().Read(0, 4)));
        Assert.Contains(frame.Effect.Pools[0].Slots, value => value.Callback == CleanupCallback && (value.Flags & 1) != 0);
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(task, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadCommonQueue().Read(0, 4)));
        int offset = checked((int)(task - 0x20DFAB8));
        Assert.Equal(0x5D90B0u, BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(offset + 8, 4)));
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena.Read(offset + 100, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena.Read(offset, 2)) & 1);
    }

    [Fact]
    public void Root_hands_off_COMMON_before_async_texture_restore_overwrites_orphan_headers()
    {
        NativeBattleEffect173Frame frame = InitializeCommonBank(Frame(0));
        NativeBattleEffectTaskPool child = frame.Effect.Pools[1];
        child.Slots[0].UpdateHeader(0, child.Slots[0].State, child.Slots[0].Next, child.Slots[0].Callback);
        child.UpdateLinks(null, null);
        (frame, uint task) = Append(frame, CommonDescriptor, 0x5D90B0);
        frame = Write(frame, task, Read(frame, task).WriteBytes(16, new byte[8]).WriteWord(24, 4096).WriteWord(26, 0).WriteWord(100, 0xBEEF));
        frame = NativeBattleEffect173Programme.Initialize8FB0(frame, task + 28, ProgrammeAddress, 64, Resources());
        NativeBattleEffectTaskPool formerCommonOwner = frame.Effect.Pools[2];
        Harness harness = new() { Flags = 1 };
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(2, frame.Effect.Pools.Length);
        Assert.Throws<ObjectDisposedException>(() => formerCommonOwner.Validate());
        Assert.Equal(task, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadCommonQueue().Read(0, 4)));
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(CommonBankOffset + 100, 2)));
        NativeBattleEffect173Frame beforeRestore = NativeBattleStorageSnapshots.Capture(frame);
        frame = NativeBattleStorageSnapshots.Restore(beforeRestore);
        frame = Advance(frame, Resources(), harness.Inputs());
        NativeBattleEffect173AppendCommonTextureLoad508630 restore = Assert.IsType<NativeBattleEffect173AppendCommonTextureLoad508630>(Assert.Single(harness.Operations));
        Assert.Equal(0x20DFAB8u, restore.SourceAddress);
        Assert.Equal(0u, Read(frame, restore.CompletionAddress - 14).ReadWord(14));
        //508660 phase0 writes the A3 file to P before its completion callback; these are the first overlapping bank bytes.
        byte[] overwrittenHeader = Enumerable.Repeat((byte)0xA5, 16).ToArray();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(CommonBankOffset, overwrittenHeader) } };
        frame.Effect.Validate();
        NativeBattleEffect173Frame afterRestore = NativeBattleStorageSnapshots.Capture(frame);
        frame = NativeBattleStorageSnapshots.Restore(afterRestore);
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(overwrittenHeader, frame.Memory.Arena!.Read(CommonBankOffset, overwrittenHeader.Length));
        Assert.Equal(task, BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadCommonQueue().Read(0, 4)));
        frame = WriteCompletionByte(frame, restore.CompletionAddress, 255);
        frame = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(overwrittenHeader, frame.Memory.Arena!.Read(CommonBankOffset, overwrittenHeader.Length));
    }

    [Theory]
    [InlineData(0, 0x2030u)]
    [InlineData(819, 0x20F0u)]
    public void ACT_context_DWORD_mask_discards_source_high_bit_after_using_it_for_CPU_view_branch(short cue, uint expectedFlags)
    {
        NativeBattleEffect173Frame frame = Frame(0);
        NativeBattleEffect173Resources resources = Resources();
        resources = resources with { ProgrammeFlags = 0x10000030, StaticFronts = resources.StaticFronts.SetItem(ProgrammeFlagsAddress, BitConverter.GetBytes(0x10000030u).ToImmutableArray()) };
        byte[] output = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(28), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(36), cue);
        NativeBattleGfAnimationNode node = new(592, 0, output.ToImmutableArray());
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().Write(8, BitConverter.GetBytes(ProgrammeFlagsAddress));
        uint temporary = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleEffect173Frame drawn = NativeBattleEffect173Programme.DrawNode9130(frame, resources.Programme, node, caller, resources, new Harness().Inputs());
        Assert.Equal(expectedFlags, BinaryPrimitives.ReadUInt32LittleEndian(Context(drawn, temporary, 88).Read(28, 4)));
        Assert.Equal(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4), drawn.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    private static NativeBattleEffect173Frame Frame(short age)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteWord(12, 0));
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(MainCallback);
        child = child.WriteSlot(0, child.Slots[0].WriteWord(12, unchecked((ushort)age)));
        NativeBattleOriginalEffectState effect = new(173, 172, new(173, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, effect, new(42), NativeBattleEffect173State.FromColdProcess().DetachBank(0).DetachBank(1).WriteDword(CasterGlobal, 3).WriteDword(ArenaGlobal, 0x20DFAB8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffectArena Point(short x, short y, short z, ushort w) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(6, BitConverter.GetBytes(w));

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public bool CompleteTexture { get; init; }
        public List<NativeBattleEffect173Operation> Operations { get; } = [];
        public List<(int Actor, int Selector, int Fraction)> Points { get; } = [];
        public NativeBattleEffect173Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets(), (_, actor) => new(actor, 2) { Rotation14 = 0x1234 }, (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, (frame, actor, selector, fraction, retained) =>
        {
            Points.Add((actor, selector, fraction));
            return new(frame, CopyBytes(Point(10, 20, 30, 0), 0, retained, 0, 6));
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect173AppendCommonTextureLoad508630 texture && CompleteTexture)
            {
                frame = WriteCompletionByte(frame, texture.CompletionAddress, 1);
            }
            if (operation is NativeBattleEffect173TargetCenter5020A0 center)
            {
                frame = WriteBytes(frame, center.Destination, Point(100, 200, 300, 0), 6);
            }
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 14, -1, 38, 173, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }

    private static NativeBattleEffect173Resources Resources()
    {
        byte[] source = new byte[688];
        BinaryPrimitives.WriteInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 580);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 12);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(580), 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(584), 6);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(588), 12);
        byte[] rows = new byte[32];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(rows.AsSpan(index * 4), (ushort)(10 + index));
            BinaryPrimitives.WriteUInt16LittleEndian(rows.AsSpan(16 + index * 4), (ushort)(20 + index));
        }
        ImmutableDictionary<uint, ImmutableArray<byte>> fronts = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(BirthRowsAddress, rows.ToImmutableArray()).Add(ProgrammeFlagsAddress, BitConverter.GetBytes(0u).ToImmutableArray());
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, new(ProgrammeAddress, source.ToImmutableArray()), [], [], [], 0, [], rows.ToImmutableArray(), fronts);
    }
}
