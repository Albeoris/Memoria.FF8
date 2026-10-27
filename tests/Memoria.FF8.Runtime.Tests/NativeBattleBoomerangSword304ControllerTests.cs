using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Controller;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBoomerangSword304ControllerTests
{
    [Fact]
    public void Entry_keeps_common_inactive_and_owns_only_reset_root()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(ModelAddress(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(0, 1)[0]);
    }

    [Theory]
    [InlineData(0u, 0x116F0u, 1u)]
    [InlineData(1u, 0x16F0u, 0u)]
    [InlineData(0x80000000u, 0x16F0u, 0u)]
    public void Full_DWORD_parity_and_no_pause(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Sorted_target_pointers_leave_contacts_unchanged_and_mean_wraps_before_division()
    {
        var packets = Packets(0, 1, 2);
        var inputs = Inputs([], packets) with { ReadAnchor571400 = (current, model) =>
        {
            int actor = Actor(model);
            short x = actor == 0 ? (short)1200 : actor == 1 ? (short)-2000 : (short)100;
            return new(current, PointView(new(x, 30000, 30000), (short)(700 + actor)));
        } };
        var frame = Initialize(Inactive(), Resources(), inputs);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        var task = Read(actual, Pools[1].Bank);
        Assert.Equal(new[] { ModelAddress(1), ModelAddress(0), ModelAddress(2) }, new[] { task.ReadDword(32), task.ReadDword(36), task.ReadDword(40) });
        Assert.Equal(new byte[] { 0, 1, 2 }, Enumerable.Range(0, 3).Select(index => packets.ReadContact(index).Primary.ActorIndex));
        Assert.Equal(unchecked((ushort)-2500), task.ReadWord(18));
        Assert.Equal((ushort)1700, task.ReadWord(20));
        Assert.Equal((ushort)(unchecked((short)90000) / 3), task.ReadWord(22));
        Assert.Equal((ushort)(unchecked((short)90000) / 3 - 1000), task.ReadWord(24));
        Assert.Equal((ushort)701, task.ReadWord(50));
        Assert.Equal((ushort)700, task.ReadWord(58));
        Assert.Equal((ushort)702, task.ReadWord(66));
    }

    [Fact]
    public void Empty_count_keeps_native_divide_fault_instead_of_a_synthetic_centroid()
    {
        var inputs = Inputs([], Packets());
        var frame = Initialize(Inactive(), Resources(), inputs);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        Assert.Throws<DivideByZeroException>(() => AdvanceTask(frame, RootBank, Resources(), inputs));
    }

    [Fact]
    public void Ages_four_and_five_publish_caster_bone23_without_overwriting_retained_W()
    {
        var frame = Active();
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).WriteWord(12, 4));
        List<int> selectors = [];
        var inputs = Inputs([]) with { ReadBone502170 = (current, model, selector, displacement) =>
        {
            Assert.Equal(ModelAddress(3), model);
            Assert.Equal(0, displacement);
            selectors.Add(selector);
            return new(current with { Random = current.Random.Draw(out _) }, PointView(new(321, -654, 987)));
        } };
        var four = AdvanceTask(frame, Pools[1].Bank, Resources(), inputs).Frame;
        Assert.Equal(PointView(new(321, -654, 987), 0).Read(0, 8), TaskView(Read(four, Pools[1].Bank), 148, 8).Read(0, 8));
        Assert.Equal(TaskView(Read(four, Pools[1].Bank), 148, 8).Read(0, 8), TaskView(Read(four, Pools[1].Bank), 156, 8).Read(0, 8));
        var five = AdvanceTask(four, Pools[1].Bank, Resources(), inputs).Frame;
        Assert.Equal(new[] { 23, 23 }, selectors);
        Assert.Equal(PointView(new(321, -654, 987), 0).Read(0, 8), TaskView(Read(five, Pools[1].Bank), 140, 8).Read(0, 8));
        Assert.Equal(TaskView(Read(five, Pools[1].Bank), 68, 8).Read(0, 8), TaskView(Read(five, Pools[1].Bank), 76, 8).Read(0, 8));
        Assert.Equal((ushort)777, Read(five, Pools[1].Bank).ReadWord(98));
    }

    [Fact]
    public void Curve_uses_stateful_three_weighted_blends_and_retains_unknown_output_W()
    {
        var frame = Active();
        for (int index = 0; index < 4; index++)
        {
            frame = WriteBytes(frame, Pools[1].Bank + 68 + (uint)(index * 8), PointView(new((short)(index * 100), 0, 0), (short)(1000 + index)), 8);
        }
        var actual = SamplePath304(frame, Pools[1].Bank, 0, 2048, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(PointView(new(150, 0, 0)).Read(0, 6), actual.Point8.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => actual.Point8.Read(6, 2));
        Assert.Equal(2048u, actual.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(150, actual.Frame.Memory.ScriptedColorRegisters.Ir[0]);
        Assert.Equal(frame.Memory.GeometryRegisters, actual.Frame.Memory.GeometryRegisters);
    }

    [Fact]
    public void Zero_tangents_skip_intermediate_blends_and_preserve_the_native_FIFO_history()
    {
        var frame = Active();
        NativeBattleVector first = new(100, 50, 25);
        NativeBattleVector second = new(200, 100, 50);
        for (int index = 0; index < 4; index++)
        {
            frame = WriteBytes(frame, Pools[1].Bank + 68 + (uint)(index * 8), PointView(index < 2 ? first : second, (short)(900 + index)), 8);
        }
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters! with { Rgb = [0x11223344u, 0x55667788u, 0x99AABBCCu], Material = 0xAB000000 };
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = colors } };
        NativeBattleEffectMemory beforeFinalBlend = frame.Memory with { ScriptedColorRegisters = colors with { Mac = [0, 0, 0], Ir = [0, 0, 0], Flags = 0 } };
        NativeBattleWeightedPointsStep expected = NativeBattleWeightedPoints56CB90.Blend(beforeFinalBlend, first, second, 2048, 2048, NativeBattleGteColorInterpolation.CodeConstants45F270);
        var actual = SamplePath304(frame, Pools[1].Bank, 0, 2048, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(PointView(expected.Position).Read(0, 6), actual.Point8.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => actual.Point8.Read(6, 2));
        Assert.Equal(0x99AABBCCu, actual.Frame.Memory.ScriptedColorRegisters!.Rgb[0]);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Memory.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(actual.Frame.Memory.ScriptedColorRegisters));
        Assert.Equal(frame.Memory.GeometryRegisters, actual.Frame.Memory.GeometryRegisters);
    }

    [Fact]
    public void Contact_matcher_executes_every_duplicate_matching_physical_slot()
    {
        var frame = Active();
        List<NativeBattleBoomerangSword304Operation> operations = [];
        var packets = Packets(0, 1, 0);
        _ = ApplyModelContacts66A2C0(frame, ModelAddress(0), Inputs(operations, packets));
        Assert.Equal(new[] { 0, 2 }, operations.OfType<NativeBattleBoomerangSword304Contact506690>().Select(operation => operation.ContactIndex));
    }

    [Fact]
    public void Contact_matcher_reloads_count_after_consumer()
    {
        var frame = Active();
        var packets = Packets(0, 0, 0);
        List<int> contacts = [];
        var inputs = Inputs([], packets) with { ReadPackets = _ => packets, Execute = (current, operation) =>
        {
            if (operation is NativeBattleBoomerangSword304Contact506690 contact)
            {
                contacts.Add(contact.ContactIndex);
                var header = packets.Headers[0];
                packets = packets with { Headers = packets.Headers.SetItem(0, header with { Payload = header.Payload.SetItem(16, 1) }) };
            }
            return current;
        } };
        _ = ApplyModelContacts66A2C0(frame, ModelAddress(0), inputs);
        Assert.Equal(new[] { 0 }, contacts);
    }

    [Fact]
    public void Contact_matcher_reloads_physical_base_after_consumer()
    {
        var frame = Active();
        var packets = Packets(0, 0, 1);
        List<int> contacts = [];
        var inputs = Inputs([], packets) with { ReadPackets = _ => packets, Execute = (current, operation) =>
        {
            if (operation is NativeBattleBoomerangSword304Contact506690 contact)
            {
                contacts.Add(contact.ContactIndex);
                var header = packets.Headers[0];
                byte[] bytes = header.Payload.ToArray();
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x1D28344 + 24);
                bytes[16] = 2;
                packets = packets with { Headers = packets.Headers.SetItem(0, header with { Payload = bytes.ToImmutableArray(), FirstContactIndex = 1 }) };
            }
            return current;
        } };
        _ = ApplyModelContacts66A2C0(frame, ModelAddress(0), inputs);
        Assert.Equal(new[] { 0 }, contacts);
        packets.Validate();
    }

    [Fact]
    public void Crossing_contact_survives_failed_programme_and_all_particle_appends()
    {
        var frame = Crossing();
        for (int index = 0; index < 3; index++)
        {
            (frame, _) = Append(frame, Pools[3].Descriptor, 0x66A3F0);
        }
        for (int index = 0; index < 64; index++)
        {
            (frame, _) = Append(frame, Pools[4].Descriptor, 0x66A330);
        }
        List<NativeBattleBoomerangSword304Operation> operations = [];
        var inputs = Inputs(operations) with { ReadAnchor571400 = (_, _) => throw new InvalidOperationException("Failed programme must not capture an anchor.") };
        var actual = AdvanceTask(frame, Pools[1].Bank, Resources(), inputs).Frame;
        Assert.Equal(0, Assert.Single(operations.OfType<NativeBattleBoomerangSword304Contact506690>()).ContactIndex);
        Assert.Equal((ushort)1, Read(actual, Pools[1].Bank).ReadWord(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Successful_crossing_attempts_twenty_one_particles_and_keeps_unwritten_payload_unknown()
    {
        var frame = Crossing();
        frame = Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).MarkPayloadUnknown(26, 2).MarkPayloadUnknown(34, 2));
        List<NativeBattleBoomerangSword304Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs(operations)).Frame;
        var random = frame.Random;
        for (int draw = 0; draw < 42; draw++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
        Assert.Equal(21, actual.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)1, Read(actual, Pools[1].Bank).ReadWord(16));
        Assert.Throws<NotSupportedException>(() => Read(actual, Pools[4].Bank).ReadWord(26));
        Assert.Throws<NotSupportedException>(() => Read(actual, Pools[4].Bank).ReadWord(34));
        Assert.Equal((ushort)0, Read(actual, Pools[4].Bank).ReadWord(18));
        Assert.Single(operations.OfType<NativeBattleBoomerangSword304Contact506690>());
    }

    [Theory]
    [InlineData((ushort)57, 0, (ushort)58)]
    [InlineData((ushort)58, 2, (ushort)59)]
    public void Main_retires_at_new_fifty_nine(ushort age, int result, ushort finalAge)
    {
        var frame = MainAt(age);
        var actual = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs([]));
        Assert.Equal(result, actual.Result);
        Assert.Equal(finalAge, Read(actual.Frame, Pools[1].Bank).ReadWord(12));
    }

    [Fact]
    public void Root_retires_with_live_particle_bank_and_transfers_actual_flags_to_common()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        var pools = frame.Effect.Pools.ToBuilder();
        foreach (var profile in Pools.Skip(1))
        {
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity));
        }
        frame = frame with { Effect = frame.Effect with { Pools = pools.ToImmutable() }, Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0, 0x1558) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1));
        (frame, uint particle) = Append(frame, Pools[4].Descriptor, 0x66A330);
        frame = ClearDwords701200(frame, particle + 12, 6);
        var actual = Advance(frame, Resources(), Inputs([]));
        Assert.Empty(actual.Effect.Pools);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena!.Read(0xC58, 2)) & 1);
        Assert.NotNull(actual.State.RootBankStorage);
        var cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleBoomerangSword304Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(actual.Memory.Arena!.Read(0xC58, 36), cold.Memory.Arena!.Read(0xC58, 36));
        Assert.Single(Initialize(cold, Resources(), Inputs([])).Effect.Pools);
    }

    private static NativeBattleBoomerangSword304Frame Crossing()
    {
        var frame = MainAt(25);
        for (int index = 0; index < 7; index++)
        {
            frame = WriteBytes(frame, Pools[1].Bank + 68 + (uint)(index * 8), PointView(new(-100, 0, 0), 0), 8);
        }
        return frame;
    }

    private static NativeBattleBoomerangSword304Frame MainAt(ushort age)
    {
        var frame = Active();
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).WriteWord(12, 4));
        frame = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs([])).Frame;
        frame = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs([])).Frame;
        return Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).WriteWord(12, age));
    }

    private static NativeBattleBoomerangSword304Frame Active()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        return AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
    }

    private static NativeBattleBoomerangSword304Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0, new byte[] { 0xA5 }), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(304, 303, new(304, Entry, Initializer), new(3, 0, 0x7F, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleBoomerangSword304State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleBoomerangSword304Inputs Inputs(List<NativeBattleBoomerangSword304Operation> operations, NativeBattleActionPacketsState? packets = null)
    {
        packets ??= Packets(0);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, model) => Model(model), (current, _, _, _) => new(current, PointView(new(321, -654, 987))), (current, model) => new(current, PointView(new(Model(model).Transform!.Position28.X, -200, 1000), 777)), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleActionPacketsState Packets(params byte[] actors)
    {
        var packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[32 * 20], new byte[49 * 24]);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 159, 304, null));
        foreach (byte actor in actors)
        {
            (packets, _) = packets.AppendContact(new(new(actor, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(checked((byte)actors.Length));
        return packets;
    }

    private static NativeBattleBoomerangSword304Resources Resources() => NativeBattleBoomerangSword304StateTests.Resources();
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
    private static int Actor(uint model) => checked((int)((model - 0x1D972C0) / 156));
    private static NativeBattleGraphicsModel Model(uint model)
    {
        int actor = Actor(model);
        short x = actor == 0 ? (short)100 : actor == 1 ? (short)300 : actor == 2 ? (short)-200 : (short)500;
        return new(actor, 2) { Rotation14 = 0, MinimumY146 = -100, Transform = new(new(0, 0, 0), new(x, 0, 1000), 0, 777, 4096) };
    }
}
