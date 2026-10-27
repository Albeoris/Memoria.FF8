using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect298Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect298ControllerTests
{
    [Fact]
    public void Entry_retains_COMMON_local_RNG_and_current_script_counter_owners()
    {
        Harness harness = new();
        var frame = Inactive();
        frame = frame with { State = frame.State.WriteDword(SeedGlobal, 0xABCD).WriteWord(ScriptCounterAddresses[0], 0) };
        var actual = Initialize(frame, Resources(), harness.Inputs());
        Assert.Single(actual.Effect.Pools);
        Assert.Null(actual.State.RootBankStorage);
        Assert.Equal(RootDescriptor, actual.Effect.ReturnedDescriptor);
        Assert.Equal(new byte[] { 0xA5 }, actual.Memory.Arena!.Read(0, 1));
        Assert.Equal(0xABCDu, actual.State.ReadDword(SeedGlobal));
        Assert.Equal(new byte[2], actual.State.ReadStatic(Resources(), ScriptCounterAddresses[0], 2));
        Assert.Equal(new byte[8], TaskView(Read(actual, RootBank), 12, 8).Read(0, 8));
        Assert.Empty(harness.Operations);
    }

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    [InlineData(0x80000000u, FirstPacketOffset, 0u)]
    public void Root_parity_reads_the_entire_DWORD(uint before, int expectedOffset, uint next)
    {
        Harness harness = new();
        var frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, before));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(ArenaAddress + (uint)expectedOffset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, Read(actual, RootBank).ReadWord(12));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void First_root_reset_creates_exact_banks_and_runs_main_before_scripts()
    {
        Harness harness = new();
        var frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(5, actual.Effect.Pools.Length);
        Assert.Equal(Pools.Select(pool => pool.Bank), actual.Effect.Pools.Select(pool => pool.BankAddress));
        Assert.Equal(new[] { 20, 36, 28, 328, 35316 }, actual.Effect.Pools.Select(pool => (int)pool.Stride));
        NativeBattleModulePoolTask main = actual.Effect.Pools[4].Slots[0];
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal(0x12345678u, main.ReadDword(16));
        Assert.Equal(new byte[] { 100, 0, 200, 0, 44, 1, 144, 1 }, TaskView(main, 20, 8).Read(0, 8));
        Assert.Equal(Pools[4].Bank + 0x2EC, main.ReadDword(0x89EC));
        Assert.Equal(Pools[4].Bank + 0x83EC, main.ReadDword(0x89F0));
        Assert.Equal(3, actual.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(new[] { "music", "vibration", "io-init", "body", "fields", "io-advance" }, harness.Log);
        Assert.Equal(0xCAFEu, actual.State.ReadDword(SeedGlobal));
        Assert.Null(actual.State.RootBankStorage);
    }

    [Theory]
    [InlineData((ushort)116, (ushort)115, false)]
    [InlineData((ushort)117, (ushort)1, true)]
    [InlineData((ushort)118, (ushort)1, true)]
    public void Root_stops_scanning_at117_and_queues_only_at118(ushort rootAge, ushort mainAge, bool mainRemains)
    {
        Harness harness = new();
        var frame = Active(mainAge);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, rootAge).WriteByte(15, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(mainRemains, (actual.Effect.Pools[4].Slots[0].Flags & 1) != 0);
        Assert.Equal(rootAge == 118 ? 1 : 0, harness.Operations.OfType<NativeBattleEffect298QueueFile5341D0>().Count());
        if (rootAge == 118)
        {
            Assert.Equal(new NativeBattleEffect298QueueFile5341D0(360, ArenaAddress + FirstPacketOffset, 0), harness.Operations.OfType<NativeBattleEffect298QueueFile5341D0>().Single());
        }
        Assert.Single(harness.Operations.OfType<NativeBattleEffect298AdvanceIo534210>());
        Assert.Equal(unchecked((ushort)(rootAge + 1)), Read(actual, RootBank).ReadWord(12));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public void Root_waits_at119_for_actual_IO_and_does_not_require_child_drain(bool busy, int result)
    {
        Harness harness = new() { Busy = busy };
        var frame = Active(17);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 119).WriteByte(15, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs());
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)119, Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)17, Read(actual.Frame, Pools[4].Bank).ReadWord(12));
        Assert.Equal(new[] { "io-advance", "io-poll-0" }, harness.Log);
    }

    [Fact]
    public void Completed_IO_detaches_exclusive_banks_while_preserving_live_payload()
    {
        Harness harness = new() { Busy = false };
        var frame = Active(17);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 119).WriteByte(15, 1));
        var actual = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Empty(actual.Effect.Pools);
        Assert.NotNull(actual.State.RootBankStorage);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena!.Read(MainOffset, 2)) & 1);
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena.Read(MainOffset + 12, 2)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect298Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)))))));
    }

    [Theory]
    [InlineData((ushort)115)]
    [InlineData((ushort)32767)]
    public void Main_returns_before_body_and_updates_at115(ushort age)
    {
        Harness harness = new();
        var actual = AdvanceMain(Active(age), Pools[4].Bank, Resources(), harness.Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal(age, Read(actual.Frame, Pools[4].Bank).ReadWord(12));
        Assert.Empty(harness.Operations);
    }

    [Theory]
    [InlineData((ushort)1, true, false)]
    [InlineData((ushort)111, false, true)]
    public void Main_uses_literal_sound_and_actual_vibration_handle(ushort age, bool sound, bool stop)
    {
        Harness harness = new();
        var actual = AdvanceMain(Active(age), Pools[4].Bank, Resources(), harness.Inputs());
        Assert.Equal(sound ? 1 : 0, harness.Operations.OfType<NativeBattleEffect298Sound501330>().Count());
        Assert.Equal(stop ? 1 : 0, harness.Operations.OfType<NativeBattleEffect298StopVibration4A2940>().Count());
        if (sound)
        {
            Assert.Equal(new NativeBattleEffect298Sound501330(Resources().Sound, 0, 128), harness.Operations.OfType<NativeBattleEffect298Sound501330>().Single());
        }
        if (stop)
        {
            Assert.Equal(0x12345678, harness.Operations.OfType<NativeBattleEffect298StopVibration4A2940>().Single().Handle);
        }
        Assert.Equal(unchecked((ushort)(age + 1)), Read(actual.Frame, Pools[4].Bank).ReadWord(12));
    }

    [Fact]
    public void Main_Z_is_a_WORD_wrap_and_does_not_restore_or_clear_model_bit4()
    {
        Harness harness = new();
        harness.Model = harness.Model with { Flags0 = 0x1802, Transform = harness.Model.Transform! with { Position28 = new(100, 200, short.MinValue) } };
        _ = AdvanceMain(Active(114), Pools[4].Bank, Resources(), harness.Inputs());
        Assert.Equal(new NativeBattleVector(100, 200, 32640), harness.Model.Transform!.Position28);
        Assert.Equal((ushort)0x1806, harness.Model.Flags0);
        Assert.Equal(new[] { "body", "fields", "fields" }, harness.Log);
    }

    [Theory]
    [InlineData((ushort)0, (ushort)0, false)]
    [InlineData((ushort)1, (ushort)0, true)]
    [InlineData((ushort)2, (ushort)1, false)]
    public void Large_row_expiration_and_bounce_preserve_native_partial_writes(ushort remaining, ushort expected, bool hint)
    {
        var frame = Active(0);
        uint row = Pools[4].Bank + 0x2EC;
        NativeBattleEffectArena data = NativeBattleEffectArena.Clear().Write(0, new byte[0xAC]).Write(2, BitConverter.GetBytes((short)1)).Write(6, BitConverter.GetBytes(remaining)).Write(10, BitConverter.GetBytes((short)4)).Write(20, BitConverter.GetBytes((short)7)).Write(22, BitConverter.GetBytes(short.MinValue)).MarkUnknown(24, 8);
        frame = WriteBytes(frame, row, data, 0xAC);
        var actual = AdvanceLargeRows672BB0(frame, Pools[4].Bank);
        NativeBattleEffectArena after = ReadBytes(actual, row, 0xAC);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(after.Read(6, 2)));
        if (remaining == 2)
        {
            Assert.Equal((short)5, BinaryPrimitives.ReadInt16LittleEndian(after.Read(2, 2)));
            Assert.Equal((short)-12, BinaryPrimitives.ReadInt16LittleEndian(after.Read(10, 2)));
            Assert.Equal((short)-7, BinaryPrimitives.ReadInt16LittleEndian(after.Read(20, 2)));
            Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(after.Read(22, 2)));
        }
        Assert.Equal(hint ? row : 0u, Read(actual, Pools[4].Bank).ReadDword(0x89EC));
        Assert.Throws<NotSupportedException>(() => after.Read(24, 8));
    }

    [Fact]
    public void Script_vector_assignment_addition_and_yield_preserve_W()
    {
        Harness harness = new();
        var resources = Script(0x1000, 32767, -1, 1, 0x1001, 1, 2, 3, 0x10FF);
        var frame = ScriptAt(resources);
        uint row = Pools[4].Bank + 28;
        frame = WriteBytes(frame, row + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0xBEEF)), 2);
        var actual = AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs());
        Assert.Equal(0, actual.Result);
        Assert.Equal(new short[] { short.MinValue, 1, 4 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(actual.Frame, row + (uint)(axis * 2), 2).Read(0, 2))));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(actual.Frame, row + 6, 2).Read(0, 2)));
        Assert.Equal(ScriptAddress + 0x100u + 18, Read(actual.Frame, Pools[1].Bank).ReadDword(16));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Script_bounce_uses_signed_arithmetic_shift_before_WORD_store()
    {
        Harness harness = new();
        var resources = Script(0x1002, 0, -5, 0, 0x1004, 0x10FF);
        var frame = ScriptAt(resources);
        uint row = Pools[4].Bank + 28;
        frame = WriteBytes(frame, row + 2, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)10)), 2);
        var actual = AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs()).Frame;
        Assert.Equal(new byte[2], ReadBytes(actual, row + 2, 2).Read(0, 2));
        Assert.Equal((ushort)3, Read(actual, Pools[1].Bank).ReadWord(22));
    }

    [Theory]
    [InlineData((short)0x1080, 0, 16)]
    [InlineData((short)0x1081, 0, 8)]
    [InlineData((short)0x1082, 8, 8)]
    public void Script_row_copy_keeps_actual_byte_knownness(short opcode, int offset, int length)
    {
        Harness harness = new();
        var resources = Script(opcode, 2, 0x10FF);
        var frame = ScriptAt(resources);
        uint row = Pools[4].Bank + 28;
        var source = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 16).Select(value => (byte)(value + 1)).ToArray()).MarkUnknown(6, 2).MarkUnknown(14, 2);
        frame = WriteBytes(frame, row, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 16).ToArray()), 16);
        frame = WriteBytes(frame, row + 32, source, 16);
        var actual = AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs()).Frame;
        Assert.Equal(source.ReadSnapshot(offset, length).Data, ReadBytes(actual, row + (uint)offset, length).ReadSnapshot(0, length).Data);
        Assert.Equal(source.ReadSnapshot(offset, length).Known, ReadBytes(actual, row + (uint)offset, length).ReadSnapshot(0, length).Known);
        if (length == 8)
        {
            int untouched = offset == 0 ? 8 : 0;
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), ReadBytes(actual, row + (uint)untouched, 8).Read(0, 8));
        }
    }

    [Fact]
    public void Script_loop_decrements_current_PE_counter_and_restores_only_on_zero()
    {
        Harness harness = new();
        var resources = Resources();
        uint counter = ScriptCounterAddresses[0];
        var frame = ScriptAt(resources, counter - 2);
        frame = frame with { State = frame.State.WriteWord(counter, 1) };
        var first = AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs()).Frame;
        Assert.Equal(new byte[2], first.State.ReadStatic(resources, counter, 2));
        Assert.Equal(counter - 2, Read(first, Pools[1].Bank).ReadDword(16));
        var cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect298Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        var second = AdvanceScript670C60(cold, Pools[1].Bank, resources, harness.Inputs()).Frame;
        Assert.Equal((ushort)17, BinaryPrimitives.ReadUInt16LittleEndian(second.State.ReadStatic(resources, counter, 2)));
        Assert.Equal(counter + 8, Read(second, Pools[1].Bank).ReadDword(16));
    }

    [Fact]
    public void Script_unknown_counter_rejects_resource_seed_and_1100_returns2_without_cursor_write()
    {
        Harness harness = new();
        var resources = Resources();
        var frame = ScriptAt(resources, ScriptCounterAddresses[0] - 2);
        frame = frame with { State = frame.State with { ScriptCounterWrites = NativeBattleEffect298State.FromUnknown().ScriptCounterWrites } };
        Assert.Throws<NotSupportedException>(() => AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs()));
        resources = Script(0x1100);
        frame = ScriptAt(resources);
        uint cursor = Read(frame, Pools[1].Bank).ReadDword(16);
        var actual = AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal(cursor, Read(actual.Frame, Pools[1].Bank).ReadDword(16));
    }

    [Theory]
    [InlineData((ushort)65535, (ushort)65534)]
    [InlineData((ushort)32768, (ushort)32767)]
    public void Script_loop_nonzero_signed_counts_wrap_the_low_WORD(ushort count, ushort expected)
    {
        Harness harness = new();
        var resources = Resources();
        uint counter = ScriptCounterAddresses[0];
        var frame = ScriptAt(resources, counter - 2);
        frame = frame with { State = frame.State.WriteWord(counter, count) };
        var actual = AdvanceScript670C60(frame, Pools[1].Bank, resources, harness.Inputs()).Frame;
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(actual.State.ReadStatic(resources, counter, 2)));
        Assert.Equal(counter - 2, Read(actual, Pools[1].Bank).ReadDword(16));
    }

    [Fact]
    public void Mandatory_script_allocation_does_not_silently_skip_a_full_pool()
    {
        var frame = Active(0);
        var pool = frame.Effect.Pools[1];
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            (pool, _) = pool.Append(ScriptCallback);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        Assert.Throws<InvalidDataException>(() => BirthScript670C10(frame, ScriptAddress, Resources()));
    }

    private static NativeBattleEffect298Resources Script(params short[] words)
    {
        var resources = Resources();
        byte[] bytes = resources.ScriptBytes.ToArray();
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x100 + index * 2), words[index]);
        }
        return resources with { ScriptBytes = bytes.ToImmutableArray() };
    }

    private static NativeBattleEffect298Frame ScriptAt(NativeBattleEffect298Resources resources, uint? cursor = null)
    {
        var frame = Active(0);
        (var pool, _) = frame.Effect.Pools[1].Append(ScriptCallback);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        frame = ClearDwords701200(frame, Pools[1].Bank + 12, 6);
        return Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).WriteDword(12, Pools[4].Bank + 28).WriteDword(16, cursor ?? ScriptAddress + 0x100));
    }

    private static NativeBattleEffect298Frame Active(ushort mainAge)
    {
        var frame = Inactive();
        var pools = Pools.Select(profile => NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity)).ToImmutableArray();
        (var main, _) = pools[4].Append(MainCallback);
        (var root, _) = pools[0].Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = pools.SetItem(4, main).SetItem(0, root), ReturnedDescriptor = RootDescriptor }, State = frame.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(3)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null } };
        frame = ClearDwords701200(frame, Pools[4].Bank + 12, 0x227A);
        return Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(12, mainAge).WriteDword(16, 0x12345678));
    }

    private static NativeBattleEffect298Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0, new byte[] { 0xA5 }) };
        NativeBattleOriginalEffectState effect = new(298, 297, new(298, Entry, Initializer), new(3, 0, 0x7F, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, NativeBattleEffect298State.FromColdProcess().WriteDword(SeedGlobal, 0xCAFE)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public NativeBattleGraphicsModel Model { get; set; } = new(3, 2) { Transform = new(new(0, 0, 0), new(100, 200, 300), 400, 0, 4096) };
        public List<NativeBattleEffect298Operation> Operations { get; } = [];
        public List<string> Log { get; } = [];
        public bool Busy { get; init; } = true;

        public NativeBattleEffect298Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => Model, (frame, _, _, _) => new(frame, PointView(new(1, 2, 3))), _ => throw new NotSupportedException("The selected upper callback has no contact packet consumer."), (_, _) => throw new NotSupportedException("The neutral upper fixture has no body service."), (frame, operation) =>
        {
            Operations.Add(operation);
            Log.Add(operation switch
            {
                NativeBattleEffect298Music47E3C0 => "music",
                NativeBattleEffect298InitializeIo534110 => "io-init",
                NativeBattleEffect298AdvanceIo534210 => "io-advance",
                NativeBattleEffect298DrawOffsetBody672330 => "body",
                NativeBattleEffect298ModelFields => "fields",
                _ => operation.GetType().Name
            });
            if (operation is NativeBattleEffect298ModelFields fields)
            {
                Model = Model with { Flags0 = (ushort)((Model.Flags0 & ~fields.ClearFlags0) | fields.SetFlags0) };
                if (fields.PositionZ is short z)
                {
                    Model = Model with { Transform = Model.Transform! with { Position28 = Model.Transform!.Position28 with { Z = z } } };
                }
            }
            return frame;
        }, frame =>
        {
            Log.Add("vibration");
            return new(frame, 0x12345678);
        }, (frame, index) =>
        {
            Log.Add("io-poll-" + index);
            return new(frame, Busy);
        });
    }

    private static NativeBattleEffect298Resources Resources() => NativeBattleEffect298StateTests.Resources();
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
}
