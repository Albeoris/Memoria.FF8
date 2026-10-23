using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Controller;
using static Memoria.FF8.Runtime.NativeBattlePoisonGas171Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoisonGas171ControllerTests
{
    [Theory]
    [InlineData((ushort)0x200, (ushort)0)]
    [InlineData((ushort)0x204, (ushort)4)]
    public void Pause_precedes_model_bone_and_births_and_old_one_captures_only_current_flag_four(ushort flags, ushort saved)
    {
        NativeBattlePoisonGas171Frame frame = Frame(1);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(22, 0xABCD));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(14, 0xBEEF).MarkPayloadUnknown(28, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        List<NativeBattlePoisonGas171Operation> operations = [];
        List<(int Actor, int Selector, int Distance)> bones = [];
        NativeBattlePoisonGas171Inputs inputs = Inputs(operations, bones) with { ReadModel = (_, actor) => new(actor, flags) };
        var paused = AdvanceMain(frame, ChildBank, Resources(), inputs with { GraphicsFlags = 0x201, ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException(), ReadModel = (_, _) => throw new InvalidOperationException() });
        Assert.Equal(frame, paused.Frame);
        Assert.Empty(bones);
        Assert.Empty(operations);
        NativeBattlePoisonGas171Frame result = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        Assert.Equal(new[] { (3, 0xF1, 0) }, bones);
        Assert.Equal(new uint[] { 0x5DB620, 0x5DBED0, 0x5DBED0, 0x5DC270, 0x5DC270 }, result.Effect.Pools[1].Slots.Skip(1).Take(5).Select(task => task.Callback));
        NativeBattleModulePoolTask body = Read(result, ChildBank + 36);
        Assert.Equal(saved, body.ReadWord(26));
        Assert.Equal((ushort)0xBEEF, body.ReadWord(14));
        Assert.Throws<NotSupportedException>(() => body.ReadWord(28));
        NativeBattleModulePoolTask[] smoke = result.Effect.Pools[1].Slots.Skip(2).Take(4).ToArray();
        Assert.Equal(new ushort[] { 1, 2, 1, 2 }, smoke.Select(task => task.ReadWord(14)));
        Assert.Equal(new ushort[] { 0, 2, 4, 6 }, smoke.Select(task => task.ReadWord(24)));
        Assert.All(smoke, task => Assert.Equal(new byte[] { 103, 0, 203, 0, 47, 1, 205, 171 }, TaskView(task, 16, 8).Read(0, 8)));
        Assert.Equal((ushort)0xABCD, Read(result, ChildBank).ReadWord(22));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(new NativeBattlePoisonGas171Stage5713E0(128, 0), operations[0]);
        Assert.Equal(new NativeBattlePoisonGas171Sound501330(0x61ACC, 0, 128), operations[1]);
    }

    [Fact]
    public void Old_eight_pair_consumes_two_draws_and_writes_only_native_mesh_fields()
    {
        NativeBattlePoisonGas171Frame frame = Frame(8);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        for (int slot = 1; slot <= 2; slot++)
        {
            pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(14, 0xABCD).WriteWord(18, 0xBEEF).WriteWord(20, 0xCAFE).MarkPayloadUnknown(30, 2)) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeBattlePoisonGas171Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs([], [])).Frame;
        Assert.Equal(AfterDraws(frame.Random, 2), result.Random);
        Assert.All(result.Effect.Pools[1].Slots.Skip(1).Take(2), task =>
        {
            Assert.Equal(0x5DC5B0u, task.Callback);
            Assert.Equal((ushort)0, task.ReadWord(12));
            Assert.Equal((short)-400, Signed(task, 16));
            Assert.Equal((ushort)0xB00, task.ReadWord(28));
            Assert.Equal((ushort)0xABCD, task.ReadWord(14));
            Assert.Equal((ushort)0xBEEF, task.ReadWord(18));
            Assert.Equal((ushort)0xCAFE, task.ReadWord(20));
            Assert.Throws<NotSupportedException>(() => task.ReadWord(30));
        });
        Assert.Equal((ushort)10, Read(result, ChildBank + 36).ReadWord(22));
        Assert.Equal((ushort)30, Read(result, ChildBank + 36).ReadWord(26));
        Assert.Equal((ushort)6, Read(result, ChildBank + 72).ReadWord(22));
        Assert.Equal((ushort)15, Read(result, ChildBank + 72).ReadWord(26));
    }

    [Fact]
    public void Old_twenty_seven_uses_actual_captured_contact_actors_low_word_shift_and_six_byte_bone_writes()
    {
        NativeBattlePoisonGas171Frame frame = Frame(27);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(22, 0xBEEF)).SetItem(2, pool.Slots[2].MarkPayloadUnknown(22, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        List<(int Actor, int Selector, int Distance)> bones = [];
        NativeBattleActionPacketsState packets = Packets(35).ActivateHeader(0);
        NativeBattlePoisonGas171Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs([], bones) with { Packets = packets }).Frame;
        Assert.Equal(35, bones.Count);
        Assert.Equal(Enumerable.Range(0, 35).Select(ordinal => ((ordinal * 3 + 2) % 7, 0xF1, 0)), bones);
        Assert.Equal(36, result.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(frame.Random, result.Random);
        NativeBattleModulePoolTask[] children = result.Effect.Pools[1].Slots.Skip(1).Take(35).ToArray();
        Assert.All(children, task => Assert.Equal(0x5DC700u, task.Callback));
        Assert.Equal((ushort)4, children[0].ReadWord(14));
        Assert.Equal((ushort)0x8000, children[13].ReadWord(14));
        Assert.Equal((ushort)0, children[14].ReadWord(14));
        Assert.Equal((ushort)0, children[31].ReadWord(14));
        Assert.Equal((ushort)4, children[32].ReadWord(14));
        Assert.Equal((ushort)8, children[33].ReadWord(14));
        Assert.Equal((ushort)16, children[34].ReadWord(14));
        Assert.Equal((ushort)0xBEEF, children[0].ReadWord(22));
        Assert.Throws<NotSupportedException>(() => children[1].ReadWord(22));
        for (int ordinal = 0; ordinal < 35; ordinal++)
        {
            int actor = (ordinal * 3 + 2) % 7;
            Assert.Equal((ushort)(100 + actor), children[ordinal].ReadWord(16));
            Assert.Equal((ushort)(200 + actor), children[ordinal].ReadWord(18));
            Assert.Equal((ushort)(300 + actor), children[ordinal].ReadWord(20));
        }
    }

    [Fact]
    public void Empty_recipient_range_does_not_read_unknown_pointer_and_unobserved_nonempty_packet_fails()
    {
        NativeBattlePoisonGas171Frame frame = Frame(27);
        NativeBattleActionPacketsState packets = Packets(0);
        packets = packets with { Headers = packets.Headers.SetItem(1, packets.Headers[1] with { FirstContactIndex = null }) };
        List<(int Actor, int Selector, int Distance)> bones = [];
        NativeBattlePoisonGas171Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs([], bones) with { Packets = packets }).Frame;
        Assert.Empty(bones);
        Assert.Single(result.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
        NativeBattleActionPacketsState missing = Packets(1);
        missing = missing with { Contacts = missing.Contacts.SetItem(1, null) };
        Assert.Throws<NotSupportedException>(() => AdvanceMain(frame, ChildBank, Resources(), Inputs([], []) with { Packets = missing }));
    }

    [Fact]
    public void Old_thirty_applies_the_captured_counted_header_independent_of_active_header()
    {
        NativeBattlePoisonGas171Frame frame = Frame(30);
        List<NativeBattlePoisonGas171Operation> operations = [];
        NativeBattleActionPacketsState packets = Packets(3).ActivateHeader(0);
        NativeBattlePoisonGas171Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations, []) with { Packets = packets }).Frame;
        Assert.Equal(new NativeBattlePoisonGas171Contacts506BA0(1), Assert.Single(operations));
        Assert.Equal(3, packets.ReadHeaderContacts506BA0(1).Length);
        Assert.Equal((ushort)31, Read(result, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0, 0, 1, 0)]
    [InlineData(16, 2048, 1, 0)]
    [InlineData(17, 0, 0, 0)]
    [InlineData(41, 0, 0, 0)]
    [InlineData(42, 2048, 1, 0)]
    [InlineData(50, 0, 2, 2)]
    public void Stage_envelope_uses_distinct_shifts_thresholds_and_terminal_extra_zero(ushort age, int value, int stageCount, int code)
    {
        List<NativeBattlePoisonGas171Operation> operations = [];
        NativeBattlePoisonGas171Frame frame = Frame(age);
        var step = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations, []));
        Assert.Equal(code, step.Result);
        Assert.Equal(stageCount, operations.Count);
        Assert.All(operations, operation => Assert.Equal(new NativeBattlePoisonGas171Stage5713E0(value, 0), operation));
        Assert.Equal((ushort)(age + 1), Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Json_continuation_preserves_partial_mesh_unknownness_and_crt_next_state()
    {
        NativeBattlePoisonGas171Frame frame = Frame(8);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].MarkPayloadUnknown(18, 4)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeBattlePoisonGas171Frame restored = JsonSerializer.Deserialize<NativeBattlePoisonGas171Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattlePoisonGas171Frame expected = AdvanceMain(frame, ChildBank, Resources(), Inputs([], [])).Frame;
        NativeBattlePoisonGas171Frame actual = AdvanceMain(restored, ChildBank, Resources(), Inputs([], [])).Frame;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<NotSupportedException>(() => Read(actual, ChildBank + 36).ReadWord(18));
    }

    [Fact]
    public void Root_keeps_common_owner_and_parity_and_detaches_only_after_actual_child_drain()
    {
        NativeBattlePoisonGas171Frame frame = Frame(50);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x55 });
        frame = frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State.WriteWord(ParticleABase + 6, 0xCAFE).WriteWord(ParticleBBase + 6, 0xBEEF) };
        NativeBattlePoisonGas171Inputs paused = Inputs([], []) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(ArenaAddress, frame.State.ReadDword(CursorGlobal));
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(ArenaAddress + 0xC000u, frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)50, Read(frame, ChildBank).ReadWord(12));
        NativeBattleEffectTaskPool empty = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, empty) } };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Same(arena, frame.Memory.Arena);
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(ParticleABase + 6));
        Assert.Equal((ushort)0xBEEF, frame.State.ReadWord(ParticleBBase + 6));
    }

    private static NativeBattlePoisonGas171Frame Frame(ushort age)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age)) };
        NativeBattleOriginalEffectState effect = new(171, 170, new(171, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), RootDescriptor) { Pools = [roots, children] };
        NativeBattlePoisonGas171State state = NativeBattlePoisonGas171State.FromColdProcess().WriteDword(CasterGlobal, 3).WriteDword(WorkingGlobal, ArenaAddress) with { RootBankStorage = null, ChildBankStorage = null };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(55), state);
    }

    private static NativeBattlePoisonGas171Resources Resources() => new(null!, null!, null!, [], null!, 0x61ACC);

    private static NativeBattlePoisonGas171Inputs Inputs(List<NativeBattlePoisonGas171Operation> operations, List<(int Actor, int Selector, int Distance)> bones) => new(Packets(3), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(actor, 0x204), (_, _) => throw new InvalidOperationException(), (current, actor, selector, distance) =>
    {
        Assert.Equal(0xF1, selector);
        Assert.Equal(0, distance);
        bones.Add((actor, selector, distance));
        return new(current, PointView(new((short)(100 + actor), (short)(200 + actor), (short)(300 + actor)), 0x7777));
    }, (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleActionPacketsState Packets(byte count)
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        (state, _) = state.Prepare(new(3, 8, 14, -1, 32, 171, null));
        (state, _) = state.AppendContact(Contact(6));
        (state, _) = state.Publish(1);
        (state, _) = state.Prepare(new(3, 8, 14, -1, 32, 171, null));
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            (state, _) = state.AppendContact(Contact(checked((byte)((ordinal * 3 + 2) % 7))));
        }
        (state, _) = state.Publish(count);
        return state;
    }

    private static NativeBattleContactPacket Contact(byte actor) => new(new(actor, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0));

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
