using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215ControllerTests
{
    [Fact]
    public void RootFirstPhaseUsesSecondPrivateBankBeforeCreatingItsScene()
    {
        NativeBattleDuel215Frame frame = Frame(0);
        NativeBattleDuel215Frame step = NativeBattleDuel215Controller.Advance69AC00(frame, new((_, _) => throw new InvalidOperationException("Phase zero has no scene services.")), (_, _, _) => throw new InvalidOperationException("Phase zero has no child callbacks."));
        Assert.Equal(140568, step.Effect.DrawArenaOffset);
        Assert.Equal(0x20DFAB8u + 140568u, U32(step.Staging, 0x2508988));
        Assert.Equal(0x20DFAB8u + 214296u, U32(step.Staging, 0x250898C));
        NativeBattleModulePoolTask task = Assert.Single(step.Effect.Pools).Slots[0];
        Assert.Equal(1, task.ReadWord(12));
        Assert.Equal(1, task.ReadByte(15));
        Assert.Equal(0x2508968u, step.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void SceneBirthUsesExact5120And660BytePoolsAndClears1277Dwords()
    {
        NativeBattleDuel215Frame frame = Frame(1);
        int prepared = 0;
        int children = 0;
        NativeBattleDuel215Inputs inputs = new((current, operation) =>
        {
            Assert.Equal(0, Assert.IsType<NativeBattleDuel215PrepareScene>(operation).Slot);
            prepared++;
            return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, 28880) };
        });
        NativeBattleDuel215Frame step = NativeBattleDuel215Controller.Advance69AC00(frame, inputs, (current, pool, slot) =>
        {
            Assert.Equal(0x2508958u, pool.DescriptorAddress);
            Assert.Equal(0x20DFAB8u, pool.BankAddress);
            Assert.Equal(5120, pool.Stride);
            Assert.Equal(0x69B070u, pool.Slots[slot].Callback);
            Assert.Equal(5108, pool.Slots[slot].Payload.Length);
            Assert.All(pool.Slots[slot].Payload, value => Assert.Equal((byte)0, value));
            children++;
            return new(current, pool, 0);
        });
        Assert.Equal(1, prepared);
        Assert.Equal(1, children);
        Assert.Equal(3, step.Effect.Pools.Length);
        NativeBattleEffectTaskPool particles = step.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508948);
        Assert.Equal(0x20DFAB8u + 5120u, particles.BankAddress);
        Assert.Equal(660, particles.Stride);
        Assert.Equal(36, particles.Slots.Length);
        Assert.All(step.Effect.Pools, pool => pool.Validate());
    }

    [Fact]
    public void SignedCounterSaturationRetainsTheOriginal256Then257Oscillation()
    {
        NativeBattleDuel215Frame first = NativeBattleDuel215Controller.Advance69AC00(Frame(256), new((_, _) => throw new InvalidOperationException()), (_, _, _) => throw new InvalidOperationException());
        Assert.Equal(257, first.Effect.Pools[0].Slots[0].ReadWord(12));
        NativeBattleDuel215Frame second = NativeBattleDuel215Controller.Advance69AC00(first, new((_, _) => throw new InvalidOperationException()), (_, _, _) => throw new InvalidOperationException());
        Assert.Equal(256, second.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(66840, second.Effect.DrawArenaOffset);
    }

    private static NativeBattleDuel215Frame Frame(ushort counter)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2508968, 0x2508978, 16, 1);
        (root, int slot) = root.Append(0x69AC00);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, counter)) };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = new([]) };
        NativeBattleOriginalEffectState effect = new(215, 214, new(215, 0x69AB60, 0x69AB70), new(0, 0, 8, 0, 0, 0), 0x2508968) { Pools = [root] };
        return new(memory, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
    }

    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
