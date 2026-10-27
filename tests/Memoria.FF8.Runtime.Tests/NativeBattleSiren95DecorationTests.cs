using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95DecorationTests
{
    [Fact]
    public void Emitters_preserve_exact_five_child_random_order_and_odd_clock_gate()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = StageOne(fixture.Frame, fixture.Inputs.Packets);
        uint first = Address(frame, 0x743C70);
        frame = Put(frame, first, Task(frame, first).WriteWord(0x24, 2).WriteByte(0x29, 2));
        var empty = NativeBattleSiren95Decorations.AdvanceEmitter(frame, first, fixture.Inputs.Packets);
        Assert.Equal(frame.Random, empty.Frame.Random);
        Assert.DoesNotContain(empty.Frame.Effect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x743DD0);
        uint second = Address(frame, 0x744060);
        frame = Put(frame, second, Task(frame, second).WriteByte(0x29, 2));
        NativeCrtRandomState expected = frame.Random;
        expected = expected.Draw(out ushort segment);
        expected = expected.Draw(out ushort fraction);
        expected = expected.Draw(out ushort angle);
        for (int index = 3; index < 60; index++)
        {
            expected = expected.Draw(out _);
        }
        var result = NativeBattleSiren95Decorations.AdvanceEmitter(frame, second, fixture.Inputs.Packets);
        Assert.Equal(expected, result.Frame.Random);
        NativeBattleModulePoolTask[] children = result.Frame.Effect.Pools.SelectMany(pool => pool.Slots).Where(task => task.Callback == 0x7441D0).ToArray();
        Assert.Equal(5, children.Length);
        Assert.Equal((ushort)(segment % 16), children[0].ReadWord(0xCC));
        Assert.Equal((ushort)(fraction & 15), children[0].ReadWord(0xD4));
        Assert.Equal(unchecked((ushort)(NativeBattleCameraMath.Sine(angle & 4095) / 32)), children[0].ReadWord(0xAE));
        Assert.Equal(unchecked((ushort)(NativeBattleCameraMath.Cosine(angle & 4095) / 32)), children[0].ReadWord(0xB0));
        Assert.Equal((byte)5, Task(result.Frame, second).ReadByte(0x28));
        Assert.All(children, child =>
        {
            Assert.Equal(NativeBattleSiren95Controller.MainBank, child.ReadDword(0x10));
            Assert.Equal(second, child.ReadDword(0x18));
        });
    }

    [Fact]
    public void Child_uses_current_vertex_cache_and_four_world_positions_preserves_padding_and_snapshot_random()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = StageOne(fixture.Frame, fixture.Inputs.Packets);
        uint emitter = Address(frame, 0x744060);
        frame = Put(frame, emitter, Task(frame, emitter).WriteByte(0x29, 2));
        frame = NativeBattleSiren95Decorations.AdvanceEmitter(frame, emitter, fixture.Inputs.Packets).Frame;
        uint address = Address(frame, 0x7441D0);
        NativeBattleSiren95State state = frame.Memory.Siren95!;
        state = state.WriteDword(0x258EC60, 4096).WriteDword(0x258EC64, 4096).WriteDword(0x258EC68, 4096);
        state = state.Write(0x258FB80, Words(1, 2, 3)).Write(state.DirectorAddress1533010!.Value + 0xC, BitConverter.GetBytes((short)10));
        for (int index = 0; index < 17; index++)
        {
            state = state.Write(0x258E8A0u + (uint)(8 * index), Words((short)(16 * index), (short)(32 * index), (short)(48 * index), unchecked((short)0xBEEF)));
        }
        frame = frame with { Memory = frame.Memory with { Siren95 = state } };
        NativeBattleModulePoolTask child = Task(frame, address);
        for (int corner = 0; corner < 4; corner++)
        {
            child = child.WriteWord(0x72 + corner * 8, (ushort)(0x9000 + corner));
        }
        frame = Put(frame, address, child);
        string saved = NativeBattleTestSnapshots.Json(frame);
        List<(short X, short Y, short Z, ushort Padding)> points = [];
        NativeBattleSiren95Frame Draw(NativeBattleSiren95Frame current, uint taskAddress)
        {
            NativeBattleModulePoolTask task = Task(current, taskAddress);
            points.Add((unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)), task.ReadWord(0x22)));
            return current;
        }
        var result = NativeBattleSiren95Decorations.AdvanceChild(frame, address, Draw);
        Assert.Equal(4, points.Count);
        for (int corner = 0; corner < 4; corner++)
        {
            int segment = child.ReadWord(0xCC + corner * 2);
            int fraction = child.ReadWord(0xD4 + corner * 2);
            int x = 16 * segment + fraction + unchecked((short)child.ReadWord(0xAE + corner * 8));
            int y = 32 * segment + 2 * fraction + unchecked((short)child.ReadWord(0xB0 + corner * 8));
            int z = 48 * segment + 3 * fraction + unchecked((short)child.ReadWord(0xB2 + corner * 8));
            Assert.Equal(((short)(1 - x), (short)(2 + y), (short)(13 - 8192 - z), (ushort)(0x9000 + corner)), points[corner]);
        }
        points.Clear();
        var restored = NativeBattleSiren95Decorations.AdvanceChild(NativeBattleTestSnapshots.RestoreJson<NativeBattleSiren95Frame>(saved)!, address, Draw);
        Assert.Equal(NativeBattleTestSnapshots.Json(result.Frame), NativeBattleTestSnapshots.Json(restored.Frame));
        Assert.Equal(4, points.Count);
        frame = Put(result.Frame, address, Task(result.Frame, address).WriteByte(0x29, 1).WriteWord(0x50, 15));
        int hiddenCalls = 0;
        result = NativeBattleSiren95Decorations.AdvanceChild(frame, address, (current, taskAddress) =>
        {
            Assert.NotEqual(0, Task(current, taskAddress).ReadByte(0x26) & 4);
            hiddenCalls++;
            return current;
        });
        Assert.Equal(4, hiddenCalls);
        Assert.True(result.Complete);
        Assert.Equal((ushort)15, Task(result.Frame, address).ReadWord(0x50));
    }

    [Fact]
    public void Second_surface_emits_three_children_with_two_random_draws_per_corner_and_uses_the_25_point_cache()
    {
        var fixture = NativeBattleSiren95ControllerTests.Fixture();
        NativeBattleSiren95Frame frame = StageOne(fixture.Frame, fixture.Inputs.Packets);
        uint emitter = Address(frame, 0x744060);
        frame = Put(frame, emitter, (NativeBattleTaskFixture.EditTask(Task(frame, emitter), taskData => taskData with { Callback = 0x744450 })).WriteByte(0x29, 2));
        NativeCrtRandomState expected = frame.Random;
        expected = expected.Draw(out ushort segment);
        expected = expected.Draw(out ushort fraction);
        for (int index = 2; index < 24; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = NativeBattleSiren95Decorations.AdvanceEmitter(frame, emitter, fixture.Inputs.Packets).Frame;
        Assert.Equal(expected, frame.Random);
        NativeBattleModulePoolTask[] children = frame.Effect.Pools.SelectMany(pool => pool.Slots).Where(task => task.Callback == 0x744570).ToArray();
        Assert.Equal(3, children.Length);
        Assert.Equal((ushort)(segment % 24), children[0].ReadWord(0xCC));
        Assert.Equal((ushort)(fraction & 15), children[0].ReadWord(0xD4));
        uint address = Address(frame, 0x744570);
        NativeBattleSiren95State state = frame.Memory.Siren95!;
        state = state.WriteDword(0x258EC60, 4096).WriteDword(0x258EC64, 4096).WriteDword(0x258EC68, 4096);
        state = state.Write(0x258FB80, Words(1, 2, 3)).Write(state.DirectorAddress1533010!.Value + 0xC, BitConverter.GetBytes((short)10));
        for (int index = 0; index < 25; index++)
        {
            state = state.Write(0x258BAA0u + (uint)(8 * index), Words((short)(16 * index), (short)(32 * index), (short)(48 * index), -1));
        }
        frame = frame with { Memory = frame.Memory with { Siren95 = state } };
        NativeBattleModulePoolTask child = Task(frame, address);
        for (int corner = 0; corner < 4; corner++)
        {
            child = child.WriteWord(0xCC + 2 * corner, 23).WriteWord(0xD4 + 2 * corner, 15).WriteWord(0xAE + 8 * corner, 777).WriteWord(0x72 + 8 * corner, 0xABCD);
        }
        frame = Put(frame, address, child);
        int draws = 0;
        var result = NativeBattleSiren95Decorations.AdvanceChild(frame, address, (current, pointer) =>
        {
            var point = Task(current, pointer);
            Assert.Equal(unchecked((ushort)(1 - 383)), point.ReadWord(0x1C));
            Assert.Equal((ushort)(2 + 766), point.ReadWord(0x1E));
            Assert.Equal(unchecked((ushort)(13 - 4096 - 1149)), point.ReadWord(0x20));
            Assert.Equal((ushort)0xABCD, point.ReadWord(0x22));
            draws++;
            return current;
        });
        Assert.Equal(4, draws);
        Assert.Equal(unchecked((ushort)-16), Task(result.Frame, address).ReadWord(0x56));
        Assert.Equal((byte)1, Task(result.Frame, address).ReadByte(0x29));
    }

    private static NativeBattleSiren95Frame StageOne(NativeBattleSiren95Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleSiren95State state = frame.Memory.Siren95!;
        frame = frame with { Memory = frame.Memory with { Siren95 = state.Write(state.DirectorAddress1533010!.Value + 0x40, BitConverter.GetBytes((short)1)) } };
        return NativeBattleSiren95Actors.Spawn73BCF0(frame, NativeBattleSiren95Controller.MainBank, new(packets, (_, _, _, _) => throw new InvalidOperationException()));
    }

    private static uint Address(NativeBattleSiren95Frame frame, uint callback)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            for (int index = 0; index < pool.Slots.Length; index++)
            {
                if (pool.Slots[index].Callback == callback)
                {
                    return pool.BankAddress + (uint)(index * pool.Stride);
                }
            }
        }
        throw new InvalidOperationException("Missing fixture callback.");
    }

    private static NativeBattleModulePoolTask Task(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static byte[] Words(params short[] words) => words.SelectMany(BitConverter.GetBytes).ToArray();
}
