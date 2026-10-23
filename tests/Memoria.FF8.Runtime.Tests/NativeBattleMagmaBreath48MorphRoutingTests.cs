using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48MorphRoutingTests
{
    [Fact]
    public void Opt_in_external_morph_runs_before_resource_update_without_reading_external_shapes_as_bank_offsets()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaSpawnStep spawned = NativeBattleCuragaNodeKinematics.Spawn8818A0(fixture.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 1, new(1));
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(spawned.Frame, source => source[0x16] = 5);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0x130, 0, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0x134, 1, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0x138, 2048);
        NativeBattleCuragaNodeMemory memory = new(frame, fixture.Task);
        memory.ContextDword(0x230, NativeBattleCuragaExecutableState.Address + 0xC4);
        byte[] bytes = frame.Memory.CuragaExecutable!.Data!.Value.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xC4), 0x40000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xC8), 0x40001000);
        NativeBattleEffectNodeBank bank = new(NativeBattleCuragaExecutableState.Address, bytes.ToImmutableArray(), (_, _) => throw new InvalidOperationException("External morph must use its current addressed store hook."));
        List<string> order = [];
        NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException())
        {
            MorphResource = (mesh, first, second, ratio) =>
            {
                Assert.Equal(bank.Address + 0x400, mesh);
                Assert.Equal(0x40000000u, first);
                Assert.Equal(0x40001000u, second);
                Assert.Equal((short)2048, ratio);
                order.Add("morph");
            },
            ResourceUpdate = (current, _, kind, _) =>
            {
                Assert.Equal(5, kind);
                Assert.Equal(new[] { "morph" }, order);
                order.Add("uv");
                return current.Data;
            }
        };
        new NativeBattleEffectNodeLifecycle(memory, bank, inputs).AdvanceDrawable(spawned.Node, bank.Read(bank.Address + 0x104, 0x158));
        Assert.Equal(new[] { "morph", "uv" }, order);
    }
}
