using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMorph172Controller;
using static Memoria.FF8.Runtime.NativeBattleMorph172Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMorph172ControllerTests
{
    [Fact]
    public void Entry_resets_full_task_banks_but_only_particle_flags_and_keeps_private_packet_and_vertex_owners()
    {
        NativeBattleMorph172Frame frame = Inactive();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        NativeBattleMorph172State state = frame.State.WriteDword(ParticleBase, 0xFFFFFFFF).WriteWord(ParticleBase + 6, 0xABCD).WriteDword(ParticleBase + 99 * ParticleStride, 7).WriteWord(ParticleBase + 99 * ParticleStride + 22, 0xBEEF).WriteWord(MutableVerticesA, 0xCAFE).WriteWord(MutableVerticesB, 0xC0DE).WriteDword(RadiiBase, 0xDEADBEEF).WriteDword(PacketBase, 0x12345678).WriteDword(PacketBase + PacketStep, 0x87654321) with { RootBankStorage = NativeBattleEffectArena.Clear().Write(14, new byte[] { 0xAA, 0xBB }), ChildBankStorage = NativeBattleEffectArena.Clear().Write(30, new byte[] { 0xCC, 0xDD }) };
        frame = Initialize(frame with { State = state }, Resources(), Inputs([]));
        Assert.Equal(RootDescriptor, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Null(frame.State.ChildBankStorage);
        Assert.Equal(RootCallback, Read(frame, RootBank).Callback);
        Assert.Equal(MainCallback, Read(frame, ChildBank).Callback);
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(14));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(30));
        Assert.Equal(0u, frame.State.ReadDword(ParticleBase));
        Assert.Equal(0u, frame.State.ReadDword(ParticleBase + 99 * ParticleStride));
        Assert.Equal((ushort)0xABCD, frame.State.ReadWord(ParticleBase + 6));
        Assert.Equal((ushort)0xBEEF, frame.State.ReadWord(ParticleBase + 99 * ParticleStride + 22));
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(MutableVerticesA));
        Assert.Equal((ushort)0xC0DE, frame.State.ReadWord(MutableVerticesB));
        Assert.Equal(0xDEADBEEFu, frame.State.ReadDword(RadiiBase));
        Assert.Equal(0x12345678u, frame.State.ReadDword(PacketBase));
        Assert.Equal(0x87654321u, frame.State.ReadDword(PacketBase + PacketStep));
        Assert.Same(arena, frame.Memory.Arena);
        Assert.Equal(2u, frame.State.ReadDword(FirstTargetGlobal));
        Assert.Equal(3u, frame.State.ReadDword(CasterGlobal));
        Assert.Equal(0x1D99A78u, frame.State.ReadDword(ParametersGlobal));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x30000u, frame.State.ReadDword(InitializerGlobal));
        Assert.Equal(new NativeCrtRandomState(55), frame.Random);
    }

    [Theory]
    [InlineData((ushort)0, 1u)]
    [InlineData((ushort)1, 0x200u)]
    [InlineData((ushort)8, 0x201u)]
    public void Pause_precedes_unknown_caster_anchor_birth_sound_and_age(ushort age, uint flags)
    {
        NativeBattleMorph172Frame frame = Frame(age);
        frame = frame with { State = frame.State with { Globals = frame.State.Globals.MarkUnknown(checked((int)(CasterGlobal - BaseAddress)), 4) } };
        NativeBattleMorph172Inputs inputs = Inputs([]) with { GraphicsFlags = flags, ReadAnchor571400 = (_, _) => throw new InvalidOperationException() };
        var result = AdvanceMain(frame, ChildBank, Resources(), inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal(frame, result.Frame);
    }

    [Fact]
    public void Old_zero_publishes_actual_anchor_frame_and_all_eight_point_bytes()
    {
        NativeBattleMorph172Frame frame = Frame(0);
        List<NativeBattleMorph172Operation> operations = [];
        NativeBattleMorph172Inputs inputs = Inputs(operations) with { ReadAnchor571400 = (current, actor) =>
        {
            Assert.Equal(3, actor);
            return new(current with { Memory = current.Memory with { Active = 19 } }, PointView(new(101, 202, 303), unchecked((short)0xBEEF)));
        } };
        NativeBattleMorph172Frame result = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        Assert.Equal(19, result.Memory.Active);
        Assert.Equal((ushort)0xBEEF, Read(result, ChildBank).ReadWord(22));
        Assert.Equal(new byte[] { 101, 0, 202, 0, 47, 1 }, TaskView(Read(result, ChildBank), 16, 6).Read(0, 6));
        Assert.Equal((ushort)1, Read(result, ChildBank).ReadWord(12));
        Assert.Single(result.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Empty(operations);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Old_one_preserves_partial_birth_payload_and_orders_radii_before_fresh_positioned_sound()
    {
        NativeBattleMorph172Frame frame = Frame(1);
        frame = Write(frame, ChildBank, CopyTask(Read(frame, ChildBank), 16, PointView(new(10, 20, 30), 0x1234), 8));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        for (int index = 1; index <= 3; index++)
        {
            pool = pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index].WriteWord(14, 0xABCD).WriteWord(16, 0xCAFE).MarkPayloadUnknown(30, 2)) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        List<NativeBattleMorph172Operation> operations = [];
        NativeBattleMorph172Inputs inputs = Inputs(operations) with { ReadAnchor571400 = (current, actor) =>
        {
            Assert.Equal(3, actor);
            Assert.Equal(new uint[] { MainCallback, Callback5D9F20, Callback5DA0F0, Callback5DA2C0 }, current.Effect.Pools[1].Slots.Take(4).Select(task => task.Callback));
            Assert.Equal(5u, current.State.ReadDword(RadiiBase));
            return new(current with { Memory = current.Memory with { Active = 23 } }, PointView(new(40, 50, 60), 0x5678));
        } };
        NativeBattleMorph172Frame result = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        for (int index = 1; index <= 2; index++)
        {
            NativeBattleModulePoolTask task = Read(result, ChildBank + (uint)(index * ChildStride));
            Assert.Equal((ushort)0, task.ReadWord(12));
            Assert.Equal((ushort)0xABCD, task.ReadWord(14));
            Assert.Equal((ushort)10, task.ReadWord(16));
            Assert.Equal((ushort)20, task.ReadWord(18));
            Assert.Equal((ushort)30, task.ReadWord(20));
            Assert.Equal((ushort)0x1234, task.ReadWord(22));
            Assert.Equal((ushort)0, task.ReadWord(24));
            Assert.Equal((short)-10, Signed(task, 26));
            Assert.Equal((ushort)(index == 1 ? 0xC00 : 0x1600), task.ReadWord(28));
            Assert.Throws<NotSupportedException>(() => task.ReadWord(30));
        }
        NativeBattleModulePoolTask particles = Read(result, ChildBank + 3 * ChildStride);
        Assert.Equal((ushort)0, particles.ReadWord(12));
        Assert.Equal((ushort)0xABCD, particles.ReadWord(14));
        Assert.Equal((ushort)0xCAFE, particles.ReadWord(16));
        Assert.Throws<NotSupportedException>(() => particles.ReadWord(30));
        NativeBattleMorph172Sound5013A0 sound = Assert.IsType<NativeBattleMorph172Sound5013A0>(Assert.Single(operations));
        Assert.Equal(0x61AD3u, sound.Packed);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal(new byte[] { 40, 0, 50, 0, 60, 0, 120, 86 }, sound.Point8.Read(0, 8));
        Assert.Equal(23, result.Memory.Active);
        Assert.Equal((ushort)10, Read(result, ChildBank).ReadWord(16));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Old_eight_body_copies_retained_point_and_preserves_unknown_suffix_without_anchor_or_rng()
    {
        NativeBattleMorph172Frame frame = Frame(8);
        frame = Write(frame, ChildBank, CopyTask(Read(frame, ChildBank), 16, PointView(new(7, 8, 9), 0x4321), 8));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(14, 0xCAFE).MarkPayloadUnknown(24, 12)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        List<NativeBattleMorph172Operation> operations = [];
        NativeBattleMorph172Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations) with { ReadAnchor571400 = (_, _) => throw new InvalidOperationException() }).Frame;
        NativeBattleModulePoolTask body = Read(result, ChildBank + ChildStride);
        Assert.Equal(Callback5DA520, body.Callback);
        Assert.Equal((ushort)0, body.ReadWord(12));
        Assert.Equal((ushort)0xCAFE, body.ReadWord(14));
        Assert.Equal(new byte[] { 7, 0, 8, 0, 9, 0, 33, 67 }, TaskView(body, 16, 8).Read(0, 8));
        Assert.Throws<NotSupportedException>(() => body.ReadWord(24));
        Assert.Throws<NotSupportedException>(() => body.ReadWord(34));
        Assert.Empty(operations);
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData((ushort)59, (ushort)60, 0)]
    [InlineData((ushort)60, (ushort)61, 2)]
    [InlineData((ushort)61, (ushort)62, 2)]
    [InlineData((ushort)32766, (ushort)32767, 2)]
    [InlineData((ushort)32767, (ushort)32768, 0)]
    [InlineData((ushort)65535, (ushort)0, 0)]
    public void Main_completes_on_new_signed_age_above_sixty_and_retains_native_wrap(ushort age, ushort next, int code)
    {
        NativeBattleMorph172Frame frame = Frame(age);
        List<NativeBattleMorph172Operation> operations = [];
        var result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations));
        Assert.Equal(code, result.Result);
        Assert.Equal(next, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Empty(operations);
    }

    [Fact]
    public void Root_alternates_private_banks_during_pause_then_transfers_only_actual_drained_task_storage()
    {
        NativeBattleMorph172Frame frame = Frame(60);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        frame = frame with { State = frame.State.WriteDword(PacketBase, 0x11223344).WriteDword(PacketBase + PacketStep, 0x55667788) };
        NativeBattleMorph172Inputs paused = Inputs([]) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(PacketBase, frame.State.ReadDword(CursorGlobal));
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(PacketBase + PacketStep, frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)60, Read(frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)2, Read(frame, RootBank).ReadWord(12));
        NativeBattleEffectTaskPool empty = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, empty) } };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Equal(0x11223344u, frame.State.ReadDword(PacketBase));
        Assert.Equal(0x55667788u, frame.State.ReadDword(PacketBase + PacketStep));
        Assert.Same(arena, frame.Memory.Arena);
        frame = Initialize(frame, Resources(), Inputs([]));
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(12));
        Assert.Equal(0x11223344u, frame.State.ReadDword(PacketBase));
        Assert.Equal(0x55667788u, frame.State.ReadDword(PacketBase + PacketStep));
    }

    [Fact]
    public void Json_continuation_preserves_unknown_anchor_word_and_partial_body_suffix()
    {
        NativeBattleMorph172Frame frame = Frame(8);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).MarkPayloadUnknown(22, 2));
        NativeBattleMorph172Frame restored = JsonSerializer.Deserialize<NativeBattleMorph172Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleMorph172Frame expected = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        NativeBattleMorph172Frame actual = AdvanceMain(restored, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<NotSupportedException>(() => Read(actual, ChildBank + ChildStride).ReadWord(22));
    }

    private static NativeBattleMorph172Frame Frame(ushort age)
    {
        NativeBattleMorph172Frame frame = Inactive();
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age)) };
        return frame with { Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = RootDescriptor }, State = frame.State.WriteDword(CasterGlobal, 3) with { RootBankStorage = null, ChildBankStorage = null } };
    }

    private static NativeBattleMorph172Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
        NativeBattleOriginalEffectState effect = new(172, 171, new(172, Entry, Initializer), new(3, 0, 0x7F, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(55), NativeBattleMorph172State.FromColdProcess());
    }

    private static NativeBattleMorph172Inputs Inputs(List<NativeBattleMorph172Operation> operations)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 32, 172, null));
        (packets, _) = packets.AppendContact(new(new(2, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(actor, 0x204), (current, _) => new(current, PointView(new(100, 200, 300), 0x7777)), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleMorph172Resources Resources()
    {
        byte[] mesh = new byte[0x2C40];
        BitConverter.GetBytes(8 + VertexCount * 8).CopyTo(mesh, 0);
        BitConverter.GetBytes(VertexCount).CopyTo(mesh, 4);
        BitConverter.GetBytes((short)3).CopyTo(mesh, 8);
        BitConverter.GetBytes((short)4).CopyTo(mesh, 10);
        int cursor = 8 + VertexCount * 8;
        int[] groups = [0, 0, 0, 0, 0, 0, 20, 240];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < groups.Length; group++)
        {
            BitConverter.GetBytes(groups[group]).CopyTo(mesh, cursor);
            cursor += 4 + groups[group] * strides[group];
        }
        byte[] sprite = new byte[0x160];
        BitConverter.GetBytes((ushort)13).CopyTo(sprite, 8);
        for (int index = 0; index <= 13; index++)
        {
            BitConverter.GetBytes((ushort)0x40).CopyTo(sprite, 10 + index * 2);
        }
        byte[] camera = new byte[CameraLength];
        BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
        return new(new(0, MeshAAddress, mesh.ToImmutableArray()), new(0, SpriteAAddress, 0, 0, sprite.ToImmutableArray()), Enumerable.Repeat(0u, ColorCount).ToImmutableArray(), new("FF8_EN:D61E28", camera.ToImmutableArray()), 0x61AD3);
    }
}
