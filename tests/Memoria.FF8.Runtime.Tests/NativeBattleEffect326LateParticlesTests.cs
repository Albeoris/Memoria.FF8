using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect326LateParticlesTests
{
    [Fact]
    public void HistoryBurstConsumesOneCrtDrawAndPreservesUnwrittenTaskWords()
    {
        NativeBattleEffect326Frame frame = CreateFrame();
        NativeBattleModulePoolTask retained = NativeBattleEffect326Controller.Read(frame, ChildBank).WriteWord(14, 0xA55A).WriteWord(0x1A, 0x1234).WriteWord(0x1E, 0x6789).WriteWord(0x22, 0xFEDC);
        frame = NativeBattleEffect326Controller.Write(frame, ChildBank, retained);
        NativeCrtRandomState expectedRandom = frame.Random.Draw(out ushort random);

        NativeBattleEffect326Frame result = NativeBattleEffect326Controller.InitializeHistoryBurst630A70(frame);

        NativeBattleModulePoolTask task = NativeBattleEffect326Controller.Read(result, ChildBank);
        Assert.Equal(expectedRandom, result.Random);
        Assert.Equal(0x630AB0u, task.Callback);
        Assert.Equal((ushort)0, task.ReadWord(12));
        Assert.Equal((ushort)(random & 4095), task.ReadWord(0x18));
        Assert.Equal((ushort)0x600, task.ReadWord(0x1C));
        Assert.Equal((ushort)0xA00, task.ReadWord(0x20));
        Assert.Equal((ushort)0xA55A, task.ReadWord(14));
        Assert.Equal((ushort)0x1234, task.ReadWord(0x1A));
        Assert.Equal((ushort)0x6789, task.ReadWord(0x1E));
        Assert.Equal((ushort)0xFEDC, task.ReadWord(0x22));
        Assert.Equal(1, result.Effect.Pools[1].Slots.Count(slot => (slot.Flags & 1) != 0));
    }

    [Fact]
    public void RibbonClearsOnlyTheTenHistoryHeadsWithoutConsumingRandom()
    {
        NativeBattleEffect326Frame frame = CreateFrame();
        for (uint row = 0x24F2180; row < 0x24F2248; row += 20)
        {
            frame = frame with { State = frame.State.WriteDword(row, 0xFFFFFFFF).WriteWord(row + 4, 0x5678).WriteWord(row + 18, 0xABCD) };
        }

        NativeBattleEffect326Frame result = NativeBattleEffect326Controller.InitializeRibbon630400(frame);

        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(0x630430u, NativeBattleEffect326Controller.Read(result, ChildBank).Callback);
        for (uint row = 0x24F2180; row < 0x24F2248; row += 20)
        {
            Assert.Equal(0u, result.State.ReadDword(row));
            Assert.Equal((ushort)0x5678, result.State.ReadWord(row + 4));
            Assert.Equal((ushort)0xABCD, result.State.ReadWord(row + 18));
        }
    }

    [Fact]
    public void MandatoryLateBirthFailsWhenItsPhysicalChildBankIsFull()
    {
        NativeBattleEffect326Frame frame = CreateFrame();
        for (int index = 0; index < 100; index++)
        {
            frame = NativeBattleEffect326Controller.InitializeHistoryBurst630A70(frame);
        }

        Assert.Throws<InvalidDataException>(() => NativeBattleEffect326Controller.InitializeHistoryBurst630A70(frame));
        Assert.Equal(100, frame.Effect.Pools[1].Slots.Count(slot => (slot.Flags & 1) != 0));
    }

    private static NativeBattleEffect326Frame CreateFrame()
    {
        NativeBattleOriginalEffectState effect = new(Effect, Effect - 1, new(Effect, Entry, Initializer), null, 0)
        {
            Pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray()
        };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect326State.FromColdProcess());
    }
}
