using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Controller;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional181ControllerTests
{
    [Fact]
    public void Director_pause_precedes_bones_and_initial_bone_writes_only_six_bytes()
    {
        NativeBattleAdditional181Frame frame = Frame(0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(22, 0xABCD));
        NativeBattleAdditional181Inputs inputs = Inputs([]);
        var paused = AdvanceMain(frame, ChildBank, Resources(), inputs with { GraphicsFlags = 0x201, ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException() });
        Assert.Equal(frame, paused.Frame);
        Assert.Equal(0, paused.Result);
        var active = AdvanceMain(frame, ChildBank, Resources(), inputs);
        NativeBattleModulePoolTask main = Read(active.Frame, ChildBank);
        Assert.Equal(new byte[] { 100, 0, 132, 3, 44, 1 }, TaskView(main, 16, 6).Read(0, 6));
        Assert.Equal((ushort)0xABCD, main.ReadWord(22));
        Assert.Equal((ushort)5, main.ReadWord(14));
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal(frame.Random, active.Frame.Random);
    }

    [Fact]
    public void Old_four_and_five_spawn_five_then_four_with_fresh_XZ_initial_Y_and_exact_twenty_seven_draws()
    {
        NativeBattleAdditional181Frame frame = Frame(4);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(22, 0xBEEF).MarkPayloadUnknown(32, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeBattleAdditional181Frame first = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal((ushort)4, Read(first, ChildBank).ReadWord(14));
        Assert.Equal(AfterDraws(frame.Random, 15), first.Random);
        Assert.Equal(6, first.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleAdditional181Frame second = AdvanceMain(first, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal((ushort)3, Read(second, ChildBank).ReadWord(14));
        Assert.Equal(AfterDraws(frame.Random, 27), second.Random);
        Assert.Equal(10, second.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)0xBEEF, Read(second, ChildBank + 36).ReadWord(22));
        Assert.Throws<NotSupportedException>(() => Read(second, ChildBank + 36).ReadWord(32));
        Assert.All(second.Effect.Pools[1].Slots.Skip(1).Take(9), task =>
        {
            Assert.Equal(0x5CC720u, task.Callback);
            Assert.Equal((ushort)100, task.ReadWord(16));
            Assert.InRange(task.ReadWord(18), (ushort)195, (ushort)204);
            Assert.Equal((ushort)300, task.ReadWord(20));
            Assert.Equal((short)0, Signed(task, 24));
            Assert.InRange(Signed(task, 26), (short)25, (short)99);
            Assert.InRange(task.ReadWord(28), (ushort)0x800, (ushort)0x8FF);
        });
        Assert.Equal((ushort)200, Read(second, ChildBank).ReadWord(18));
    }

    [Fact]
    public void Nonpositive_old_four_count_still_queries_geometry_and_decrements_once_without_births_or_random()
    {
        NativeBattleAdditional181Frame frame = Frame(4);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 0));
        int queries = 0;
        NativeBattleAdditional181Inputs inputs = Inputs([]);
        inputs = inputs with { ReadBone502170 = (current, actor, selector, distance) =>
        {
            queries++;
            return Inputs([]).ReadBone502170(current, actor, selector, distance);
        } };
        NativeBattleAdditional181Frame result = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        Assert.Equal(1, queries);
        Assert.Equal((short)-1, Signed(Read(result, ChildBank), 14));
        Assert.Equal(frame.Random, result.Random);
        Assert.Single(result.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(14, 0, 0)]
    [InlineData(38, 0, 0)]
    [InlineData(50, 0, 2)]
    public void Director_uses_original_sound_flash_first_contact_and_terminal_ages(ushort age, int births, int code)
    {
        NativeBattleAdditional181Frame frame = Frame(age);
        List<NativeBattleAdditional181Operation> operations = [];
        var result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations));
        Assert.Equal(code, result.Result);
        Assert.Equal(births + 1, result.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal((ushort)(age + 1), Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
        if (age == 1)
        {
            Assert.Equal(0x5CB9B0u, Read(result.Frame, ChildBank + 36).Callback);
            Assert.Equal(new NativeBattleAdditional181Sound501330(0x4949F, 0, 128), Assert.Single(operations));
        }
        if (age == 14)
        {
            Assert.Equal(new NativeBattleAdditional181Flash5712C0(0, 1, 4, 255), Assert.Single(operations));
        }
        if (age == 38)
        {
            Assert.Equal(new NativeBattleAdditional181Contact506690(2), Assert.Single(operations));
        }
    }

    [Fact]
    public void Contact_resolves_first_physical_packet_independently_of_header_count_and_secondary_half()
    {
        NativeBattleAdditional181Frame frame = Frame(38);
        NativeBattleActionPacketsState packets = Packets();
        Assert.Equal(3, packets.ReadHeaderContacts506BA0(1).Length);
        List<NativeBattleAdditional181Operation> operations = [];
        AdvanceMain(frame, ChildBank, Resources(), Inputs(operations) with { Packets = packets });
        NativeBattleAdditional181Contact506690 contact = Assert.IsType<NativeBattleAdditional181Contact506690>(Assert.Single(operations));
        Assert.Equal(2, contact.ContactIndex);
        Assert.Equal((byte)3, packets.ReadContact(contact.ContactIndex).Primary.ActorIndex);
        Assert.Equal((byte)0, packets.ReadContact(contact.ContactIndex).Secondary.ActorIndex);
        NativeBattleActionHeaderSlot header = packets.Headers[1] with { FirstContactIndex = null };
        Assert.Throws<NotSupportedException>(() => AdvanceMain(frame, ChildBank, Resources(), Inputs([]) with { Packets = packets with { Headers = packets.Headers.SetItem(1, header) } }));
    }

    [Fact]
    public void Anchor_at_fifteen_replaces_packed_W_and_births_keep_unwritten_fields_then_ring_order()
    {
        NativeBattleAdditional181Frame frame = Frame(15);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(14, 0xBEEF).MarkPayloadUnknown(32, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeBattleAdditional181Frame first = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(AfterDraws(frame.Random, 1), first.Random);
        Assert.Equal(new uint[] { 0x5CC160, 0x5CC2A0 }, first.Effect.Pools[1].Slots.Skip(1).Take(2).Select(task => task.Callback));
        Assert.Equal((ushort)0x1234, Read(first, ChildBank).ReadWord(22));
        NativeBattleModulePoolTask mesh = Read(first, ChildBank + 36);
        Assert.Equal((ushort)0xBEEF, mesh.ReadWord(14));
        Assert.Throws<NotSupportedException>(() => mesh.ReadWord(32));
        Assert.Equal((ushort)0x580, mesh.ReadWord(28));
        Assert.Equal(mesh.ReadWord(28), mesh.ReadWord(30));
        NativeBattleAdditional181Frame second = AdvanceMain(first, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(AfterDraws(frame.Random, 5), second.Random);
        Assert.Equal(new uint[] { 0x5CC000, 0x5CC000, 0x5CC560 }, second.Effect.Pools[1].Slots.Skip(3).Take(3).Select(task => task.Callback));
        NativeBattleModulePoolTask ringA = Read(second, ChildBank + 3 * 36);
        NativeBattleModulePoolTask ringB = Read(second, ChildBank + 4 * 36);
        Assert.InRange(Signed(ringA, 26), (short)60, (short)169);
        Assert.InRange(Signed(ringB, 26), (short)-209, (short)-80);
        Assert.Equal((ushort)460, ringA.ReadWord(28));
        Assert.Equal(ringA.ReadWord(28), ringA.ReadWord(30));
        Assert.Equal((ushort)870, ringB.ReadWord(28));
        Assert.Equal(ringB.ReadWord(28), ringB.ReadWord(30));
        Assert.All(second.Effect.Pools[1].Slots.Skip(1).Take(5), task => Assert.Equal(new byte[] { 144, 1, 244, 1, 88, 2, 52, 18 }, TaskView(task, 16, 8).Read(0, 8)));
    }

    [Fact]
    public void Json_continues_birth_random_and_retained_payload_knownness_identically()
    {
        NativeBattleAdditional181Frame frame = Frame(4);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].MarkPayloadUnknown(22, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeBattleAdditional181Frame restored = JsonSerializer.Deserialize<NativeBattleAdditional181Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleAdditional181Frame expected = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        NativeBattleAdditional181Frame actual = AdvanceMain(restored, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<NotSupportedException>(() => Read(actual, ChildBank + 36).ReadWord(22));
    }

    [Fact]
    public void Root_advances_common_parity_while_paused_and_detaches_only_actual_drained_banks()
    {
        NativeBattleAdditional181Frame frame = Frame(50);
        NativeBattleAdditional181Inputs paused = Inputs([]) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(ArenaAddress, frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)50, Read(frame, ChildBank).ReadWord(12));
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(ArenaAddress + 0xC000u, frame.State.ReadDword(CursorGlobal));
        NativeBattleEffectTaskPool empty = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, empty) }, State = frame.State.WriteWord(ParticleBase + 6, 0xCAFE) };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(ParticleBase + 6));
    }

    private static NativeBattleAdditional181Frame Frame(ushort age)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age).WriteWord(14, 5).WriteWord(16, 10).WriteWord(18, 200).WriteWord(20, 30)) };
        NativeBattleOriginalEffectState effect = new(181, 180, new(181, Entry, Initializer), new(0, 0, 8, 1, 0, 0), RootDescriptor) { Pools = [roots, children] };
        NativeBattleAdditional181State state = NativeBattleAdditional181State.FromColdProcess().WriteDword(CasterGlobal, 0).WriteDword(WorkingGlobal, ArenaAddress) with { RootBankStorage = null, ChildBankStorage = null };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(55), state);
    }

    private static NativeBattleAdditional181Resources Resources() => new(null!, null!, null!, null!, null!, null!, 0x4949F);

    private static NativeBattleAdditional181Inputs Inputs(List<NativeBattleAdditional181Operation> operations) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => new(0, 3) { Transform = new(new(0, 0, 0), new(100, 0, 1300), 0, 0, 4096) }, (current, actor, selector, distance) =>
    {
        Assert.Equal(0, actor);
        Assert.Equal(0xF0, selector);
        Assert.Equal(2560, distance);
        return new(current, PointView(new(100, 900, 300)));
    }, (current, actor) =>
    {
        Assert.Equal(0, actor);
        return new(current, PointView(new(400, 500, 600), 0x1234));
    }, (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        (state, _) = state.Prepare(new(0, 31, 24, -1, 8, 181, null));
        (state, _) = state.AppendContact(Contact(0));
        (state, _) = state.AppendContact(Contact(1));
        (state, _) = state.Publish(2);
        (state, _) = state.Prepare(new(0, 31, 24, -1, 8, 181, null));
        (state, _) = state.AppendContact(Contact(3) with { Secondary = new(0, 4, 0, 0, 0, 7, 0) });
        (state, _) = state.AppendContact(Contact(4));
        (state, _) = state.AppendContact(Contact(5));
        (state, _) = state.Publish(3);
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
