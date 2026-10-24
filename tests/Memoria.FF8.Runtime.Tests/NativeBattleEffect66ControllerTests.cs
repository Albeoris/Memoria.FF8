using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66ControllerTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0x200, -1)]
    public void Driver_composes_the_retained_world_before_its_pause_return(uint flags, int io)
    {
        NativeBattleEffect66Frame frame = Frame(54);
        frame = frame with { State = frame.State.WriteWord(ViewAddress + 18, 0xA55A) };
        Harness harness = new() { Flags = flags, StepResult = io };
        NativeBattleEffect66Step result = AdvanceMain6226E0(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Equal(54, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Empty(harness.Operations);
        Assert.Equal(11, Dword(result.Frame.State.ReadView(ViewAddress, 32), 20));
        Assert.Equal(22, Dword(result.Frame.State.ReadView(ViewAddress, 32), 24));
        Assert.Equal(33, Dword(result.Frame.State.ReadView(ViewAddress, 32), 28));
        Assert.Equal(0xA55A, result.Frame.State.ReadWord(ViewAddress + 18));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(832, 833, 2)]
    [InlineData(32767, 32768, 0)]
    public void Driver_uses_a_signed_word_clock_and_retires_only_after_832(short age, ushort expected, int result)
    {
        NativeBattleEffect66Frame frame = Frame(age);
        Harness harness = new();
        NativeBattleEffect66Step step = AdvanceMain6226E0(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Equal(expected, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(result, step.Result);
        Assert.Equal(result == 2 ? 1 : 0, harness.Operations.OfType<NativeBattleEffect66CaptureCameraRetirement>().Count());
        if (result == 2)
        {
            Assert.Equal(new NativeBattleEffect66StopSound46B450(-1, 0), Assert.Single(harness.Operations.OfType<NativeBattleEffect66StopSound46B450>()));
            Assert.Equal(new NativeBattleEffect66Sequence505C00(5, 1), Assert.Single(harness.Operations.OfType<NativeBattleEffect66Sequence505C00>()));
        }
    }

    [Fact]
    public void Actor_text_has_exact_actor_index_and_native_descriptor_operands()
    {
        Harness harness = new();
        NativeBattleEffect66Step step = AdvanceMain6226E0(Frame(191), ChildBank, Resources(), harness.Inputs());
        Assert.Equal(new NativeBattleEffect66Text4876F0(5, 5, 40), Assert.Single(harness.Operations));
        Assert.Equal(192, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Voice_gain_request_is_separate_from_music_and_preserves_the_full_frame()
    {
        Harness harness = new();
        NativeBattleEffect66Frame frame = Frame(802);
        NativeBattleEffect66Step step = AdvanceMain6226E0(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Equal(new NativeBattleEffect66SoundGain46BEC0(120, 0), Assert.Single(harness.Operations));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Midpoint_preserves_bone_service_updates_and_uses_actual_model_word26()
    {
        Harness harness = new();
        NativeBattleEffect66Inputs inputs = harness.Inputs() with { ReadBone502170 = (frame, actor, bone, offset, retained) =>
        {
            Assert.Equal(5, actor);
            Assert.Equal(2048, offset);
            frame = frame with { State = frame.State.WriteDword(BaseAddress, frame.State.ReadDword(BaseAddress) + 1) };
            return new(frame, PointView(bone == 1 ? new(-3, 5, -7) : new(2, -2, 4)));
        } };
        NativeBattleEffect66Step step = AdvanceMain6226E0(Frame(53), ChildBank, Resources(), inputs);
        Assert.Equal(new NativeBattleVector(0, 1, -1), Vector(step.Frame.State.ReadView(CenterAddress, 6)));
        Assert.Equal(unchecked((ushort)-1450), step.Frame.State.ReadWord(CenterAddress + 6));
        Assert.Equal(2u, step.Frame.State.ReadDword(BaseAddress));
        Assert.IsType<NativeBattleEffect66Sequence505C00>(harness.Operations[0]);
        Assert.Equal(new NativeBattleEffect66MusicStop46B800(0), harness.Operations[1]);
    }

    [Fact]
    public void Contact_resolves_the_current_header_instead_of_the_entry_target()
    {
        Harness harness = new();
        harness.Packets = harness.Packets with { Headers = harness.Packets.Headers.SetItem(0, harness.Packets.Headers[0] with { FirstContactIndex = 1 }) };
        AdvanceMain6226E0(Frame(25), ChildBank, Resources(), harness.Inputs());
        Assert.Equal(new NativeBattleEffect66Contact506690(1), Assert.Single(harness.Operations.OfType<NativeBattleEffect66Contact506690>()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Vibration_stop_is_queried_only_at_the_native_age(bool playing)
    {
        NativeBattleEffect66Frame frame = Frame(829);
        frame = frame with { State = frame.State.WriteDword(VibrationGlobal, 17) };
        Harness harness = new() { Playing = playing };
        AdvanceMain6226E0(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Equal(playing ? 1 : 0, harness.Operations.OfType<NativeBattleEffect66StopVibration4A2940>().Count());
        Assert.Equal(1, harness.PlayingQueries);
    }

    [Theory]
    [InlineData(6, 216, 0)]
    [InlineData(7, 255, 0)]
    [InlineData(58, 38, 0)]
    [InlineData(59, -1, 2)]
    public void Envelope_divides_before_multiplying_and_increments_even_at_retirement(ushort age, int color, int result)
    {
        NativeBattleEffect66Frame frame = BirthEnvelope622CF0(Frame(), 7, 20, 32, 255);
        uint task = ChildBank + ChildStride;
        frame = Write(frame, task, Read(frame, task).WriteWord(12, age));
        Harness harness = new();
        NativeBattleEffect66Step step = AdvanceEnvelope622D30(frame, task, harness.Inputs());
        Assert.Equal(result, step.Result);
        Assert.Equal(age + 1, Read(step.Frame, task).ReadWord(12));
        if (color >= 0)
        {
            Assert.Equal(new NativeBattleEffect66Rectangle571250(color, color, color), Assert.Single(harness.Operations));
        }
        else
        {
            Assert.Empty(harness.Operations);
        }
    }

    [Fact]
    public void White_publishes_before_pause_but_envelope_waits_without_publication()
    {
        NativeBattleEffect66Frame frame = BirthWhite622DC0(Frame(), 1, 172);
        uint task = ChildBank + ChildStride;
        Harness harness = new() { Flags = 0x200 };
        NativeBattleEffect66Step white = AdvanceWhite(frame, task, harness.Inputs());
        Assert.IsType<NativeBattleEffect66PcRender47CF50>(Assert.Single(harness.Operations));
        Assert.Equal(0, Read(white.Frame, task).ReadWord(12));
        frame = BirthEnvelope622CF0(frame, 7, 20, 32, 255);
        harness.Operations.Clear();
        NativeBattleEffect66Step envelope = AdvanceEnvelope622D30(frame, task + ChildStride, harness.Inputs());
        Assert.Empty(harness.Operations);
        Assert.Equal(0, Read(envelope.Frame, task + ChildStride).ReadWord(12));
    }

    [Fact]
    public void Spawn_initializes_only_its_two_clock_words_and_retains_other_payload_knownness()
    {
        NativeBattleEffect66Frame frame = Frame();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteDword(16, 0x12345678).MarkPayloadUnknown(20, 4)) }) } };
        (frame, uint address) = Spawn622CD0(frame, 0x622EB0);
        Assert.Equal(0u, Read(frame, address).ReadDword(12));
        Assert.Equal(0x12345678u, Read(frame, address).ReadDword(16));
        Assert.Throws<NotSupportedException>(() => Read(frame, address).ReadDword(20));
    }

    [Fact]
    public void Empty_child_root_increments_and_detaches_both_fixed_bank_owners()
    {
        NativeBattleEffect66Frame frame = Frame();
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        child = child with { Slots = child.Slots.SetItem(99, child.Slots[99].WriteDword(16, 0xAABBCCDD)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, child) } };
        NativeBattleEffect66Frame result = Advance(frame, Resources(), new Harness().Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(result.State.ReadBank(0).Read(12, 2)));
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(result.State.ReadBank(1).Read(99 * ChildStride + 16, 4)));
        NativeBattleEffect66Frame restored = JsonSerializer.Deserialize<NativeBattleEffect66Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(result.State.ReadBank(1).Read(0, ChildCount * ChildStride), restored.State.ReadBank(1).Read(0, ChildCount * ChildStride));
    }

    [Fact]
    public void Body_factory_writes_exact_face_words_and_only_the_shard_marker_words()
    {
        NativeBattleEffect66Frame frame = Frame();
        frame = WriteBytes(frame, 0x20DFAB8 + 0x29184, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 68).ToArray()), 68);
        NativeBattleEffect66Frame result = NativeBattleEffect66Lower.InitializeBody625790(frame);
        Assert.Equal(0x20DFAB8u + 0x28000, result.State.ReadDword(0x24C9190));
        Assert.Equal(0x20DFAB8u + 0x29184, result.State.ReadDword(0x24C917C));
        Assert.Equal(0u, result.State.ReadDword(0x24C9180));
        byte[] flags = ReadBytes(result, 0x20DFAB8 + 0x28000, 0x1184).Read(0, 0x1184);
        Assert.All(Enumerable.Range(0, 2242), index => Assert.Equal(0x4000, BinaryPrimitives.ReadUInt16LittleEndian(flags.AsSpan(index * 2))));
        Assert.Equal(new byte[] { 0, 0 }, ReadBytes(result, 0x20DFAB8 + 0x29184, 2).Read(0, 2));
        Assert.All(ReadBytes(result, 0x20DFAB8 + 0x29186, 66).Read(0, 66), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Later_burst_reuses_the_actual_shard_pointer_and_preserves_record_payloads()
    {
        NativeBattleEffect66Frame frame = NativeBattleEffect66Lower.InitializeBody625790(Frame());
        uint records = frame.State.ReadDword(0x24C917C);
        frame = WriteBytes(frame, records, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 24).ToArray()), 24);
        NativeBattleEffect66Frame result = NativeBattleEffect66Lower.InitializeBurst627840(frame);
        Assert.Equal(records, result.State.ReadDword(InitializedBase));
        Assert.Equal(new byte[4], ReadBytes(result, records, 4).Read(0, 4));
        Assert.All(ReadBytes(result, records + 4, 20).Read(0, 20), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(0xFFFFFFFFu, result.State.ReadDword(InitializedBase + 4));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Ring_factory_consumes_three_shared_crt_values_and_publishes_each_native_shape()
    {
        NativeBattleEffect66Frame frame = Frame();
        frame = frame with { State = frame.State.WriteWord(CenterAddress + 2, 300) };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort third);
        NativeBattleEffect66Frame result = NativeBattleEffect66Lower.BirthRings626B00(frame);
        Assert.Equal(expected, result.Random);
        ushort[] angles = [(ushort)(first & 2047), (ushort)(second & 2047), (ushort)(third & 2047)];
        short[] y = [-700, 300, 1500];
        ushort[] durations = [34, 36, 32];
        for (int index = 0; index < 3; index++)
        {
            NativeBattleModulePoolTask task = Read(result, ChildBank + (uint)((index + 1) * ChildStride));
            Assert.Equal(0x626C50u, task.Callback);
            Assert.Equal(angles[index], task.ReadWord(0x18));
            Assert.Equal(y[index], unchecked((short)task.ReadWord(0x12)));
            Assert.Equal(durations[index], task.ReadWord(14));
        }
    }

    [Fact]
    public void Body_wrapper_keeps_its_final_nondrawing_age_before_retirement()
    {
        NativeBattleEffect66Frame frame = Frame(160);
        Harness harness = new();
        NativeBattleEffect66Step result = NativeBattleEffect66Lower.AdvanceBody6257F0(frame, ChildBank, harness.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(161, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Rotation_load_retains_translation_and_unknown_matrix_padding()
    {
        NativeBattleEffect66Frame frame = Frame();
        NativeBattleGeometryRegisters prior = NativeBattleGeometryRegisters.Unknown() with { Translation = [123, null, -456] };
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = prior } };
        NativeBattlePackedMatrix32 source = NativeBattlePackedMatrix32.FromMatrix(Identity(), null);
        source = source with { Known = source.Known.Select((known, index) => index < 18 && known).ToImmutableArray() };
        NativeBattleEffect66Frame result = LoadMatrix(frame, source, true, false);
        Assert.Equal(prior.Translation, result.Memory.GeometryRegisters!.Translation);
        Assert.Equal(source.Bytes.Take(18).Select(value => (byte?)value), result.Memory.GeometryRegisters.Rotation20.Take(18));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Throws<NotSupportedException>(() => LoadMatrix(frame, source, false, true));
    }

    [Fact]
    public void Translation_load_does_not_require_or_publish_unknown_rotation()
    {
        NativeBattleEffect66Frame frame = Frame();
        NativeBattleGeometryRegisters prior = NativeBattleGeometryRegisters.Unknown() with { Rotation20 = Enumerable.Repeat((byte?)0xA5, 20).ToImmutableArray() };
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = prior } };
        NativeBattlePackedMatrix32 source = NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 11, Y = -22, Z = 33 }, null);
        source = source with { Known = source.Known.Select((known, index) => index >= 20 && known).ToImmutableArray() };
        NativeBattleEffect66Frame result = LoadMatrix(frame, source, false, true);
        Assert.Equal(prior.Rotation20, result.Memory.GeometryRegisters!.Rotation20);
        Assert.Equal(new int?[] { 11, -22, 33 }, result.Memory.GeometryRegisters.Translation);
        Assert.Throws<NotSupportedException>(() => LoadMatrix(frame, source, true, false));
    }

    private static NativeBattleEffect66Frame Frame(short age = 100)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(MainCallback);
        child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, unchecked((ushort)age))) };
        NativeBattleOriginalEffectState effect = new(66, 65, new(66, Entry, Initializer), new(5, 0, 1, 0, 0, 0), null) { Pools = [root, child], ReturnedDescriptor = RootDescriptor };
        NativeBattleEffect66Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(42), NativeBattleEffect66State.FromColdProcess().DetachBank(0).DetachBank(1));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(CasterGlobal, 5) };
        return WriteBytes(frame, CapturedWorldAddress, PackedArena(NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 1, Y = 2, Z = 3 }, 0)), 32);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static int Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffect66Resources Resources() => new(new("texture", []), new("embedded", []), new("camera", new byte[CameraLength].ToImmutableArray()), new("vibration", []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public int StepResult { get; init; }
        public bool Playing { get; init; }
        public int PlayingQueries { get; private set; }
        public List<NativeBattleEffect66Operation> Operations { get; } = [];
        public NativeBattleActionPacketsState Packets { get; set; } = PacketsForTest();
        public NativeBattleEffect66Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets, _ => Flags, current => new(current, StepResult), _ => NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 10, Y = 20, Z = 30 }, 0), null!, _ => 0, _ => new(new(1, 2, 3), new(4, 5, 6), 512, 7), (_, actor) => new(actor, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, -8192) }, null!, (_, _) => throw new InvalidOperationException(), null!, (_, _) =>
        {
            PlayingQueries++;
            return Playing;
        }, (current, operation) =>
        {
            Operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleActionPacketsState PacketsForTest()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(5, 8, 16, -1, 354, 66, null));
        (packets, _) = packets.AppendContact(new(new(5, 5, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(0, 5, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        return packets;
    }
}
