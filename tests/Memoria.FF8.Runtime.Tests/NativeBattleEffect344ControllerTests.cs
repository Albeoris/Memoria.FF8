using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect344Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect344ControllerTests
{
    [Fact]
    public void Entry_preserves_write_only_target_byte_without_model_eligibility_gate()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleContactPacket contact = packets.ReadContact(0);
        packets = packets with { Contacts = packets.Contacts.SetItem(0, contact with { Primary = contact.Primary with { ActorIndex = 255 } }) };
        Harness harness = new() { CurrentPackets = packets };
        NativeBattleEffect344Frame frame = Initialize(Cold(), Resources(), harness.Inputs());
        Assert.Equal(255u, frame.State.ReadDword(TargetGlobal));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Entry_retains_payload_suffix_and_completion_and_reads_actual_first_contact()
    {
        Harness harness = new();
        NativeBattleEffect344Frame frame = Cold();
        NativeBattleEffectArena root = frame.State.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xA55A));
        NativeBattleEffectArena child = frame.State.ReadBank(1).Write(14, BitConverter.GetBytes((ushort)0xBEEF)).MarkUnknown(16, 1).Write(99 * 36 + 35, [0x77]);
        frame = frame with { State = frame.State.Write(CompletionGlobal, [1]) with { BankStorage = [root, child] } };
        frame = Initialize(frame, Resources(), harness.Inputs());
        Assert.Equal(6u, frame.State.ReadDword(TargetGlobal));
        Assert.Equal(4u, frame.State.ReadDword(CasterGlobal));
        Assert.Equal(0x1D99A78u, frame.State.ReadDword(ParametersGlobal));
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)0xA55A, Read(frame, RootBank).ReadWord(14));
        Assert.Equal((ushort)0xBEEF, Read(frame, ChildBank).ReadWord(14));
        Assert.Throws<NotSupportedException>(() => Read(frame, ChildBank).ReadByte(16));
        Assert.Equal((byte)0x77, frame.Effect.Pools[1].Slots[99].ReadByte(35));
        Assert.Equal((byte)1, frame.State.Read(CompletionGlobal, 1)[0]);
        Assert.All(frame.State.BankStorage, bank => Assert.Null(bank));
        Assert.Empty(harness.Operations);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x200u)]
    [InlineData(0x201u)]
    public void Pause_holds_child_services_and_clock_but_root_still_alternates(uint flags)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect344Frame frame = Active();
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(12));
        Assert.Equal(0x24E0BC8u, frame.State.ReadDword(CursorGlobal));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal((ushort)2, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(0x24E8BC8u, frame.State.ReadDword(CursorGlobal));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Worker_wait_uses_actual_byte_without_clearing_it_in_controller()
    {
        Harness harness = new();
        NativeBattleEffect344Frame frame = AtAge(30);
        NativeBattleEffect344Step staged = AdvanceChild(frame, ChildBank, harness.Inputs());
        Assert.Equal(new NativeBattleEffect344StageMusic501A20(MusicAddress, CompletionGlobal), Assert.Single(harness.Operations));
        Assert.Equal((ushort)31, Read(staged.Frame, ChildBank).ReadWord(12));
        NativeBattleEffect344Step waiting = AdvanceChild(staged.Frame, ChildBank, harness.Inputs());
        Assert.Equal((ushort)31, Read(waiting.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, waiting.Result);
        frame = WriteCompletionByte(waiting.Frame, CompletionGlobal, 1);
        NativeBattleEffect344Step ready = AdvanceChild(frame, ChildBank, harness.Inputs());
        Assert.Equal((ushort)32, Read(ready.Frame, ChildBank).ReadWord(12));
        Assert.Equal((byte)1, ready.Frame.State.Read(CompletionGlobal, 1)[0]);
        Assert.Single(harness.Operations);
    }

    [Fact]
    public void Camera_registration_precedes_caster_sequence20()
    {
        Harness harness = new();
        NativeBattleEffect344Step step = AdvanceChild(AtAge(32), ChildBank, harness.Inputs());
        Assert.Equal((ushort)33, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect344RegisterCamera5099A0(CameraAddress), operation),
            operation => Assert.Equal(new NativeBattleEffect344SetSequence505C00(4, 20), operation));
    }

    [Fact]
    public void Contact_is_resolved_from_current_header_after_music_gain()
    {
        Harness harness = new();
        NativeBattleEffect344Frame frame = AtAge(34);
        harness.ChangeContactOnGain = true;
        NativeBattleEffect344Step step = AdvanceChild(frame, ChildBank, harness.Inputs());
        Assert.Equal((ushort)35, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect344MusicGain501B10(0, 127), operation),
            operation => Assert.Equal(new NativeBattleEffect344ConsumeFirstContact506690(1), operation));
        Assert.Equal(6u, step.Frame.State.ReadDword(TargetGlobal));
    }

    [Theory]
    [InlineData(34, 35, 0)]
    [InlineData(35, 36, 2)]
    [InlineData(32767, 32768, 0)]
    [InlineData(65535, 0, 0)]
    public void Retirement_compares_new_signed_word_after_increment(ushort age, ushort next, int result)
    {
        NativeBattleEffect344Step step = AdvanceChild(AtAge(age), ChildBank, new Harness().Inputs());
        Assert.Equal(next, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(result, step.Result);
    }

    [Fact]
    public void Root_release_preserves_banks_and_does_not_touch_common_arena()
    {
        NativeBattleEffect344Frame frame = AtAge(35);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(14, 0xA55A));
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteByte(35, 0xCC));
        NativeBattleEffectMemory memory = frame.Memory;
        frame = Advance(frame, null!, new Harness().Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal((ushort)1, BitConverter.ToUInt16(frame.State.ReadBank(0).Read(12, 2)));
        Assert.Equal((ushort)0xA55A, BitConverter.ToUInt16(frame.State.ReadBank(0).Read(14, 2)));
        Assert.Equal((byte)0xCC, frame.State.ReadBank(1).Read(35, 1)[0]);
        Assert.Same(memory, frame.Memory);
    }

    [Fact]
    public void Unknown_worker_byte_stays_unknown_across_json_and_cannot_complete_wait()
    {
        NativeBattleEffect344Frame frame = AtAge(31);
        frame = frame with { State = frame.State with { Storage = frame.State.Storage.MarkUnknown(checked((int)(CompletionGlobal - BaseAddress)), 1) } };
        frame = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect344Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Throws<NotSupportedException>(() => AdvanceChild(frame, ChildBank, new Harness().Inputs()));
        Assert.Throws<NotSupportedException>(() => WriteCompletionByte(frame, CompletionGlobal + 1, 1));
    }

    private static NativeBattleEffect344Frame AtAge(ushort age)
    {
        NativeBattleEffect344Frame frame = Active();
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age));
    }

    private static NativeBattleEffect344Frame Active() => Initialize(Cold(), Resources(), new Harness().Inputs());

    private static NativeBattleEffect344Frame Cold()
    {
        NativeBattleOriginalEffectState effect = new(344, 343, new(344, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleEffect344State.FromColdProcess());
    }

    private static NativeBattleEffect344Resources Resources()
    {
        byte[] music = new byte[MusicLength];
        BinaryPrimitives.WriteInt32LittleEndian(music, 2);
        BinaryPrimitives.WriteInt32LittleEndian(music.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(music.AsSpan(8), 129584);
        BinaryPrimitives.WriteInt32LittleEndian(music.AsSpan(12), MusicLength);
        "AKAO"u8.CopyTo(music.AsSpan(129584));
        music[129588] = 91;
        BinaryPrimitives.WriteUInt16LittleEndian(music.AsSpan(129590), 10528);
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        new byte[] { 6, 0, 0, 2 }.CopyTo(camera, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(14), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(16), 508);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(20), 8);
        NativeBattleEffect344Resources resources = new(new("synthetic-music", music.ToImmutableArray()), new("synthetic-camera", camera.ToImmutableArray()));
        Assert.Equal(24, new NativeBattleCameraBank(camera.AsSpan(12).ToArray().ToImmutableArray()).TrackOffset(0));
        resources.Validate();
        return resources;
    }

    private static NativeBattleActionPacketsState Packets(byte first = 6)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(4, 8, 16, -1, 368, 344, null));
        foreach (byte actor in new byte[] { first, 1 })
        {
            (packets, _) = packets.AppendContact(new(new(actor, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(2);
        return packets;
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public bool ChangeContactOnGain { get; set; }
        public NativeBattleActionPacketsState CurrentPackets { get; set; } = Packets();
        public List<NativeBattleEffect344Operation> Operations { get; } = [];

        public NativeBattleEffect344Inputs Inputs() => new(frame => Flags, frame => CurrentPackets, (frame, operation) =>
        {
            Operations.Add(operation);
            if (ChangeContactOnGain && operation is NativeBattleEffect344MusicGain501B10)
            {
                CurrentPackets = CurrentPackets with { Headers = CurrentPackets.Headers.SetItem(0, CurrentPackets.Headers[0] with { FirstContactIndex = 1 }) };
            }
            return frame;
        });
    }
}
