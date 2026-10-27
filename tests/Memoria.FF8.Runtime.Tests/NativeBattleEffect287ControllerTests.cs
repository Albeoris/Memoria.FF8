using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect287ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0x1070;
    private const uint MeshBank = Arena + 0x21FF4;

    [Fact]
    public void Initial_root_uses_second_buffer_and_leaves_uninitialized_common_queues_unread()
    {
        Harness harness = new();
        NativeBattleEffect287Step step = AdvanceRoot6F2F20(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(step.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Single(step.Frame.Effect.Pools);
        Assert.Equal(new NativeBattleEffect287Stage5713E0(0, 0), Assert.Single(harness.Operations));
        Assert.Equal(0, harness.Polls);
    }

    [Fact]
    public void Native_unlink_returns_live_orphan_bytes_and_keeps_only_empty_queue_owner()
    {
        NativeBattleEffect287Frame frame = Frame(10);
        (frame, uint child) = Append(frame, MeshDescriptor, 0x6F3DF0);
        frame = Write(frame, child, Read(frame, child).WriteWord(12, 17).MarkPayloadUnknown(15, 1));
        frame = ClearMeshQueue(frame);
        Assert.DoesNotContain(frame.Effect.Pools, pool => pool.DescriptorAddress == MeshDescriptor);
        Assert.Equal(new byte[8], frame.State.ReadMeshQueue().Read(0, 8));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(0x21FF4, 2)));
        Assert.Equal(0x6F3DF0u, BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena.Read(0x21FFC, 4)));
        Assert.Equal(17, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena.Read(0x22000, 2)));
        Assert.False(frame.Memory.Arena.ReadSnapshot(0x22003, 1).Known[0]);
        Assert.Throws<InvalidDataException>(() => Read(frame, child));
        Assert.Throws<InvalidDataException>(() => ClearMeshQueue(frame));
    }

    [Fact]
    public void Empty_mesh_scan_does_not_decode_unknown_bank_or_require_tail_knownness()
    {
        NativeBattleEffect287Frame frame = ClearMeshQueue(Frame(10));
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0x21FF4, 4096) }, State = frame.State.WriteView(MeshQueueAddress + 4, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 4) };
        (NativeBattleEffect287Frame result, int survivors) = AdvanceDescriptor(frame, MeshDescriptor, null!, new Harness().Inputs());
        Assert.Same(frame, result);
        Assert.Equal(0, survivors);
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(0x21FF4, 1));
    }

    [Fact]
    public void Opaque_tim_bytes_survive_io_hold_json_and_main_only_retirement()
    {
        Harness harness = new();
        NativeBattleEffect287Frame frame = ClearMeshQueue(Frame(10));
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x21FF4, Enumerable.Repeat((byte)255, 4096).ToArray()).MarkUnknown(0x22020, 1) } };
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(2, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(Arena + SecondPacketOffset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1, harness.Polls);
        NativeBattleEffect287Frame restored = JsonSerializer.Deserialize<NativeBattleEffect287Frame>(JsonSerializer.Serialize(frame))!;
        harness.Complete = true;
        restored = Advance(restored, null!, harness.Inputs());
        Assert.Empty(restored.Effect.Pools);
        Assert.Equal(0u, restored.Effect.ReturnedDescriptor);
        Assert.Equal(new byte[8], restored.State.ReadMeshQueue().Read(0, 8));
        Assert.NotNull(restored.State.FixedBanks[RootBank]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, restored.Memory.Arena!.Read(0x21FF4, 4));
        Assert.False(restored.Memory.Arena.ReadSnapshot(0x22020, 1).Known[0]);
        Assert.Equal(2, harness.Polls);
    }

    [Fact]
    public void Captured_targets_keep_current_contact_order_and_duplicate_actors()
    {
        Harness harness = new();
        harness.SetContacts(7, 2, 0, 2);
        NativeBattleEffect287Frame frame = CaptureTargets(AddMain(Frame(10), 0), Main, harness.Inputs());
        Assert.Equal(3, Read(frame, Main).ReadWord(14));
        Assert.Equal(new[] { ModelAddress(2), ModelAddress(0), ModelAddress(2) }, Enumerable.Range(0, 3).Select(index => Read(frame, Main).ReadDword(32 + 4 * index)).ToArray());
    }

    [Fact]
    public void Fourth_recipient_cannot_overwrite_first_workspace()
    {
        Harness harness = new();
        harness.SetContacts(1, 0, 1, 2, 3);
        Assert.Throws<NotSupportedException>(() => CaptureTargets(AddMain(Frame(10), 0), Main, harness.Inputs()));
    }

    [Fact]
    public void File_queue_precedes_advance_and_is_not_repeated_at_next_age()
    {
        Harness harness = new();
        NativeBattleEffect287Frame frame = AddMain(Frame(10), 55);
        frame = AdvanceTail(frame, Main, 55, harness.Inputs()).Frame;
        Assert.Collection(harness.Operations, operation => Assert.Equal(new NativeBattleEffect287Queue5341D0(360, Arena + File360Offset, 0), operation), operation => Assert.IsType<NativeBattleEffect287Advance534210>(operation));
        harness.Operations.Clear();
        _ = AdvanceTail(frame, Main, 56, harness.Inputs());
        Assert.IsType<NativeBattleEffect287Advance534210>(Assert.Single(harness.Operations));
    }

    [Fact]
    public void Contact_batch_reads_current_header_after_io_advance()
    {
        Harness harness = new();
        harness.SetContacts(1, 0);
        harness.OnAdvance = () => harness.SetContacts(9, 2, 1, 2);
        NativeBattleEffect287Frame frame = AdvanceTail(AddMain(Frame(10), 65), Main, 65, harness.Inputs()).Frame;
        Assert.Collection(harness.Operations, operation => Assert.IsType<NativeBattleEffect287Advance534210>(operation), operation => Assert.Equal(new NativeBattleEffect287Contacts506BA0(9, 3), operation));
        Assert.Equal(66, Read(frame, Main).ReadWord(12));
    }

    [Theory]
    [InlineData(33, 0)]
    [InlineData(40, 1792)]
    [InlineData(41, 2048)]
    [InlineData(58, 2048)]
    [InlineData(59, 2048)]
    [InlineData(74, 128)]
    [InlineData(75, 0)]
    public void Stage_intensity_uses_native_piecewise_age_ranges(int age, int expected)
    {
        Assert.Equal(expected, StageIntensity(age));
    }

    [Fact]
    public void Early_sprite_has_three_random_draws_per_success_and_stops_after_full_bank()
    {
        NativeBattleEffect287Frame frame = Frame(10);
        for (int index = 0; index < 63; index++)
        {
            (frame, _) = Append(frame, SpriteDescriptor, 0x6F3B80);
        }
        uint last = Arena + 63 * 36;
        frame = WriteBytes(frame, last + 26, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x12 }).MarkUnknown(1, 1), 2);
        NativeCrtRandomState expected = frame.Random.Draw(out _).Draw(out _).Draw(out _);
        NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = 100, Y = 200, Z = 300 };
        NativeBattleEffect287Frame result = BirthEarlySprites(frame, 0, world);
        Assert.Equal(expected, result.Random);
        Assert.Equal(FirstSpriteAddress, Read(result, last).ReadDword(12));
        Assert.Equal(0, Read(result, last).ReadWord(26));
        Assert.Equal(14, Read(result, last).ReadWord(34));
        Assert.Equal(2000u, Read(result, last).ReadDword(16));
        Assert.Equal(new[] { true, false }, result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal((byte)0x34, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.Equal(frame.Memory.GeometryRegisters!.Translation, result.Memory.GeometryRegisters!.Translation);
    }

    [Fact]
    public void Late_sprite_failure_skips_bone_query_and_random_draws()
    {
        Harness harness = new();
        NativeBattleEffect287Frame frame = Frame(10);
        for (int index = 0; index < 63; index++)
        {
            (frame, _) = Append(frame, SpriteDescriptor, 0x6F3B80);
        }
        NativeBattleEffect287Frame result = BirthBoneSprites(frame, harness.Inputs());
        Assert.Equal(frame.Random.Draw(out _).Draw(out _), result.Random);
        Assert.Equal(27, Assert.Single(harness.Bones));
        Assert.Equal(SecondSpriteAddress, Read(result, Arena + 63 * 36).ReadDword(12));
    }

    [Fact]
    public void Small_birth_keeps_retained_age_and_padding_and_consumes_six_draws()
    {
        NativeBattleEffect287Frame frame = Frame(10);
        uint first = Arena + 0x900;
        frame = WriteBytes(frame, first + 12, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)42)).MarkUnknown(2, 2), 4);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect287Frame result = BirthSmallDebris(frame, new Harness().Inputs());
        Assert.Equal(expected, result.Random);
        Assert.Equal(42, Read(result, first).ReadWord(12));
        Assert.Equal(new[] { false, false }, TaskView(Read(result, first), 14, 2).ReadSnapshot(0, 2).Known);
        Assert.InRange(Signed(Read(result, first), 22), -63, -32);
    }

    [Fact]
    public void Caller40_has_negative_depth_exact_window_bytes_and_unknown_matrix_padding()
    {
        NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = 100, Y = -27, Z = 300 };
        NativeBattleEffectArena caller = ProgrammeCaller(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), world, 1, 96, Arena + MorphOffset);
        Assert.Equal(-27, BinaryPrimitives.ReadInt32LittleEndian(caller.Read(24, 4)));
        Assert.Equal((short)-128, BinaryPrimitives.ReadInt16LittleEndian(caller.Read(32, 2)));
        Assert.Equal(new byte[] { 1, 96 }, caller.Read(34, 2));
        Assert.Equal(Arena + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(caller.Read(36, 4)));
        Assert.Equal(new[] { false, false }, caller.ReadSnapshot(18, 2).Known);
    }

    [Fact]
    public void Imported_age75_skips_drawing_but_still_advances_io_before_retirement()
    {
        Harness harness = new();
        NativeBattleEffect287Frame frame = AddMain(Frame(10), 75);
        NativeBattleEffect287Step step = AdvanceMain6F31A0(frame, Main, null!, harness.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(76, Read(step.Frame, Main).ReadWord(12));
        Assert.Collection(harness.Operations, operation => Assert.IsType<NativeBattleEffect287ModelFlags>(operation), operation => Assert.IsType<NativeBattleEffect287Advance534210>(operation));
        Assert.Equal(0u, step.Frame.State.ReadDword(IntensityGlobal));
    }

    private static NativeBattleEffect287Frame AddMain(NativeBattleEffect287Frame frame, int age)
    {
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x267);
        return Write(frame, main, Read(frame, main).WriteWord(12, (ushort)age));
    }

    private static NativeBattleEffect287Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(15, initialized ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                pools = pools.Add(NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity));
            }
        }
        NativeBattleEffect287State state = NativeBattleEffect287State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        if (initialized)
        {
            state = state.DetachMeshQueue();
        }
        NativeBattleOriginalEffectState effect = new(287, 286, new(287, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools), effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public bool Complete { get; set; }
        public int Polls { get; private set; }
        public Action? OnAdvance { get; set; }
        public List<int> Bones { get; } = [];
        public List<NativeBattleEffect287Operation> Operations { get; } = [];
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

        public NativeBattleEffect287Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (_, _, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (frame, _, retained) => new(frame, retained), (frame, _, bone, retained) =>
        {
            Bones.Add(bone);
            return new(frame, PutPoint(retained, 0, new(100, 200, 300)));
        }, (frame, _, retained) => new(frame, retained), (frame, _) =>
        {
            Polls++;
            return new(frame, Complete);
        }, frame => new(frame, 0), (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect287Advance534210)
            {
                OnAdvance?.Invoke();
            }
            return frame;
        });
    }
}
