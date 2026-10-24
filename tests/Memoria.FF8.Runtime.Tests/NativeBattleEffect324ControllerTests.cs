using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect324ControllerTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0x200u)]
    [InlineData(0x201u)]
    public void Point_query_precedes_pause_and_retains_the_actual_w_word(uint flags)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect324Step result = AdvanceMain596C70(Frame(1), ChildBank, harness.Inputs());
        Assert.Equal(1, harness.PointQueries);
        Assert.Equal(ModelAddress(3), harness.LastModel);
        Assert.Equal((short)0xF0, harness.LastSelector);
        Assert.Equal((short)4096, harness.LastFraction);
        Assert.Equal(777u, result.Frame.State.ReadDword(InitializerGlobal));
        Assert.Equal((ushort)0xCAFE, result.Frame.State.ReadWord(PointAddress + 6));
        Assert.Equal(67, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.State.Read(PointAddress, 2)));
        Assert.Equal(flags == 0 ? 2 : 1, Read(result.Frame, ChildBank).ReadWord(12));
        if (flags == 0)
        {
            NativeBattleEffect324Sound5013A0 sound = Assert.IsType<NativeBattleEffect324Sound5013A0>(Assert.Single(harness.Operations));
            Assert.Equal(SoundAddress, sound.SourceAddress);
            Assert.Equal(result.Frame.State.Read(PointAddress, 8), sound.Point8.Read(0, 8));
        }
        else
        {
            Assert.Empty(harness.Operations);
        }
    }

    [Fact]
    public void Vibration_start_writes_the_returned_handle_through_the_operation_destination()
    {
        Harness harness = new();
        NativeBattleEffect324Step result = AdvanceMain596C70(Frame(0), ChildBank, harness.Inputs());
        Assert.Equal(new NativeBattleEffect324VibrationStart4A29A0(VibrationAddress, 1, 128, VibrationGlobal), Assert.Single(harness.Operations));
        Assert.Equal(unchecked((uint)-17), result.Frame.State.ReadDword(VibrationGlobal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Vibration_stop_depends_on_current_query_and_rereads_its_handle(bool active)
    {
        Harness harness = new() { VibrationActive = active };
        NativeBattleEffect324Frame frame = Frame(197);
        frame = frame with { State = frame.State.WriteDword(VibrationGlobal, unchecked((uint)-123)) };
        NativeBattleEffect324Step result = AdvanceMain596C70(frame, ChildBank, harness.Inputs());
        Assert.Equal(-123, harness.QueriedHandle);
        Assert.Equal(1, harness.VibrationQueries);
        Assert.Equal(198, Read(result.Frame, ChildBank).ReadWord(12));
        if (active)
        {
            Assert.Equal(new NativeBattleEffect324VibrationStop4A2940(-456), Assert.Single(harness.Operations));
        }
        else
        {
            Assert.Empty(harness.Operations);
        }
    }

    [Theory]
    [InlineData(8, 0x597410u)]
    [InlineData(18, 0x597520u)]
    public void Scheduled_single_birth_preserves_other_payload_bytes(int age, uint callback)
    {
        Harness harness = new();
        NativeBattleEffect324Frame result = AdvanceMain596C70(Frame(age), ChildBank, harness.Inputs()).Frame;
        NativeBattleModulePoolTask child = result.Effect.Pools[1].Slots[1];
        Assert.Equal(callback, child.Callback);
        Assert.Equal(0, child.ReadWord(12));
        Assert.Equal(0xABAB, child.ReadWord(16));
        Assert.Equal(age == 8 ? 3 : 0xABAB, child.ReadWord(14));
    }

    [Fact]
    public void Age_twelve_orders_both_births_and_initializes_only_the_driver_counter()
    {
        Harness harness = new();
        NativeBattleEffect324Frame result = AdvanceMain596C70(Frame(12), ChildBank, harness.Inputs()).Frame;
        NativeBattleEffectTaskPool pool = result.Effect.Pools[1];
        Assert.Equal(0x596E30u, pool.Slots[1].Callback);
        Assert.Equal(0x597130u, pool.Slots[2].Callback);
        Assert.Equal(0, Read(result, ChildBank).ReadWord(14));
        Assert.Equal(0xABAB, pool.Slots[1].ReadWord(14));
        Assert.Equal(0xABAB, pool.Slots[2].ReadWord(14));
    }

    [Fact]
    public void Six_odd_late_births_alternate_eye_side_without_resetting_the_counter()
    {
        Harness harness = new();
        NativeBattleEffect324Frame frame = Frame(125);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 0));
        for (int index = 0; index < 6; index++)
        {
            int age = 125 + index * 2;
            frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, (ushort)age));
            frame = AdvanceMain596C70(frame, ChildBank, harness.Inputs()).Frame;
            NativeBattleModulePoolTask eye = frame.Effect.Pools[1].Slots[index + 1];
            Assert.Equal(0x597870u, eye.Callback);
            Assert.Equal(index & 1, eye.ReadWord(14));
            Assert.Equal(2048, eye.ReadWord(28));
        }
        Assert.Equal(6, Read(frame, ChildBank).ReadWord(14));
        Assert.Equal(7, frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
    }

    [Theory]
    [InlineData(124)]
    [InlineData(126)]
    [InlineData(136)]
    public void Outside_eye_birth_domain_does_not_append(int age)
    {
        NativeBattleEffect324Frame frame = AdvanceMain596C70(Frame(age), ChildBank, new Harness().Inputs()).Frame;
        Assert.Single(frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void Contact_uses_the_current_absolute_header_index_at_old_age130()
    {
        Harness harness = new();
        harness.Packets = harness.Packets with { Headers = harness.Packets.Headers.SetItem(0, harness.Packets.Headers[0] with { FirstContactIndex = 7 }) };
        NativeBattleEffect324Step result = AdvanceMain596C70(Frame(130), ChildBank, harness.Inputs());
        Assert.Equal(new NativeBattleEffect324Contact506690(7), Assert.Single(harness.Operations));
        Assert.Equal(131, Read(result.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(199, 0, 200)]
    [InlineData(200, 2, 201)]
    [InlineData(-1, 0, 0)]
    public void Driver_retirement_compares_signed_age_after_increment(int age, int expectedResult, int expectedAge)
    {
        NativeBattleEffect324Step result = AdvanceMain596C70(Frame(age), ChildBank, new Harness().Inputs());
        Assert.Equal(expectedResult, result.Result);
        Assert.Equal(expectedAge, Read(result.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Root_switches_packet_banks_and_increments_even_when_the_driver_is_paused()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect324Frame frame = Frame(30);
        NativeBattleEffect324Step first = AdvanceRoot5979A0(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(FirstPacketAddress, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadWord(12));
        Assert.Equal(30, Read(first.Frame, ChildBank).ReadWord(12));
        NativeBattleEffect324Step second = AdvanceRoot5979A0(first.Frame, RootBank, null!, harness.Inputs());
        Assert.Equal(SecondPacketAddress, second.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(2, Read(second.Frame, RootBank).ReadWord(12));
        Assert.Equal(2, harness.PointQueries);
        Assert.Equal(0, second.Result);
    }

    [Fact]
    public void Retirement_preserves_orphan_flags_and_unknown_fixed_bank_payloads()
    {
        NativeBattleEffect324Frame frame = Frame(20);
        NativeBattleEffectTaskPool child = frame.Effect.Pools[1];
        child = child with { Head = null, Tail = null, Slots = child.Slots.SetItem(0, child.Slots[0].MarkPayloadUnknown(20, 1)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, child) } };
        NativeBattleEffect324Frame result = Advance(frame, null!, new Harness().Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        NativeBattleEffectArena bank = result.State.ReadBank(ChildBank);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(bank.Read(0, 2)));
        Assert.False(bank.ReadSnapshot(20, 1).Known[0]);
        NativeBattleEffect324Frame restored = JsonSerializer.Deserialize<NativeBattleEffect324Frame>(JsonSerializer.Serialize(result))!;
        Assert.False(restored.State.ReadBank(ChildBank).ReadSnapshot(20, 1).Known[0]);
    }

    [Fact]
    public void Record_initializer_changes_only_the100_marker_dwords()
    {
        NativeBattleEffect324Frame frame = Frame(0);
        byte[] before = Enumerable.Repeat((byte)0xAD, RecordStride * RecordCount).ToArray();
        frame = frame with { State = frame.State.Write(RecordsAddress, before) };
        frame = InitializeRecords(frame);
        byte[] after = frame.State.Read(RecordsAddress, before.Length);
        for (int offset = 0; offset < after.Length; offset++)
        {
            Assert.Equal(offset % RecordStride < 4 ? (byte)0 : (byte)0xAD, after[offset]);
        }
    }

    [Fact]
    public void Exhausted_mandatory_birth_fails_without_aging_the_driver()
    {
        NativeBattleEffect324Frame frame = Frame(8);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        while (children.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (children, _) = children.Append(0x597410);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        Assert.Throws<InvalidDataException>(() => AdvanceMain596C70(frame, ChildBank, new Harness().Inputs()));
        Assert.Equal(8, Read(frame, ChildBank).ReadWord(12));
    }

    private static NativeBattleEffect324Frame Frame(int age)
    {
        ImmutableArray<NativeBattleEffectTaskPool> pools = [];
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 12, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xAB, shape.Stride - 12).ToArray()), shape.Stride - 12);
            if (shape.Descriptor == ChildDescriptor)
            {
                for (int index = 1; index < shape.Capacity; index++)
                {
                    pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, index * shape.Stride + 12, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xAB, shape.Stride - 12).ToArray()), shape.Stride - 12);
                }
            }
            (pool, _) = pool.Append(shape.Descriptor == RootDescriptor ? RootCallback : MainCallback);
            pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, unchecked((ushort)(shape.Descriptor == RootDescriptor ? 0 : age)))) };
            pools = pools.Add(pool);
        }
        NativeBattleEffect324State state = NativeBattleEffect324State.FromColdProcess().DetachBank(0).DetachBank(1).WriteDword(CasterGlobal, 3).WriteWord(PointAddress + 6, 0xCAFE);
        NativeBattleOriginalEffectState effect = new(324, 323, new(324, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public bool VibrationActive { get; init; }
        public int PointQueries { get; private set; }
        public int VibrationQueries { get; private set; }
        public int QueriedHandle { get; private set; }
        public uint LastModel { get; private set; }
        public short LastSelector { get; private set; }
        public short LastFraction { get; private set; }
        public List<NativeBattleEffect324Operation> Operations { get; } = [];
        public NativeBattleActionPacketsState Packets { get; set; } = NativeBattleActionPacketsState.Fresh();
        public NativeBattleEffect324Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets, (_, _) => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, model, selector, fraction, retained) =>
        {
            PointQueries++;
            LastModel = model;
            LastSelector = selector;
            LastFraction = fraction;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 777) }, PutPoint(retained, 0, new(67, -89, 123)));
        }, (_, _) => [], (_, _) => 0, (frame, handle) =>
        {
            VibrationQueries++;
            QueriedHandle = handle;
            return new(frame with { State = frame.State.WriteDword(VibrationGlobal, unchecked((uint)-456)) }, VibrationActive);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return operation is NativeBattleEffect324VibrationStart4A29A0 start ? frame with { State = frame.State.WriteDword(start.DestinationAddress, unchecked((uint)-17)) } : frame;
        });
    }
}
