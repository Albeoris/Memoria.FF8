using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleCannonBlow151Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCannonBlow151ControllerTests
{
    [Fact]
    public void Entry_resets_full_task_strides_and_only_particle_heads_without_clearing_COMMON()
    {
        NativeBattleCannonBlow151Frame frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0, [0xBA]) }, State = frame.State.WriteDword(0x244D6A8, uint.MaxValue).WriteWord(0x244D6AC, 0xCAFE).WriteView(0x244E010, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, 32), ChildBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, 3600) } };
        frame = Initialize(frame, Resources(), Inputs());
        Assert.Equal((byte)0xBA, frame.Memory.Arena!.Read(0, 1)[0]);
        Assert.Equal(0x20DFAB8u, frame.State.ReadDword(0x244FD58));
        Assert.Equal(6u, frame.State.ReadDword(0x244D670));
        Assert.Equal(0u, frame.State.ReadDword(0x244D6A8));
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(0x244D6AC));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(0x244E010));
        Assert.Null(frame.State.RootBankStorage);
        Assert.Null(frame.State.ChildBankStorage);
        Assert.All(frame.Effect.Pools[1].Slots.Skip(1), task => Assert.All(task.Payload, value => Assert.Equal((byte)0, value)));
    }

    [Fact]
    public void Trajectory_uses_bone_fifteen_and_preserves_unwritten_W_and_matrix_padding()
    {
        NativeBattleCannonBlow151Frame frame = Active(0x60CA90);
        frame = frame with { State = frame.State.WriteView(0x244F93E, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteView(0x244F8BA, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        for (int sample = 0; sample < 32; sample++)
        {
            frame = frame with { State = frame.State.WriteView(0x244F95Eu + (uint)(sample * 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        NativeBattleCannonBlow151Inputs inputs = Inputs() with { ReadBone502170 = (current, actor, selector, offset) =>
        {
            Assert.Equal(3, actor);
            Assert.Equal(15, selector);
            Assert.Equal(4096, offset);
            return new(current, PointView(new(100, 200, 300)));
        } };
        var result = AdvanceMain(frame, 0x244E9F8, Resources(), inputs);
        Assert.Equal(new byte[] { 44, 1, 206, 255, 50, 0 }, result.Frame.State.Read(0x244F938, 6));
        Assert.Equal((short)218, unchecked((short)result.Frame.State.ReadWord(0x244F888)));
        Assert.Equal((short)457, unchecked((short)result.Frame.State.ReadWord(0x244F88C)));
        Assert.Equal(result.Frame.State.Read(0x244F938, 6), result.Frame.State.Read(0x244F958, 6));
        Assert.Equal(result.Frame.State.Read(0x244F888, 6), result.Frame.State.Read(0x244FA50, 6));
        Assert.Throws<NotSupportedException>(() => result.Frame.State.ReadWord(0x244F93E));
        Assert.Throws<NotSupportedException>(() => result.Frame.State.ReadWord(0x244F95E));
        Assert.Throws<NotSupportedException>(() => result.Frame.State.ReadWord(0x244F8BA));
        Assert.Equal(AfterDraws(frame.Random, 2), result.Frame.Random);
        Assert.Single(result.Frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Age_one_spawns_seven_exact_callbacks_and_sound_after_five_CRT_draws()
    {
        NativeBattleCannonBlow151Frame frame = Active(0x60CA90);
        frame = Write(frame, 0x244E9F8, Read(frame, 0x244E9F8).WriteWord(12, 1));
        List<NativeBattleCannonBlow151Operation> operations = [];
        var result = AdvanceMain(frame, 0x244E9F8, Resources(), Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }));
        Assert.Equal(new uint[] { 0x60D260, 0x60D3B0, 0x60D670, 0x60D790, 0x60D850, 0x60DA10, 0x60DC20 }, result.Frame.Effect.Pools[1].Slots.Skip(1).Take(7).Select(task => task.Callback));
        Assert.All(result.Frame.Effect.Pools[1].Slots.Skip(1).Take(7), task => Assert.Equal((ushort)0, task.ReadWord(14)));
        Assert.Equal(AfterDraws(frame.Random, 5), result.Frame.Random);
        Assert.Equal(0x61AD9u, Assert.IsType<NativeBattleCannonBlow151Sound>(Assert.Single(operations)).Packed);
    }

    [Fact]
    public void Age_fourteen_spawns_five_delayed_particles_for_each_actual_contact_after_two_impact_meshes()
    {
        NativeBattleCannonBlow151Frame frame = Active(0x60CA90);
        frame = Write(frame, 0x244E9F8, Read(frame, 0x244E9F8).WriteWord(12, 14));
        int bones = 0;
        List<short> shakes = [];
        NativeBattleCannonBlow151Inputs inputs = Inputs((current, operation) =>
        {
            shakes.Add(Assert.IsType<NativeBattleCannonBlow151AdjustShake>(operation).Delta);
            return current;
        }) with { ReadBone502170 = (current, actor, selector, offset) =>
        {
            Assert.Equal(6, actor);
            Assert.Equal(0xF1, selector);
            Assert.Equal(0, offset);
            bones++;
            return new(current, PointView(new(1, 2, 3)));
        } };
        var result = AdvanceMain(frame, 0x244E9F8, Resources(), inputs);
        Assert.Equal(1, bones);
        Assert.Equal(new uint[] { 0x60DE40, 0x60E060, 0x60E1B0, 0x60E1B0, 0x60E1B0, 0x60E1B0, 0x60E1B0 }, result.Frame.Effect.Pools[1].Slots.Skip(1).Take(7).Select(task => task.Callback));
        Assert.Equal(AfterDraws(frame.Random, 30), result.Frame.Random);
        Assert.Equal(new short[] { -64 }, shakes);
        Assert.All(result.Frame.Effect.Pools[1].Slots.Skip(3).Take(5), task => Assert.Throws<NotSupportedException>(() => task.ReadWord(22)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public void Main_rejects_unproduced_trajectory_lanes(short index)
    {
        NativeBattleCannonBlow151Frame frame = Active(0x60CA90);
        frame = Write(frame, 0x244E9F8, Read(frame, 0x244E9F8).WriteWord(14, unchecked((ushort)index)));
        Assert.Throws<NotSupportedException>(() => AdvanceMain(frame, 0x244E9F8, Resources(), Inputs()));
    }

    [Fact]
    public void Main_pause_contact_and_completion_preserve_original_timing()
    {
        NativeBattleCannonBlow151Frame frame = Active(0x60CA90);
        List<NativeBattleCannonBlow151Operation> operations = [];
        NativeBattleCannonBlow151Inputs inputs = Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        Assert.Equal(frame, AdvanceMain(frame, 0x244E9F8, Resources(), inputs with { GraphicsFlags = 0x201 }).Frame);
        frame = Write(frame, 0x244E9F8, Read(frame, 0x244E9F8).WriteWord(12, 30));
        var contact = AdvanceMain(frame, 0x244E9F8, Resources(), inputs);
        Assert.Equal(0, Assert.IsType<NativeBattleCannonBlow151ContactsAll>(Assert.Single(operations)).HeaderIndex);
        Assert.Equal(0, contact.Result);
        frame = Write(contact.Frame, 0x244E9F8, Read(contact.Frame, 0x244E9F8).WriteWord(12, 40));
        Assert.Equal(2, AdvanceMain(frame, 0x244E9F8, Resources(), inputs).Result);
    }

    [Fact]
    public void Root_waiter_restores_and_waits_for_actual_asynchronous_completion_BYTE()
    {
        NativeBattleCannonBlow151Frame frame = Active(0x60CA90);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0] with { Flags = 0 }), Head = null, Tail = null };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        int requests = 0;
        NativeBattleCannonBlow151Inputs inputs = Inputs((current, operation) =>
        {
            NativeBattleCannonBlow151ReleaseCommon release = Assert.IsType<NativeBattleCannonBlow151ReleaseCommon>(operation);
            Assert.Equal(0x20DFAB8u, release.SourceAddress);
            Assert.Equal(0x244D688u, release.TaskAddress);
            requests++;
            return current;
        }) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0, requests);
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[1].ReadWord(12));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = JsonSerializer.Deserialize<NativeBattleCannonBlow151Frame>(JsonSerializer.Serialize(frame))!;
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = frame with { Effect = WriteCompletionByte(frame.Effect, 0x244D696, 0xFF) };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(new byte[] { 0xFF, 0 }, frame.State.RootBankStorage!.Read(30, 2));
        Assert.NotNull(frame.State.ChildBankStorage);
    }

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw(out _);
        }
        return state;
    }

    private static NativeBattleCannonBlow151Frame Active(uint callback)
    {
        NativeBattleCannonBlow151Frame frame = Frame();
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x244D698, 0x244D678, 16, 2);
        (roots, _) = roots.Append(0x60E270);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x244E9E8, 0x244E9F8, 36, 100);
        (children, _) = children.Append(callback);
        return frame with { Effect = frame.Effect with { Pools = [roots, children] }, State = frame.State.WriteDword(0x244FD58, 0x20DFAB8).WriteDword(0x244F92C, 3) with { RootBankStorage = null, ChildBankStorage = null } };
    }
    private static NativeBattleCannonBlow151Frame Frame()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(151, 150, new(151, 0x60C9C0, 0x60C9A0), new(3, 0, 1, 0, 0, 0), 0x244D698) { ResourcePointers = [new(0x244F930, 0x30000)] };
        return new(memory, effect, new(55), NativeBattleCannonBlow151State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleCannonBlow151Inputs Inputs(Func<NativeBattleCannonBlow151Frame, NativeBattleCannonBlow151Operation, NativeBattleCannonBlow151Frame>? execute = null)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        byte[] payload = new byte[20];
        payload[16] = 1;
        packets = packets with { Headers = packets.Headers.SetItem(0, new(payload.ToImmutableArray(), 0, 2, null)), Contacts = packets.Contacts.SetItem(2, new(new(6, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))) };
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (current, _, _, _) => new(current, PointView(new(100, 200, 300))), (current, _) => new(current, PointView(new(400, 500, 600))), execute ?? ((current, _) => current));
    }
    private static NativeBattleCannonBlow151Resources Resources()
    {
        byte[] camera = new byte[0x110];
        BitConverter.GetBytes((ushort)0x110).CopyTo(camera, 6);
        return new(Mesh(0xDC0E10, 0x688, 60, [0, 0, 0, 0, 0, 48, 0, 0]), Mesh(0xDC0900, 0x510, 49, [0, 0, 0, 36, 0, 0, 0, 0]), Mesh(0xDBF4C0, 0xC30, 73, [0, 0, 0, 0, 0, 0, 12, 60]), Mesh(0xDC1498, 0x928, 72, [0, 0, 0, 0, 0, 0, 0, 48]), Mesh(0xDC00F0, 0x810, 61, [0, 0, 12, 36, 0, 0, 0, 12]), Sprite(0xDBED9C, 0x1AC, 16), Sprite(0xDBEF48, 0x1AC, 16), Sprite(0xDBF0F4, 0x1AC, 16), Sprite(0xDBF2A0, 0x74, 4), Sprite(0xDBF314, 0x1AC, 16), new("FF8_EN:DBEC8C", camera.ToImmutableArray()), 0x61AD9);
    }
    private static NativeBattleStaticMeshResource Mesh(uint address, int length, int vertices, int[] counts)
    {
        byte[] data = new byte[length];
        BitConverter.GetBytes(8 + vertices * 8).CopyTo(data, 0);
        BitConverter.GetBytes(vertices).CopyTo(data, 4);
        int cursor = 8 + vertices * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            BitConverter.GetBytes(counts[group]).CopyTo(data, cursor);
            cursor += 4 + counts[group] * strides[group];
        }
        return new(0, address, data.ToImmutableArray());
    }
    private static NativeBattleModuleResource Sprite(uint address, int length, int count)
    {
        byte[] data = new byte[length];
        BitConverter.GetBytes((ushort)count).CopyTo(data, 8);
        for (int index = 0; index <= count; index++)
        {
            BitConverter.GetBytes((ushort)0x40).CopyTo(data, 10 + index * 2);
        }
        return new(0, address, 0, 0, data.ToImmutableArray());
    }
}
