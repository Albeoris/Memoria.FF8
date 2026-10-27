using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlParticleCallbacksTests
{
    [Fact]
    public void EightSubdivisionsGrowNineRowsAndConsumeTheBranchComparisonDrawEvenWithZeroBudget()
    {
        var (memory, effect) = Fixture(0, 0);
        NativeBattleQuezacotlParticleStep step = Advance(memory, effect);
        Assert.Equal((short)2048, Read16(step.Memory, 0x3C27C + 0x1E));
        Assert.Equal((short)9, Read16(step.Memory, 0x3C27C + 0xE));
        Assert.Equal((ushort)21425, step.Effect.Quezacotl!.Random17A0);
        Assert.Single(step.Pool.Slots.Where(slot => (slot.Flags & 1) != 0));
        Assert.Equal((short)1280, Read16(step.Memory, NativeBattleQuezacotlParticleProducer.Rows + 0x20));
        Assert.Empty(step.Ordering.Packets);
        Assert.Equal(0, step.Returned);
    }

    [Fact]
    public void PauseStillProjectsTheCurrentRowsWithoutReadingUnknownVelocityOrConsumingPrivateRandom()
    {
        var (memory, effect) = Fixture(0, 0);
        int row = NativeBattleQuezacotlParticleProducer.Rows;
        memory = memory with { QuezacotlPause = 1, Arena = memory.Arena!.MarkUnknown(row + 8, 6).MarkUnknown(row + 0x10, 8) };
        NativeBattleQuezacotlParticleStep step = Advance(memory, effect);
        Assert.Equal((ushort)1, step.Effect.Quezacotl!.Random17A0);
        Assert.Equal((short)0, Read16(step.Memory, 0x3C27C + 0x1E));
        Assert.Equal((short)512, Read16(step.Memory, row + 0x20));
        Assert.Equal(new byte[4], step.Memory.Arena!.Read(row + 0x14, 4));
        Assert.Throws<NotSupportedException>(() => step.Memory.Arena.Read(row + 8, 6));
    }

    [Fact]
    public void Short_paused_chain_preserves_prior_full_depth_registers_and_pushes_its_actual_projection()
    {
        var (memory, effect) = Fixture(0, 0);
        memory = memory with { QuezacotlPause = 1 };
        NativeBattleGteDepthTrace trace = new([111, 222, 333, 444]);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, -10000, 4096);
        NativeBattleQuezacotlParticleCallbacks.Advance6C7EA0(memory, effect, effect.Pools[0], 0, NativeBattleLogicalOrdering.Clear(), new(view, 216, 160, 108), trace);
        Assert.Equal(new int?[] { 222, 333, 444, 0 }, trace.DepthFifo4);
    }

    [Fact]
    public void CompletionOnlyReadsTheImpactEnableOwnerWhenTheActualModeRequiresIt()
    {
        var (memory, effect) = Fixture(3840, 0);
        memory = memory with { Arena = memory.Arena!.MarkUnknown(0x28E50, 4) };
        NativeBattleQuezacotlParticleStep modeZero = Advance(memory, effect);
        Assert.Equal((short)4096, Read16(modeZero.Memory, 0x3C27C + 0x1E));
        (memory, effect) = Fixture(3840, 1);
        NativeBattleQuezacotlParticleStep disabledImpact = Advance(NativeBattleTestSnapshots.Independent(memory), NativeBattleTestSnapshots.Independent(effect));
        Assert.Equal((short)4096, Read16(disabledImpact.Memory, 0x3C27C + 0x1E));
        memory = memory with { Arena = memory.Arena!.Write(0x28E50, [1, 0, 0, 0]) };
        Assert.Contains("6C8430", Assert.Throws<NotSupportedException>(() => Advance(memory, effect)).Message);
    }

    [Fact]
    public void Flight88BuildsItsActualModeZeroArgumentsBeforeTheThreeNullVelocityDraws()
    {
        var (memory, effect) = Fixture(0, 0);
        NativeBattleQuezacotlParticleSpawn spawned = NativeBattleQuezacotlParticleFlight.Spawn6C4043(memory, effect);
        Assert.Equal(1, spawned.Slot);
        Assert.Equal((ushort)11383, spawned.Effect.Quezacotl!.Random17A0);
        int task = 0x3C27C + 60;
        Assert.Equal(new short[] { -373, 0, 493 }, Vector(task + 0x18));
        Assert.Equal(new short[] { -313, -2873, 208 }, Vector(task + 0x20));
        Assert.Equal(new short[] { 37, -95, 49 }, Vector(task + 0x28));
        Assert.Equal(new short[] { 59, -35, -9 }, Vector(task + 0x30));
        Assert.Equal((short)0, Read16(spawned.Memory, task + 0x3A));
        Assert.Equal((short)3, Read16(spawned.Memory, task + 0x2E));

        short[] Vector(int offset) => [Read16(spawned.Memory, offset), Read16(spawned.Memory, offset + 2), Read16(spawned.Memory, offset + 4)];
    }
    [Fact]
    public void AnchorCallerUsesModeZeroBudgetThreeAndExactlyTenPrivateDraws()
    {
        var (memory, effect) = Fixture(0, 0);
        NativeBattleQuezacotlResources resources = effect.Binding.NativeQuezacotlResources! with { AnchorVertexIndices11 = [93, 33, 696, 562, 578, 450, 9, 240, 388, 408, 265] };
        ushort random = effect.Quezacotl!.Random17A0;
        Assert.Equal((short)93, NativeBattleQuezacotlParticleFlight.SelectVertex12AFE40(resources, ref random));
        effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
        NativeBattleQuezacotlParticleSpawn spawned = NativeBattleQuezacotlParticleFlight.SpawnFromAnchor6C41E2(memory, effect, new(100, 200, 300));
        Assert.Equal((ushort)11101, spawned.Effect.Quezacotl!.Random17A0);
        int task = 0x3C27C + 60;
        Assert.Equal(new short[] { 100, 200, 300 }, Vector(task + 0x18));
        Assert.Equal(new short[] { 81, -1535, 101 }, Vector(task + 0x20));
        Assert.Equal(new short[] { 35, -101, 31 }, Vector(task + 0x28));
        Assert.Equal(new short[] { 113, 59, -35 }, Vector(task + 0x30));
        Assert.Equal((short)0, Read16(spawned.Memory, task + 0x3A));
        Assert.Equal((short)3, Read16(spawned.Memory, task + 0x2E));
        Assert.Equal((short)0, Read16(spawned.Memory, task + 0x1E));
        Assert.Equal((short)256, Read16(spawned.Memory, task + 0x26));
        Assert.Equal((short)250, Read16(spawned.Memory, task + 0x38));

        short[] Vector(int offset) => [Read16(spawned.Memory, offset), Read16(spawned.Memory, offset + 2), Read16(spawned.Memory, offset + 4)];
    }
    private static NativeBattleQuezacotlParticleStep Advance(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, -10000, 4096);
        return NativeBattleQuezacotlParticleCallbacks.Advance6C7EA0(memory, effect, effect.Pools[0], 0, NativeBattleLogicalOrdering.Clear(), new(view, 216, 160, 108));
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Fixture(short progress, short mode)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        for (int index = 0; index < 256; index++)
        {
            memory = memory with { Arena = memory.Arena!.Write(NativeBattleQuezacotlParticleProducer.Rows + index * 40 + 6, [255, 255]) };
        }
        NativeBattleQuezacotlRibbonResources ribbon = new([], [], [], Enumerable.Repeat((byte)1, 16384).ToImmutableArray()) { ParticleScaleWords4096 = Enumerable.Repeat((short)4096, 4096).ToImmutableArray() };
        NativeBattleQuezacotlResources resources = new(null!, null!, null!, null!) { Ribbon = ribbon };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521718, 0x211BD34, 60, 32);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560) { NativeQuezacotlResources = resources }, null, null) { Pools = [pool], Quezacotl = new(1, 0, 3690), DrawArenaOffset = 0x50000 };
        NativeBattleQuezacotlParticleSpawn spawned = NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, new(new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), 0, progress, 256, 250, mode, 0));
        return (spawned.Memory, spawned.Effect);
    }

    private static short Read16(NativeBattleEffectMemory memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Arena!.Read(offset, 2));
}
