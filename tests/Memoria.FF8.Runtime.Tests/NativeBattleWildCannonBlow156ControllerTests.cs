using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWildCannonBlow156ControllerTests
{
    [Fact]
    public void Producer_consumes_unknown_banks_and_clears_only_auxiliary_record_heads()
    {
        NativeBattleWildCannonBlow156Frame frame = Frame();
        frame = frame with { State = frame.State with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, 32), ChildBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, 3600) } };
        frame = frame with { State = frame.State.WriteDword(0x23ECE78, 0xFFFFFFFF).WriteWord(0x23ECE7C, 0xCAFE).WriteView(0x23EDC90, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        frame = Initialize(frame, Resources(), Inputs());
        Assert.Null(frame.State.RootBankStorage);
        Assert.Null(frame.State.ChildBankStorage);
        Assert.Equal(2, frame.Effect.Pools[0].Slots.Length);
        Assert.Equal(0u, frame.State.ReadDword(0x23ECE78));
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(0x23ECE7C));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(0x23EDC90));
        Assert.Equal(6u, frame.State.ReadDword(0x23ECE40));
        Assert.Equal(0x20DFAB8u, frame.State.ReadDword(0x23F07E8));
        Assert.All(frame.Effect.Pools[1].Slots.Skip(1), task => Assert.All(task.Payload, value => Assert.Equal((byte)0, value)));
    }

    [Fact]
    public void Actual_second_root_waits_for_asynchronous_common_completion_and_restores()
    {
        NativeBattleWildCannonBlow156Frame frame = Active(0x605620);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0] with { Flags = 0 }), Head = null, Tail = null };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        int requests = 0;
        NativeBattleWildCannonBlow156Inputs inputs = Inputs((current, operation) =>
        {
            var release = Assert.IsType<NativeBattleWildCannonBlow156ReleaseCommon>(operation);
            Assert.Equal(0x20DFAB8u, release.SourceAddress);
            Assert.Equal(0x23ECE58u, release.TaskAddress);
            requests++;
            return current;
        }) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0, requests);
        Assert.Equal(1, frame.Effect.Pools[0].Head);
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[1].ReadWord(12));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        Assert.Equal(0x23ECE68u, frame.Effect.ReturnedDescriptor);
        frame = JsonSerializer.Deserialize<NativeBattleWildCannonBlow156Frame>(JsonSerializer.Serialize(frame))!;
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = frame with { Effect = WriteCompletionByte(frame.Effect, 0x23ECE66, 1) };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Equal((byte)1, frame.State.RootBankStorage.Read(30, 1)[0]);
    }

    [Fact]
    public void Director_spawns_four_physical_trajectory_indices_and_contacts_at_old_age_36()
    {
        NativeBattleWildCannonBlow156Frame frame = Active(0x605620);
        List<NativeBattleWildCannonBlow156Operation> operations = [];
        NativeBattleWildCannonBlow156Inputs inputs = Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        foreach (ushort age in new ushort[] { 1, 4, 7, 10 })
        {
            frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(12, age));
            (frame, _) = AdvanceDirector(frame, 0x23EF488, Resources(), inputs);
        }
        Assert.Equal(new ushort[] { 0, 1, 2, 3 }, frame.Effect.Pools[1].Slots.Skip(1).Take(4).Select(task => task.ReadWord(14)));
        Assert.Equal(2, operations.Count(operation => operation is NativeBattleWildCannonBlow156Sound));
        frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(12, 36));
        (frame, _) = AdvanceDirector(frame, 0x23EF488, Resources(), inputs);
        Assert.Single(operations, operation => operation is NativeBattleWildCannonBlow156ContactsAll);
        frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(12, 45));
        Assert.Equal(2, AdvanceDirector(frame, 0x23EF488, Resources(), inputs).Result);
    }

    [Theory]
    [InlineData(0, 9, -100)]
    [InlineData(1, 15, 300)]
    [InlineData(2, 9, -100)]
    [InlineData(3, 15, 300)]
    public void Trajectory_uses_native_bone_and_curve_endpoints_with_retained_W(short index, int selector, short expectedStartX)
    {
        NativeBattleWildCannonBlow156Frame frame = Active(0x6056E0);
        frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(14, (ushort)index));
        uint start = 0x23F03C8u + (uint)(index * 8);
        uint samples = 0x23F03E8u + (uint)(index * 256);
        uint matrix = 0x23F0338u + (uint)(index * 32);
        frame = frame with { State = frame.State.WriteView(start + 6, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteView(matrix + 18, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        for (int sample = 0; sample < 32; sample++)
        {
            frame = frame with { State = frame.State.WriteView(samples + (uint)(sample * 8 + 6), NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 7; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleWildCannonBlow156Inputs inputs = Inputs() with { ReadBone502170 = (current, actor, bone, scale) =>
        {
            Assert.Equal(3, actor);
            Assert.Equal(selector, bone);
            Assert.Equal(4096, scale);
            return new(current, PointView(new(100, 200, 300)));
        } };
        var actual = AdvanceMain(frame, 0x23EF488, Resources(), inputs);
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(expectedStartX, unchecked((short)actual.Frame.State.ReadWord(start)));
        Assert.Equal(actual.Frame.State.Read(start, 6), actual.Frame.State.Read(samples, 6));
        Assert.Equal(actual.Frame.State.Read(0x23F0318u + (uint)(index * 8), 6), actual.Frame.State.Read(samples + 31 * 8, 6));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(start + 6));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(samples + 6));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(matrix + 18));
        Assert.Equal(6, actual.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback != 0x6056E0));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Main_impact_draws_five_CRT_values_and_applies_exact_shake_then_retires_at_20()
    {
        NativeBattleWildCannonBlow156Frame frame = Active(0x6056E0);
        frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(12, 13));
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 5; draw++)
        {
            expected = expected.Draw(out _);
        }
        List<short> shake = [];
        NativeBattleWildCannonBlow156Inputs inputs = Inputs((current, operation) =>
        {
            shake.Add(Assert.IsType<NativeBattleWildCannonBlow156AdjustShake>(operation).Delta);
            return current;
        });
        (frame, _) = AdvanceMain(frame, 0x23EF488, Resources(), inputs);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(new short[] { 64 }, shake);
        frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(12, 19));
        Assert.Equal(2, AdvanceMain(frame, 0x23EF488, Resources(), inputs).Result);
    }

    [Fact]
    public void Main_contact_sprites_use_each_physical_contact_actor_and_preserve_task_W()
    {
        NativeBattleWildCannonBlow156Frame frame = Active(0x6056E0);
        frame = Write(frame, 0x23EF488, Read(frame, 0x23EF488).WriteWord(12, 15).WriteWord(14, 2));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(1, pool.Slots[1].WriteWord(22, 0xBEEF)) }) } };
        NativeBattleWildCannonBlow156Inputs inputs = Inputs() with { ReadBone502170 = (current, actor, selector, scale) =>
        {
            Assert.Equal(6, actor);
            Assert.Equal(0xF1, selector);
            Assert.Equal(0, scale);
            return new(current, PointView(new(1, 2, 3)));
        } };
        (frame, _) = AdvanceMain(frame, 0x23EF488, Resources(), inputs);
        NativeBattleModulePoolTask child = frame.Effect.Pools[1].Slots[1];
        Assert.Equal(0x606A60u, child.Callback);
        Assert.Equal((ushort)0x400, child.ReadWord(24));
        Assert.Equal((ushort)0xBEEF, child.ReadWord(22));
    }

    private static NativeBattleWildCannonBlow156Frame Active(uint callback)
    {
        NativeBattleWildCannonBlow156Frame frame = Frame();
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x23ECE68, 0x23ECE48, 16, 2);
        (roots, _) = roots.Append(0x606C20);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23EF478, 0x23EF488, 36, 100);
        (children, _) = children.Append(callback);
        return frame with { Effect = frame.Effect with { Pools = [roots, children] }, State = frame.State.WriteDword(0x23F07E8, 0x20DFAB8).WriteDword(0x23F03BC, 3) with { RootBankStorage = null, ChildBankStorage = null } };
    }
    private static NativeBattleWildCannonBlow156Frame Frame()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(156, 155, new(156, 0x605530, 0x605510), new(3, 0, 1, 0, 0, 0), 0x23ECE68) { ResourcePointers = [new(0x23F03C0, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleWildCannonBlow156State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleWildCannonBlow156Inputs Inputs(Func<NativeBattleWildCannonBlow156Frame, NativeBattleWildCannonBlow156Operation, NativeBattleWildCannonBlow156Frame>? execute = null)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        byte[] payload = new byte[20];
        payload[16] = 1;
        packets = packets with { Headers = packets.Headers.SetItem(0, new(payload.ToImmutableArray(), 0, 2, null)), Contacts = packets.Contacts.SetItem(2, new(new(6, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))) };
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (current, _, _, _) => new(current, PointView(new(100, 200, 300))), (current, _) => new(current, PointView(new(400, 500, 600))), execute ?? ((current, _) => current));
    }
    private static NativeBattleWildCannonBlow156Resources Resources()
    {
        byte[] camera = new byte[0xB4];
        BitConverter.GetBytes((ushort)0xB4).CopyTo(camera, 6);
        return new(Mesh(0xDB6DFC, 0x688, 60, [0, 0, 0, 0, 0, 48, 0, 0]), Mesh(0xDB68EC, 0x510, 49, [0, 0, 0, 36, 0, 0, 0, 0]), Mesh(0xDB54AC, 0xC30, 73, [0, 0, 0, 0, 0, 0, 12, 60]), Mesh(0xDB7484, 0x928, 72, [0, 0, 0, 0, 0, 0, 0, 48]), Sprite(0xDB5300, 0x1AC, 16), Sprite(0xDB528C, 0x74, 4), Sprite(0xDB4F34, 0x1AC, 16), Sprite(0xDB50E0, 0x1AC, 16), Sprite(0xDB4D88, 0x1AC, 16), new("FF8_EN:DB4CD4", camera.ToImmutableArray()), 0x61AD9, 0x61ADB);
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
