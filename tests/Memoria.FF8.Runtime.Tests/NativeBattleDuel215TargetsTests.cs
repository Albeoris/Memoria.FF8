using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215TargetsTests
{
    [Fact]
    public void TargetBodyUsesMaximumHeightAndRotatesAfterDrawingWithAlternatingSignedSteps()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel215Controller.Scene, NativeBattleDuel215Controller.Arena, 5120, 1);
        (pool, int slot) = pool.Append(0x69B070);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteBytes(12, new byte[5108]);
        for (int index = 0; index < 2; index++)
        {
            int offset = 64 + index * 32;
            task = task.WriteDword(offset, checked(0x1D972C0u + (uint)(index + 3) * 156)).WriteWord(offset + 6, 20).WriteWord(offset + 28, 100).WriteWord(offset + 30, unchecked((ushort)-100));
        }
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleOriginalEffectState effect = new(215, 214, new(215, 0x69AB60, 0x69AB70), new(0, 0, 24, 0, 0, 0), NativeBattleDuel215Controller.Root) { Pools = [pool] };
        NativeBattleDuel215Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2) { Transform = new(new(0, 0, 0), new(10, 20, 30), 0, 0, 4096) }).ToArray();
        List<int> drawn = [];
        NativeBattleDuel215TargetServices services = new(index => models[index], (index, model) => models[index] = model, ImmutableArray.Create<byte>(3, 4), (_, _) => throw new InvalidOperationException("Existing target bounds must be retained through this phase."), (current, index, angles, pivot) =>
        {
            drawn.Add(index);
            Assert.Equal(new NativeBattleVector(0, 0, 0), angles);
            Assert.Equal(new NativeBattleVector(0, 0, 0), pivot);
            Assert.Equal((short)-6893, models[index].Transform!.Position28.Y);
            Assert.Equal((ushort)2, models[index].Flags0);
            return current;
        });
        NativeBattleDuel215Frame result = NativeBattleDuel215Targets.Advance69B070(frame, slot, 232, services);
        Assert.Equal(new[] { 3, 4 }, drawn);
        Assert.Equal((ushort)6, models[3].Flags0);
        Assert.Equal((ushort)6, models[4].Flags0);
        task = result.Effect.Pools[0].Slots[slot];
        Assert.Equal(unchecked((ushort)-204), task.ReadWord(84));
        Assert.Equal((ushort)204, task.ReadWord(116));
    }
}
