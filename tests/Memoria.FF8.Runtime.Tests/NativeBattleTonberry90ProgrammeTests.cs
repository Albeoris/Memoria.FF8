using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90ProgrammeTests
{
    private const uint Camera = 0x25A3D00;
    private const uint Director = 0x25A3DF0;

    [Theory]
    [InlineData(0, 0, 1, true)]
    [InlineData(1, 0, 2, false)]
    [InlineData(2, 2, 2, false)]
    [InlineData(-32768, -32768, -32767, true)]
    [InlineData(32767, 32767, -32768, false)]
    public void Request_requires_current_spawned_requested_equality_and_signed_previous_phase(short spawned, short requested, short next, bool accepted)
    {
        NativeBattleTonberry90Frame frame = Frame();
        frame = Word(Word(frame, Director + 0x40, spawned), Director + 0x44, requested);
        var result = NativeBattleTonberry90Programme.RequestPhase763940(frame, next);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(unchecked((ushort)(accepted ? next : requested)), result.Frame.Memory.Tonberry90!.ReadWord(Director + 0x44));
        Assert.Equal(unchecked((ushort)spawned), result.Frame.Memory.Tonberry90.ReadWord(Director + 0x40));
    }

    [Fact]
    public void Scalar_start_substitution_truncation_sentinel_and_outer_transition_preserve_native_timing()
    {
        Dictionary<uint, byte[]> sources = new()
        {
            [0x1000] = Dwords(0x2000, 0x2100, 0),
            [0x2000] = Words(1, 0x7FFE, -10, 1, 4096, 0x7FFF),
            [0x2100] = Words(0, 99, 0x7FFF)
        };
        NativeBattleTonberry90ProgrammeServices services = Services(sources);
        NativeBattleTonberry90Frame frame = Word(Frame(), Camera + 0x5E, 10);
        frame = Dword(frame, Camera, 0x1000);
        frame = Word(frame, Camera + 0xE, 1234);
        frame = NativeBattleTonberry90Programme.InitializeChannel763740(frame, Camera, Camera + 0x5E, services);
        Assert.Equal((ushort)10, frame.Memory.Tonberry90!.ReadWord(Camera + 0xA));
        Assert.Equal((ushort)1234, frame.Memory.Tonberry90.ReadWord(Camera + 0xE));
        var first = NativeBattleTonberry90Programme.TickChannel763820(frame, Camera, Camera + 0x5E, services);
        Assert.False(first.Complete);
        Assert.Equal((ushort)10, first.Frame.Memory.Tonberry90!.ReadWord(Camera + 0x5E));
        var last = NativeBattleTonberry90Programme.TickChannel763820(first.Frame, Camera, Camera + 0x5E, services);
        Assert.Equal(unchecked((ushort)-10), last.Frame.Memory.Tonberry90!.ReadWord(Camera + 0x5E));
        var initialized = NativeBattleTonberry90Programme.TickChannel763820(last.Frame, Camera, Camera + 0x5E, services);
        Assert.False(initialized.Complete);
        Assert.Equal(0x1004u, initialized.Frame.Memory.Tonberry90!.ReadDword(Camera));
        Assert.Equal(unchecked((ushort)-10), initialized.Frame.Memory.Tonberry90.ReadWord(Camera + 0x5E));
        NativeBattleTonberry90Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleTonberry90Frame>(NativeBattleTestSnapshots.Json(initialized.Frame))!;
        var sampled = NativeBattleTonberry90Programme.TickChannel763820(restored, Camera, Camera + 0x5E, services);
        Assert.Equal((ushort)99, sampled.Frame.Memory.Tonberry90!.ReadWord(Camera + 0x5E));
        var ended = NativeBattleTonberry90Programme.TickChannel763820(sampled.Frame, Camera, Camera + 0x5E, services);
        Assert.True(ended.Complete);
        Assert.Equal((ushort)99, ended.Frame.Memory.Tonberry90!.ReadWord(Camera + 0x5E));
        Assert.Equal(0x2104u, ended.Frame.Memory.Tonberry90.ReadDword(Camera + 4));
    }

    [Fact]
    public void Scalar_uses_signed_product_truncation_and_signed_delay_age()
    {
        NativeBattleTonberry90ProgrammeServices services = Services(new() { [0x1000] = Words(short.MinValue) });
        NativeBattleTonberry90Frame frame = Dword(Frame(), Camera + 4, 0x1000);
        frame = Word(Word(Word(frame, Camera + 8, 1), Camera + 0xA, short.MinValue), Camera + 0xC, short.MaxValue);
        var result = NativeBattleTonberry90Programme.StepChannel763870(frame, Camera, Camera + 0x5E, services);
        int product = unchecked((short.MaxValue - short.MinValue) * (int)short.MinValue);
        Assert.Equal(unchecked((ushort)(short.MinValue + product / 4096)), result.Frame.Memory.Tonberry90!.ReadWord(Camera + 0x5E));
        frame = Word(Word(Word(result.Frame, Camera + 8, 2), Camera + 0x10, -1), Camera + 0xE6, short.MinValue);
        Assert.False(NativeBattleTonberry90Programme.StepChannel763870(frame, Camera, Camera + 0x5E, services).Complete);
        frame = Word(frame, Camera + 0xE6, -1);
        Assert.True(NativeBattleTonberry90Programme.StepChannel763870(frame, Camera, Camera + 0x5E, services).Complete);
    }

    [Fact]
    public void Four_channel_tick_publishes_once_after_age_increment_and_programme_countdown_wrap()
    {
        Dictionary<uint, byte[]> sources = new()
        {
            [0x1000] = Dwords(0x1100, 0x1200, 0x1300, 0x1400),
            [0x1100] = Dwords(0x2000, 0),
            [0x1200] = Dwords(0x2000, 0),
            [0x1300] = Dwords(0x2000, 0),
            [0x1400] = Dwords(0x2000, 0),
            [0x2000] = Words(2, 999)
        };
        List<NativeBattleMoogleCameraStep> publications = [];
        NativeBattleTonberry90ProgrammeServices services = Services(sources, publications: publications);
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Programme.InitializeChannels7636C0(WithProgramme(Frame(), 3), 0x1000, services);
        frame = Word(frame, Camera + 0xE6, -1);
        var result = NativeBattleTonberry90Programme.Advance762DA0(frame, 0x259F0A8, services);
        Assert.Single(publications);
        Assert.Equal((ushort)0, result.Frame.Memory.Tonberry90!.ReadWord(Camera + 0xE6));
        Assert.Equal(ushort.MaxValue, result.Frame.Effect.Pools[0].Slots[0].ReadWord(0x74));
        Assert.Equal((byte)4, result.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        foreach (int offset in new[] { 0, 0x14, 0x28, 0x3C })
        {
            Assert.Equal(0x2004u, result.Frame.Memory.Tonberry90.ReadDword(Camera + (uint)offset + 4));
        }
        NativeBattleTonberry90State state = frame.Memory.Tonberry90!;
        state = state with { Globals = state.Globals.MarkUnknown(checked((int)(Camera + 0xE - NativeBattleTonberry90State.GlobalAddress)), 2) };
        frame = frame with { Memory = frame.Memory with { Tonberry90 = state } };
        var delayed = NativeBattleTonberry90Programme.StepChannel763870(frame, Camera, Camera + 0x5E, services);
        Assert.False(delayed.Complete);
        Assert.Throws<NotSupportedException>(() => delayed.Frame.Memory.Tonberry90!.Read(Camera + 0xE, 2));
    }

    [Theory]
    [InlineData(-1, 0x154EB50u)]
    [InlineData(4095, 0x154EB50u)]
    [InlineData(4096, 0x154EB98u)]
    public void Programme_uses_signed_model_threshold_and_real_raw_anchor_then_immediate_camera_advance(short angle, uint expectedSource)
    {
        NativeBattleTonberry90Frame frame = WithProgramme(Frame(), 9);
        frame = Word(Word(frame, Director + 0x42, 6), Director + 0x4E, angle);
        frame = Put(frame, Director + 8, Words(11, 22, 33, unchecked((short)0xBEEF)));
        List<uint> reads = [];
        List<NativeBattleMoogleCameraStep> publications = [];
        NativeBattleTonberry90ProgrammeServices services = Services(new() { [expectedSource] = CameraSource() }, reads, publications);
        NativeBattleTonberry90TaskStep result = NativeBattleTonberry90Programme.Advance762DA0(frame, 0x259F0A8, services);
        Assert.Equal((byte)10, result.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal(new[] { expectedSource }, reads);
        Assert.Equal(2, publications.Count);
        Assert.Equal((ushort)1, result.Frame.Memory.Tonberry90!.ReadWord(Camera + 0xE2));
        Assert.Equal(Words(11, 22, 33, unchecked((short)0xBEEF)), result.Frame.Memory.Tonberry90.Read(Camera + 0xA4, 8));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Tonberry90.Read(Camera + 0x5A, 2));
        Assert.False(result.Complete);
    }

    [Fact]
    public void Programme_advance_precedes_phase_wait_and_sound_and_unknown_warm_owners_do_not_default()
    {
        NativeBattleTonberry90Frame frame = Word(WithProgramme(Frame(), 5), Director + 0x42, 3);
        List<string> order = [];
        NativeBattleTonberry90ProgrammeServices services = Services(new() { [0x154EAC0] = CameraSource() }) with
        {
            PublishCamera = (current, _) =>
            {
                order.Add("camera");
                return current;
            },
            PlaySound501330 = (current, descriptor, argument, volume) =>
            {
                Assert.Equal(0x1547170u, descriptor);
                Assert.Equal(0, argument);
                Assert.Equal(128, volume);
                order.Add("sound");
                return current;
            }
        };
        var result = NativeBattleTonberry90Programme.Advance762DA0(frame, 0x259F0A8, services);
        Assert.Equal(new[] { "camera", "sound", "camera" }, order);
        NativeBattleTonberry90State state = frame.Memory.Tonberry90!;
        frame = frame with { Memory = frame.Memory with { Tonberry90 = state with { CameraAddress1547510 = null } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleTonberry90Programme.Advance762DA0(frame, 0x259F0A8, services));
    }

    private static NativeBattleTonberry90ProgrammeServices Services(Dictionary<uint, byte[]> sources, List<uint>? reads = null, List<NativeBattleMoogleCameraStep>? publications = null) => new(
        (_, address, length) =>
        {
            KeyValuePair<uint, byte[]> owner = sources.Single(pair => address >= pair.Key && (ulong)address + (uint)length <= (ulong)pair.Key + (uint)pair.Value.Length);
            reads?.Add(address);
            return owner.Value.AsSpan(checked((int)(address - owner.Key)), length).ToArray();
        },
        (frame, step) =>
        {
            publications?.Add(step);
            return frame;
        },
        (_, _, _, _) => throw new InvalidOperationException("Sound was not reached."));

    private static NativeBattleTonberry90Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess(), new(90, 89, new(90, 0x762360, 0x762320), new(0, 1, 8, 0, 0, 0), null), new(1));
    private static NativeBattleTonberry90Frame WithProgramme(NativeBattleTonberry90Frame frame, byte phase)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x259F098, 0x259F0A8, 0x7C, 100);
        (pool, int slot) = pool.Append(0x762DA0);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x29, phase)) });
        return frame with { Effect = frame.Effect with { Pools = [pool] } };
    }
    private static NativeBattleTonberry90Frame Put(NativeBattleTonberry90Frame frame, uint address, byte[] data) => frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90!.Write(address, data) } };
    private static NativeBattleTonberry90Frame Word(NativeBattleTonberry90Frame frame, uint address, short value) => Put(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleTonberry90Frame Dword(NativeBattleTonberry90Frame frame, uint address, uint value) => Put(frame, address, BitConverter.GetBytes(value));
    private static byte[] Words(params short[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();
    private static byte[] Dwords(params uint[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();
    private static byte[] CameraSource()
    {
        byte[] source = new byte[0x48];
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(0x3A), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(0x3E), 4096);
        return source;
    }
}

