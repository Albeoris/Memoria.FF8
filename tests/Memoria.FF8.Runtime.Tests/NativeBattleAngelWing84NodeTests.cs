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
}
