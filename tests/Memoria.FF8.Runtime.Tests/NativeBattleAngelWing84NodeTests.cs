using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84NodeTests
{
    [Fact]
    public void Each_geometry_instance_samples_its_body_origin_before_copying_the_emitter_vector()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Dwords(fixture.Frame, NativeBattleCuragaSpawnTests.Emitter + 0x4C, [0, 0, 0, 0xDEADBEEF]);
        NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 0x25F8, NativeBattleCuragaSpawnTests.Node, 0x16F80, useSharedScratch: true);
        NativeBattleEffectNodeBank bank = new(NativeBattleCuragaExecutableState.Address, frame.Memory.CuragaExecutable!.Data!.Value);
        int calls = 0;
        NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => NativeBattleCuragaSpawnTests.Node)
        {
            SpawnOrigin = (ref NativeCrtRandomState state) =>
            {
                state = state.Draw(out ushort value);
                calls++;
                return new((short)value, (short)-calls, (short)(calls * 2));
            }
        };
        NativeCrtRandomState random = new(1);
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
        lifecycle.Spawn(NativeBattleCuragaSpawnTests.Emitter, 2, bank.Read(NativeBattleCuragaExecutableState.Address + 0x104, 0x158), ref random);
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        Assert.Equal(2, calls);
        NativeCrtRandomState expected = new(1);
        for (int instance = 0; instance < 2; instance++)
        {
            expected = expected.Draw(out ushort value);
            byte[] bytes = written.Memory.Arena!.Read(NativeBattleCuragaSpawnTests.Node + 0xDC + instance * 16, 16);
            Assert.Equal(unchecked((uint)(value << 16)), BinaryPrimitives.ReadUInt32LittleEndian(bytes));
            Assert.Equal(unchecked((uint)(-(instance + 1) << 16)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
            Assert.Equal((uint)((instance + 1) * 2 << 16), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
            Assert.Equal(0xDEADBEEFu, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
        }
        Assert.Equal(expected, random);
    }

    [Theory]
    [InlineData(7, 8)]
    [InlineData(65535, 0)]
    public void Generic_delay_compares_signed_native_WORDs_before_initialization(ushort counter, ushort expectedNext)
    {
        var fixture = NativeBattleAngelWing84ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(3, 0x7913C0, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteWord(36, counter).WriteWord(664, 8));
        NativeBattleAngelWing84Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleAngelWing84NodeCallbackStep step = NativeBattleAngelWing84Nodes.AdvanceGeneric(frame, appended.Address, new(default), fixture.Inputs, new(1), new(fixture.Inputs.Camera, 256, 160, 120), 0, NativeBattleLogicalOrdering.Clear(), NativeBattleGteColorInterpolation.CodeConstants45F270, (ref NativeCrtRandomState _) => throw new InvalidOperationException("Waiting context cannot sample body bones."));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(appended.Address);
        Assert.Equal(expectedNext, task.ReadWord(36));
        Assert.Equal((byte)0, task.ReadByte(41));
        Assert.Null(step.Frame.State.NodeData);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Generic_completed_context_marks_done_and_releases_the_original_parent_once()
    {
        var fixture = NativeBattleAngelWing84ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(3, 0x7913C0, 676, root, (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteByte(41, 1).WriteWord(84, 2));
        NativeBattleAngelWing84Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools }, State = fixture.Frame.State with { PassEmitterCount = 0, PassGeometryCount = 0 } };
        NativeBattleAngelWing84NodeCallbackStep step = NativeBattleAngelWing84Nodes.AdvanceGeneric(frame, appended.Address, new(default), fixture.Inputs, new(1), new(fixture.Inputs.Camera, 256, 160, 120), 0, NativeBattleLogicalOrdering.Clear(), NativeBattleGteColorInterpolation.CodeConstants45F270, (ref NativeCrtRandomState _) => throw new InvalidOperationException("Completed context cannot sample body bones."));
        hierarchy = new(step.Frame.Effect.Pools);
        Assert.Equal(2, step.Result);
        Assert.Equal((byte)2, hierarchy.Read(appended.Address).ReadByte(41));
        Assert.Equal(1, hierarchy.Read(appended.Address).ReadByte(38) & 1);
        Assert.Equal((byte)0, hierarchy.Read(root).ReadByte(40));
        Assert.Equal(1u, step.Random.Seed);
    }

}
