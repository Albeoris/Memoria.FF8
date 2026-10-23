using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAcid170Controller;
using static Memoria.FF8.Runtime.NativeBattleAcid170Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid170ControllerTests
{
    [Fact]
    public void Entry_uses_first_contact_recipient_and_preserves_common_payload_while_resetting_full_banks()
    {
        NativeBattleAcid170Frame frame = Inactive();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(17, new byte[] { 0xAA }).Write(PacketStep + 17, new byte[] { 0xBB });
        NativeBattleAcid170State state = frame.State.WriteDword(ParticleBase, 7).WriteDword(ParticleBase + 99 * ParticleStride, 9).WriteWord(ParticleBase + 6, 0xABCD).WriteWord(ParticleBase + 99 * ParticleStride + 22, 0xBEEF).WriteWord(CurveBase, 0xCAFE).WriteWord(SelectedVerticesBase, 0xC0DE).WriteWord(Bone38PointGlobal + 6, 0x1234).WriteWord(Bone40PointGlobal + 6, 0x5678) with { RootBankStorage = NativeBattleEffectArena.Clear().Write(14, new byte[] { 0xAB, 0xCD }), ChildBankStorage = NativeBattleEffectArena.Clear().Write(30, new byte[] { 0xEF, 0x01 }) };
        NativeBattleActionPacketsState packets = Packets().ActivateHeader(0);
        packets = packets with { Headers = packets.Headers.SetItem(1, packets.Headers[1] with { UnknownByteMask = 1u << 16 }) };
        frame = Initialize(frame with { Memory = frame.Memory with { Arena = arena }, State = state }, Resources(), Inputs([], []) with { Packets = packets });
        Assert.Equal(0u, frame.State.ReadDword(FirstTargetGlobal));
        Assert.Equal(3u, frame.State.ReadDword(CasterGlobal));
        Assert.Equal(0x1D99A78u, frame.State.ReadDword(ParametersGlobal));
        Assert.Equal(ArenaAddress, frame.State.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + 0x30000u, frame.State.ReadDword(InitializerGlobal));
        Assert.Same(arena, frame.Memory.Arena);
        Assert.Equal(new byte[] { 0xAA }, frame.Memory.Arena!.Read(17, 1));
        Assert.Equal(new byte[] { 0xBB }, frame.Memory.Arena!.Read(PacketStep + 17, 1));
        Assert.Null(frame.State.RootBankStorage);
        Assert.Null(frame.State.ChildBankStorage);
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(14));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(14));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(30));
        Assert.Equal(0u, frame.State.ReadDword(ParticleBase));
        Assert.Equal(0u, frame.State.ReadDword(ParticleBase + 99 * ParticleStride));
        Assert.Equal((ushort)0xABCD, frame.State.ReadWord(ParticleBase + 6));
        Assert.Equal((ushort)0xBEEF, frame.State.ReadWord(ParticleBase + 99 * ParticleStride + 22));
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(CurveBase));
        Assert.Equal((ushort)0xC0DE, frame.State.ReadWord(SelectedVerticesBase));
        Assert.Equal((ushort)0x1234, frame.State.ReadWord(Bone38PointGlobal + 6));
        Assert.Equal((ushort)0x5678, frame.State.ReadWord(Bone40PointGlobal + 6));
        Assert.Equal(new NativeCrtRandomState(55), frame.Random);
    }

    [Theory]
    [InlineData((ushort)1, 1u)]
    [InlineData((ushort)46, 0x200u)]
    [InlineData((ushort)55, 0x201u)]
    public void Pause_precedes_unknown_caster_and_contact_reads_and_all_upper_mutation(ushort age, uint flags)
    {
        NativeBattleAcid170Frame frame = Frame(age);
        frame = frame with { State = frame.State with { Globals = frame.State.Globals.MarkUnknown(checked((int)(CasterGlobal - BaseAddress)), 4) } };
        NativeBattleAcid170Inputs inputs = Inputs([], []) with { GraphicsFlags = flags, Packets = NativeBattleActionPacketsState.Fresh(), ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException(), Execute = (_, _) => throw new InvalidOperationException() };
        var result = AdvanceMain(frame, ChildBank, Resources(), inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal(frame, result.Frame);
    }

    [Fact]
    public void Old_one_orders_partial_curve_birth_bone_forty_then_thirty_eight_and_mask_two_before_sound()
    {
        NativeBattleAcid170Frame frame = Frame(1);
        frame = frame with { State = frame.State.WriteWord(Bone40PointGlobal + 6, 0xABCD).WriteWord(Bone38PointGlobal + 6, 0xBEEF).WriteDword(FirstTargetGlobal, 0) };
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        for (int index = 1; index <= 2; index++)
        {
            pool = pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index].WriteWord(16, 0xCAFE).MarkPayloadUnknown(30, 2)) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        List<NativeBattleAcid170Operation> operations = [];
        List<(int Actor, int Selector, int Distance)> bones = [];
        NativeBattleAcid170Inputs inputs = Inputs(operations, bones) with { ReadBone502170 = (current, actor, selector, distance) =>
        {
            Assert.Equal(3, actor);
            Assert.Equal(0, distance);
            Assert.Equal((ushort)1, Read(current, ChildBank).ReadWord(14));
            Assert.Equal(Callback5DCAF0, Read(current, ChildBank + ChildStride).Callback);
            Assert.Equal((ushort)0, Read(current, ChildBank + ChildStride).ReadWord(14));
            if (selector == 38)
            {
                Assert.Equal((ushort)40, current.State.ReadWord(Bone40PointGlobal));
                Assert.Equal((ushort)0xABCD, current.State.ReadWord(Bone40PointGlobal + 6));
                Assert.Equal(40, current.Memory.Active);
            }
            bones.Add((actor, selector, distance));
            return new(current with { Memory = current.Memory with { Active = selector } }, PointView(new((short)selector, (short)actor, 123), 0x7777));
        }, Execute = (current, operation) =>
        {
            Assert.Equal(Callback5DEA10, Read(current, ChildBank + 2 * ChildStride).Callback);
            Assert.Equal((ushort)2, Read(current, ChildBank + 2 * ChildStride).ReadWord(14));
            Assert.Equal(38, current.Memory.Active);
            operations.Add(operation);
            return current;
        } };
        NativeBattleAcid170Frame result = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        Assert.Equal(new[] { (3, 40, 0), (3, 38, 0) }, bones);
        Assert.Equal((ushort)0xABCD, result.State.ReadWord(Bone40PointGlobal + 6));
        Assert.Equal((ushort)0xBEEF, result.State.ReadWord(Bone38PointGlobal + 6));
        Assert.Equal((ushort)40, result.State.ReadWord(Bone40PointGlobal));
        Assert.Equal((ushort)38, result.State.ReadWord(Bone38PointGlobal));
        Assert.All(result.Effect.Pools[1].Slots.Skip(1).Take(2), task =>
        {
            Assert.Equal((ushort)0, task.ReadWord(12));
            Assert.Equal((ushort)0xCAFE, task.ReadWord(16));
            Assert.Throws<NotSupportedException>(() => task.ReadWord(30));
        });
        Assert.Equal(new NativeBattleAcid170Sound501330(0x61AFB, 0, 128), Assert.Single(operations));
        Assert.Equal((ushort)2, Read(result, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData((ushort)0, (ushort)1, (ushort)2)]
    [InlineData((ushort)14, (ushort)15, (ushort)0x8000)]
    [InlineData((ushort)15, (ushort)16, (ushort)0)]
    [InlineData((ushort)30, (ushort)31, (ushort)0)]
    [InlineData((ushort)31, (ushort)32, (ushort)1)]
    [InlineData((ushort)255, (ushort)256, (ushort)1)]
    [InlineData((ushort)65535, (ushort)0, (ushort)1)]
    public void Birth_index_is_old_word_but_particle_mask_uses_incremented_counter_with_native_shift_and_low_word(ushort old, ushort next, ushort mask)
    {
        NativeBattleAcid170Frame frame = Frame(1);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, old));
        NativeBattleAcid170Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs([], [])).Frame;
        Assert.Equal(old, Read(result, ChildBank + ChildStride).ReadWord(14));
        Assert.Equal(next, Read(result, ChildBank).ReadWord(14));
        Assert.Equal(mask, Read(result, ChildBank + 2 * ChildStride).ReadWord(14));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Old_forty_six_uses_captured_single_physical_contact_even_with_zero_or_unknown_count()
    {
        NativeBattleAcid170Frame frame = Frame(46);
        NativeBattleActionPacketsState packets = Packets().ActivateHeader(0);
        NativeBattleActionHeaderSlot captured = packets.Headers[1];
        foreach (NativeBattleActionHeaderSlot header in new[] { captured with { Payload = captured.Payload.SetItem(16, 0) }, captured with { UnknownByteMask = 1u << 16 } })
        {
            List<NativeBattleAcid170Operation> operations = [];
            NativeBattleAcid170Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations, []) with { Packets = packets with { Headers = packets.Headers.SetItem(1, header) } }).Frame;
            NativeBattleAcid170Contact506690 contact = Assert.IsType<NativeBattleAcid170Contact506690>(Assert.Single(operations));
            Assert.Equal(1, contact.ContactIndex);
            Assert.Equal((byte)0, packets.ReadContact(contact.ContactIndex).Primary.ActorIndex);
            Assert.Equal((byte)4, packets.ReadContact(contact.ContactIndex).Secondary.ActorIndex);
            Assert.Equal((ushort)47, Read(result, ChildBank).ReadWord(12));
            Assert.Equal(frame.Random, result.Random);
        }
        packets = packets with { Headers = packets.Headers.SetItem(1, captured with { FirstContactIndex = null }) };
        Assert.Throws<NotSupportedException>(() => AdvanceMain(frame, ChildBank, Resources(), Inputs([], []) with { Packets = packets }));
    }

    [Theory]
    [InlineData((ushort)54, (ushort)55, 0)]
    [InlineData((ushort)55, (ushort)56, 2)]
    [InlineData((ushort)56, (ushort)57, 2)]
    [InlineData((ushort)32766, (ushort)32767, 2)]
    [InlineData((ushort)32767, (ushort)32768, 0)]
    [InlineData((ushort)65535, (ushort)0, 0)]
    public void Main_compares_new_signed_age_against_fifty_five_with_word_wrap(ushort age, ushort next, int code)
    {
        NativeBattleAcid170Frame frame = Frame(age);
        List<NativeBattleAcid170Operation> operations = [];
        var result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations, []));
        Assert.Equal(code, result.Result);
        Assert.Equal(next, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Empty(operations);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Root_parity_uses_eight_thousand_common_step_and_closes_only_after_child_drain()
    {
        NativeBattleAcid170Frame frame = Frame(55);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        NativeBattleAcid170Inputs paused = Inputs([], []) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(ArenaAddress, frame.State.ReadDword(CursorGlobal));
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(ArenaAddress + 0x8000u, frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)55, Read(frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)2, Read(frame, RootBank).ReadWord(12));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount)) } };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Same(arena, frame.Memory.Arena);
        frame = Initialize(frame, Resources(), Inputs([], []));
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)0, Read(frame, ChildBank).ReadWord(14));
        Assert.Same(arena, frame.Memory.Arena);
    }

    [Fact]
    public void Json_continuation_keeps_unknown_global_W_and_partial_birth_payload()
    {
        NativeBattleAcid170Frame frame = Frame(1);
        frame = frame with { State = frame.State with { Globals = frame.State.Globals.MarkUnknown(checked((int)(Bone40PointGlobal + 6 - BaseAddress)), 2) } };
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].MarkPayloadUnknown(30, 2)) }) } };
        NativeBattleAcid170Frame restored = JsonSerializer.Deserialize<NativeBattleAcid170Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleAcid170Frame expected = AdvanceMain(frame, ChildBank, Resources(), Inputs([], [])).Frame;
        NativeBattleAcid170Frame actual = AdvanceMain(restored, ChildBank, Resources(), Inputs([], [])).Frame;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadWord(Bone40PointGlobal + 6));
        Assert.Throws<NotSupportedException>(() => Read(actual, ChildBank + ChildStride).ReadWord(30));
    }

    private static NativeBattleAcid170Frame Frame(ushort age)
    {
        NativeBattleAcid170Frame frame = Inactive();
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age)) };
        return frame with { Effect = frame.Effect with { Pools = [roots, children], ReturnedDescriptor = RootDescriptor }, State = frame.State.WriteDword(CasterGlobal, 3).WriteDword(WorkingGlobal, ArenaAddress) with { RootBankStorage = null, ChildBankStorage = null } };
    }

    private static NativeBattleAcid170Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
        NativeBattleOriginalEffectState effect = new(170, 169, new(170, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(55), NativeBattleAcid170State.FromColdProcess());
    }

    private static NativeBattleAcid170Inputs Inputs(List<NativeBattleAcid170Operation> operations, List<(int Actor, int Selector, int Distance)> bones) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(actor, 0x204), (current, actor, selector, distance) =>
    {
        bones.Add((actor, selector, distance));
        return new(current, PointView(new((short)selector, (short)actor, (short)distance), 0x7777));
    }, (_, _) => throw new InvalidOperationException(), (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 32, 170, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 32, 170, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(4, 4, 0, 0, 0, 3, 0)));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleAcid170Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
        return new(Mesh(MeshAAddress, 0xA20, 73, [0, 0, 12, 36, 0, 0, 0, 24]), Mesh(MeshBAddress, 0x6E8, 72, [0, 0, 0, 0, 0, 48, 0, 0]), Sprite(SpriteAAddress, 0xA8, 6), Sprite(SpriteBAddress, 0x1AC, 16), new byte[SelectedVerticesLength].ToImmutableArray(), new int[DripCountLength].ToImmutableArray(), new("FF8_EN:D68D0C", camera.ToImmutableArray()), 0x61AFB);
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, int length, int count, int[] groups)
    {
        byte[] mesh = new byte[length];
        BitConverter.GetBytes(8 + count * 8).CopyTo(mesh, 0);
        BitConverter.GetBytes(count).CopyTo(mesh, 4);
        int cursor = 8 + count * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < groups.Length; group++)
        {
            BitConverter.GetBytes(groups[group]).CopyTo(mesh, cursor);
            cursor += 4 + groups[group] * strides[group];
        }
        return new(0, address, mesh.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite(uint address, int length, int count)
    {
        byte[] sprite = new byte[length];
        BitConverter.GetBytes((ushort)count).CopyTo(sprite, 8);
        for (int index = 0; index <= count; index++)
        {
            BitConverter.GetBytes((ushort)0x40).CopyTo(sprite, 10 + index * 2);
        }
        return new(0, address, 0, 0, sprite.ToImmutableArray());
    }
}
