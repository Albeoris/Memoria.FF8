using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShellActorScaleTests
{
    [Theory]
    [InlineData(0, 112, 196, 310)]
    [InlineData(1, 106, 192, 310)]
    public void Actor_size_scales_target_offsets_only_and_retains_caster_offsets(byte anchor, int x, int y, int z)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x11] = anchor;
            source[0x1C] = 0;
            source[0x25] = 0;
            BinaryPrimitives.WriteInt32LittleEndian(source, 6 << 16);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), -8 << 16);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 10 << 16);
        });
        var owner = Kernel(frame, fixture.Task);
        foreach (int offset in new[] { 0x94, 0x1D4 })
        {
            owner.Memory.ContextDword(offset, 100u << 16);
            owner.Memory.ContextDword(offset + 4, 200u << 16);
            owner.Memory.ContextDword(offset + 8, 300u << 16);
        }
        owner.Lifecycle.InitializeEmitter(NativeBattleCuragaSpawnTests.Emitter, owner.Bank.Read(NativeBattleCuragaExecutableState.Address + 0x104, 0x158));
        Assert.Equal(unchecked((uint)(x << 16)), owner.Memory.Dword(NativeBattleCuragaSpawnTests.Emitter + 0x4C));
        Assert.Equal(unchecked((uint)(y << 16)), owner.Memory.Dword(NativeBattleCuragaSpawnTests.Emitter + 0x50));
        Assert.Equal(unchecked((uint)(z << 16)), owner.Memory.Dword(NativeBattleCuragaSpawnTests.Emitter + 0x54));
    }

    [Fact]
    public void Actor_size_scales_the_final_view_rotation_after_instance_position_without_scaling_translation()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaSpawnStep spawned = NativeBattleCuragaNodeKinematics.Spawn8818A0(fixture.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 1, new(1));
        var owner = Kernel(spawned.Frame, fixture.Task);
        owner.Lifecycle.AdvanceDrawable(NativeBattleCuragaSpawnTests.Node, owner.Bank.Read(NativeBattleCuragaExecutableState.Address + 0x104, 0x158));
        short[] rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)owner.Memory.Word(NativeBattleCuragaSpawnTests.Node + 0xAC + index * 2))).ToArray();
        Assert.Equal(new short[] { 8192, 0, 0, 0, 2048, 0, 0, 0, 4096 }, rotation);
        Assert.Equal(20000u, owner.Memory.Dword(NativeBattleCuragaSpawnTests.Node + 0xC8));
    }

    [Fact]
    public void Spread_matrix_is_scaled_before_the_saturated_direction_transform()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x34] = 1;
            source[0x33] = 1;
            source[0x22] = 0;
        });
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xCC, 5);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xD0, -7);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xD4, 9);
        var owner = Kernel(frame, fixture.Task);
        NativeCrtRandomState random = new(1);
        int node = owner.Lifecycle.Spawn(NativeBattleCuragaSpawnTests.Emitter, 1, owner.Bank.Read(NativeBattleCuragaExecutableState.Address + 0x104, 0x158), ref random);
        NativeBattleActionMatrix sphere = NativeBattleCuragaMatrices.Euler8818A0(new(41, 2083, 2238));
        NativeBattleVector expected = NativeBattleCuragaMatrices.RotateVector45DF80(NativeBattlePoseMatrices.Scale56BEF0(sphere, 8192, 2048, 4096), new(0, -4096, 0));
        Assert.Equal(unchecked((uint)(5 * expected.X << 4)), owner.Memory.Dword(node + 0x12C));
        Assert.Equal(unchecked((uint)(-7 * expected.Y << 4)), owner.Memory.Dword(node + 0x130));
        Assert.Equal(unchecked((uint)(9 * expected.Z << 4)), owner.Memory.Dword(node + 0x134));
    }

    private static (NativeBattleEffectNodeMemory Memory, NativeBattleEffectNodeBank Bank, NativeBattleEffectNodeLifecycle Lifecycle) Kernel(NativeBattleCuragaFrame frame, uint task)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, task);
        NativeBattleCuragaExecutableState executable = frame.Memory.CuragaExecutable!;
        NativeBattleEffectNodeBank bank = new(NativeBattleCuragaExecutableState.Address, Assert.IsType<NativeBattleNodeSource>(executable.Data), executable.Read);
        ushort cursor = 0;
        NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera, _ => throw new InvalidOperationException("No model selector is reached."), (_, _, _) => throw new InvalidOperationException("No new emitter is requested."), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, frame.State.Auxiliary16F80!.Value, 140, ref cursor, emitter, source)) { ActorScale = new(8192, 2048, 4096) };
        return (memory, bank, new(memory, bank, inputs));
    }
}
