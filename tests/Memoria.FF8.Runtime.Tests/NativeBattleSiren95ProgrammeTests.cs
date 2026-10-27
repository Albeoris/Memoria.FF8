using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ProgrammeTests
{
    private const uint Camera = 0x258E7B0;
    private const uint Director = 0x258EAA8;

    [Fact]
    public void First_phase_wait_does_not_read_unknown_camera_then_initializes_actual_anchor_and_raw_padding()
    {
        NativeBattleSiren95Frame frame = WithProgramme(Frame(), 0);
        NativeBattleSiren95State state = frame.Memory.Siren95!;
        frame = frame with { Memory = frame.Memory with { Siren95 = state with { CameraAddress15339D0 = null } } };
        NativeBattleSiren95ProgrammeServices unused = Services([]);
        var waiting = NativeBattleSiren95Programme.Advance73A9E0(frame, 0x257FAA0, unused);
        Assert.Equal((byte)0, waiting.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        frame = Put(Frame(), Director + 8, Words(11, 22, 33, unchecked((short)0xBEEF)));
        frame = Word(WithProgramme(frame, 0), Director + 0x42, 1);
        List<uint> reads = [];
        List<NativeBattleMoogleCameraStep> publications = [];
        NativeBattleSiren95ProgrammeServices services = Services(new() { [0x1533C40] = CameraSource() }, reads, publications);
        var initialized = NativeBattleSiren95Programme.Advance73A9E0(frame, 0x257FAA0, services);
        Assert.Equal((byte)1, initialized.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal(new uint[] { 0x1533C40 }, reads);
        Assert.Single(publications);
        Assert.Equal(Words(11, 22, 33, unchecked((short)0xBEEF)), initialized.Frame.Memory.Siren95!.Read(Camera + 0xA4, 8));
        Assert.Throws<NotSupportedException>(() => initialized.Frame.Memory.Siren95.Read(Camera + 0x5A, 2));
        NativeBattleSiren95Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(NativeBattleTestSnapshots.Json(initialized.Frame))!;
        var first = NativeBattleSiren95Programme.Advance73A9E0(initialized.Frame, 0x257FAA0, services);
        var second = NativeBattleSiren95Programme.Advance73A9E0(restored, 0x257FAA0, services);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Frame), NativeBattleTestSnapshots.Json(second.Frame));
    }

    [Fact]
    public void Camera_advance_precedes_signed_countdown_wrap_and_phase_request_is_not_premature()
    {
        NativeBattleSiren95Frame frame = WithProgramme(Frame(), 9);
        List<NativeBattleMoogleCameraStep> publications = [];
        NativeBattleSiren95ProgrammeServices services = Services([], publications: publications);
        var result = NativeBattleSiren95Programme.Advance73A9E0(frame, 0x257FAA0, services);
        Assert.Single(publications);
        Assert.Equal(ushort.MaxValue, result.Frame.Effect.Pools[0].Slots[0].ReadWord(0xDC));
        Assert.Equal((byte)10, result.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, result.Frame.Memory.Siren95!.ReadWord(Director + 0x44));
        frame = Word(Word(WithProgramme(Frame(), 7), Director + 0x40, 4), Director + 0x44, 3);
        Assert.Equal((byte)7, NativeBattleSiren95Programme.Advance73A9E0(frame, 0x257FAA0, services).Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        frame = Word(frame, Director + 0x44, 4);
        result = NativeBattleSiren95Programme.Advance73A9E0(frame, 0x257FAA0, services);
        Assert.Equal((byte)8, result.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)5, result.Frame.Memory.Siren95!.ReadWord(Director + 0x44));
    }

    private static NativeBattleSiren95ProgrammeServices Services(Dictionary<uint, byte[]> sources, List<uint>? reads = null, List<NativeBattleMoogleCameraStep>? publications = null) => new(
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
        });

    private static NativeBattleSiren95Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess(), new(95, 94, new(95, 0x739DA0, 0x739D80), new(0, 1, 8, 0, 0, 0), null), new(1));
    private static NativeBattleSiren95Frame WithProgramme(NativeBattleSiren95Frame frame, byte phase)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x257FA90, 0x257FAA0, 0xE0, 110);
        (pool, int slot) = pool.Append(0x73A9E0);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x29, phase)) });
        return frame with { Effect = frame.Effect with { Pools = [pool] } };
    }
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, byte[] data) => frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95!.Write(address, data) } };
    private static NativeBattleSiren95Frame Word(NativeBattleSiren95Frame frame, uint address, short value) => Put(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleSiren95Frame Dword(NativeBattleSiren95Frame frame, uint address, uint value) => Put(frame, address, BitConverter.GetBytes(value));
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
