using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65ControllerTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(32767, 32768)]
    [InlineData(-32768, 32769)]
    public void Main_clock_wraps_as_a_signed_word_without_private_pause_gating(short age, ushort next)
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, unchecked((ushort)age)));
        frame = frame with { State = frame.State.WriteDword(ControlGlobal, 1) };
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Step step = AdvanceMain6E07D0(frame, ArenaAddress, Resources(), Inputs(operations));
        Assert.Equal(0, step.Result);
        Assert.Equal(next, Read(step.Frame, ArenaAddress).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Single(operations.OfType<NativeBattleEffect65IoAdvance534210>());
        Assert.Empty(operations.OfType<NativeBattleEffect65Contacts506BA0>());
    }

    [Theory]
    [InlineData(true, 2, 552)]
    [InlineData(false, 0, 562)]
    public void Final_selector_uses_complete_polarity_and_keeps_the_native_retry_clock(bool complete, int result, ushort clock)
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, 551));
        int selections = 0;
        NativeBattleEffect65Inputs inputs = Inputs() with { Select534300 = (current, selector) =>
        {
            Assert.Equal(2, selector);
            selections++;
            return new(current with { State = current.State.WriteDword(RandomGlobal, 0x1234) }, complete);
        } };
        NativeBattleEffect65Step step = AdvanceMain6E07D0(frame, ArenaAddress, Resources(), inputs);
        Assert.Equal(result, step.Result);
        Assert.Equal(clock, Read(step.Frame, ArenaAddress).ReadWord(12));
        Assert.Equal(0x1234u, step.Frame.State.ReadDword(RandomGlobal));
        Assert.Equal(1, selections);
    }

    [Fact]
    public void Texture_reloads_are_queued_to_the_current_first_private_bank_in_order()
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, 543));
        List<NativeBattleEffect65Operation> operations = [];
        AdvanceMain6E07D0(frame, ArenaAddress, Resources(), Inputs(operations));
        NativeBattleEffect65IoQueue5341D0[] queued = operations.OfType<NativeBattleEffect65IoQueue5341D0>().ToArray();
        Assert.Equal(new short[] { 360, 361, 362 }, queued.Select(item => item.FileId));
        Assert.Equal(new byte[] { 1, 1, 0 }, queued.Select(item => item.Type));
        Assert.All(queued, item => Assert.Equal(ArenaAddress + FirstPacketOffset, item.DestinationAddress));
        Assert.IsType<NativeBattleEffect65IoAdvance534210>(operations[^1]);
    }

    [Fact]
    public void Contact_uses_the_current_header_pointer_after_vibration_stop()
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = frame with { Effect = frame.Effect with { Parameters = new(0, 0, 0, 5, 0, 0) } };
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, 548).WriteDword(0x10, 99));
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Step step = AdvanceMain6E07D0(frame, ArenaAddress, Resources(), Inputs(operations));
        Assert.Equal(99, Assert.Single(operations.OfType<NativeBattleEffect65StopVibration4A2940>()).Handle);
        Assert.Equal(5, Assert.Single(operations.OfType<NativeBattleEffect65Contacts506BA0>()).HeaderIndex);
        Assert.True(operations.FindIndex(item => item is NativeBattleEffect65StopVibration4A2940) < operations.FindIndex(item => item is NativeBattleEffect65Contacts506BA0));
        Assert.Equal(549, Read(step.Frame, ArenaAddress).ReadWord(12));
    }

    [Fact]
    public void Root_paused_before_birth_still_toggles_packet_banks_but_keeps_its_clock()
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(ControlGlobal, 1) };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        NativeBattleEffect65Step first = AdvanceRoot6E03F0(frame, RootBank, Resources(), Inputs());
        NativeBattleEffect65Step second = AdvanceRoot6E03F0(first.Frame, RootBank, Resources(), Inputs());
        Assert.Equal(ArenaAddress + SecondPacketOffset, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(ArenaAddress + FirstPacketOffset, second.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1, Read(second.Frame, RootBank).ReadWord(12));
        Assert.Equal(0, Read(second.Frame, RootBank).ReadByte(15));
        Assert.Equal(frame.Random, second.Frame.Random);
    }

    [Fact]
    public void Clearing_the_child_queue_keeps_orphan_flags_links_payload_and_unknown_lanes()
    {
        NativeBattleEffect65Frame frame = InitializeChildBank(Frame());
        (frame, uint first) = Append(frame, ChildDescriptor, ChildCallback);
        (frame, uint second) = Append(frame, ChildDescriptor, ChildCallback);
        frame = Write(frame, first, Read(frame, first).WriteDword(0x14, 0xAABBCCDD).MarkPayloadUnknown(0x90, 2));
        NativeBattleEffectTaskPool before = frame.Effect.Pools[2];
        frame = WriteBytes(frame, ChildDescriptor, NativeBattleEffectArena.Clear().Write(0, new byte[8]), 8);
        NativeBattleEffectTaskPool after = frame.Effect.Pools[2];
        Assert.Null(after.Head);
        Assert.Null(after.Tail);
        Assert.Equal(before.Slots, after.Slots);
        Assert.Equal(1, Read(frame, first).Next);
        Assert.Equal(0xAABBCCDDu, Read(frame, first).ReadDword(0x14));
        Assert.Equal(1, Read(frame, second).Flags);
        Assert.Throws<NotSupportedException>(() => Read(frame, first).ReadWord(0x90));
        NativeBattleEffect65Frame restored = JsonSerializer.Deserialize<NativeBattleEffect65Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(Read(frame, first).Payload.ToArray(), Read(restored, first).Payload.ToArray());
        Assert.Throws<NotSupportedException>(() => Read(restored, first).ReadWord(0x90));
    }

    [Fact]
    public void Root_retirement_detaches_residual_child_records_instead_of_erasing_them()
    {
        NativeBattleEffect65Frame frame = InitializeChildBank(Frame());
        (frame, uint child) = Append(frame, ChildDescriptor, ChildCallback);
        frame = Write(frame, child, Read(frame, child).WriteDword(0x14, 0xA5A5A5A5));
        frame = WriteBytes(frame, ChildDescriptor, NativeBattleEffectArena.Clear().Write(0, new byte[8]), 8);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 2).WriteByte(15, 1));
        frame = Write(frame, ArenaAddress, Read(frame, ArenaAddress).WriteWord(12, 551));
        NativeBattleEffect65Frame result = Advance(frame, Resources(), Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.RootBankStorage);
        Assert.NotNull(result.State.ChildQueueStorage);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena!.Read(ChildOffset, 2)));
        Assert.Equal(ChildCallback, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena.Read(ChildOffset + 8, 4)));
        Assert.Equal(0xA5A5A5A5u, BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena.Read(ChildOffset + 0x14, 4)));
    }

    [Fact]
    public void Shard_birth_uses_the_allocator_and_birth_crt_draws_and_preserves_the_shared_private_seed()
    {
        NativeBattleEffect65Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(RandomGlobal, 0x4321) };
        frame = NativeBattleEffect65Particles.Initialize6E2E20(frame);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 128 * 13; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect65Frame result = BirthShards(frame, new(10, 20, 30));
        Assert.Equal(expected, result.Random);
        Assert.Equal(0x4321u, result.State.ReadDword(RandomGlobal));
        int live = 0;
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena row = ReadBytes(result, ArenaAddress + 0x3B530 + (uint)(index * 22), 22);
            Assert.InRange(BinaryPrimitives.ReadUInt16LittleEndian(row.Read(14, 2)), (ushort)1024, (ushort)17407);
            Assert.InRange(row.Read(7, 1)[0], (byte)0, (byte)7);
            live++;
        }
        Assert.Equal(128, live);
    }

    [Theory]
    [InlineData(31, null)]
    [InlineData(166, 0)]
    [InlineData(237, 1728)]
    [InlineData(263, 1248)]
    [InlineData(289, 4096)]
    [InlineData(315, 3744)]
    [InlineData(471, null)]
    public void Camera_keyframes_publish_xyz_and_only_the_native_roll_writes(int age, int? roll)
    {
        NativeBattleEffect65Frame frame = Frame();
        List<NativeBattleEffect65Operation> operations = [];
        AdvanceCamera(frame, age, Inputs(operations));
        Assert.Single(operations.OfType<NativeBattleEffect65CameraPoseXYZ>());
        NativeBattleEffect65CameraRoll[] rolls = operations.OfType<NativeBattleEffect65CameraRoll>().ToArray();
        if (roll is int value)
        {
            Assert.Equal(value, Assert.Single(rolls).Roll);
        }
        else
        {
            Assert.Empty(rolls);
        }
        Assert.DoesNotContain(operations, item => item is NativeBattleEffect65CameraRestoreCaptured503300);
    }

    private static NativeBattleEffect65Frame Frame()
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        NativeBattleOriginalEffectState effect = new(65, 64, new(65, Entry, Initializer), null, null) { Pools = [root], ReturnedDescriptor = RootDescriptor };
        NativeBattleEffect65Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(123), NativeBattleEffect65State.FromColdProcess() with { RootBankStorage = null });
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, ArenaAddress).WriteDword(CasterGlobal, ModelAddress(5)) };
        frame = InitializeMainBank(frame);
        (frame, _) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, ArenaAddress + 12, 0x36B);
        return frame;
    }

    private static NativeBattleEffect65Inputs Inputs(List<NativeBattleEffect65Operation>? operations = null) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, null!, null!, null!, null!, null!, null!, current => new(current, false), (current, _) => new(current, true), (current, operation) =>
    {
        operations?.Add(operation);
        return current;
    });

    private static NativeBattleEffect65Resources Resources() => new(new("texture", []), new("vibration", [], []), new(0, SpriteAddress, 0, 0, []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<short, NativeBattleEffectFileAsset>.Empty);
}
