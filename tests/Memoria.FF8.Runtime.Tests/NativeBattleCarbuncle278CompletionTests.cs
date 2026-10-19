using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278CompletionTests
{
    [Theory]
    [InlineData(0, 255, true)]
    [InlineData(0, 0, false)]
    [InlineData(1, 255, false)]
    public void Root_waits_for_current_completion_and_pause_then_restores_once_before_release(uint pause, byte done, bool released)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleCarbuncle278Entry.MainDescriptor, NativeBattleCarbuncle278Entry.MainBank, 20, 1);
        (root, int index) = root.Append(NativeBattleCarbuncle278Entry.MainCallback);
        root = root with { Slots = root.Slots.SetItem(index, root.Slots[index].WriteWord(0xC, 280).WriteByte(0xF, 1)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root, Pool(0x2508188, 0x28A4, 0x5E0, 1), Pool(0x2508178, 0x23A4, 20, 64), Pool(0x2508168, 0x22C4, 28, 8), Pool(0x2508158, 0x1FA0, 0x10C, 3), Pool(0x2508138, 0x1C04, 28, 24), Pool(0x2508148, 0x1EA4, 28, 9), Pool(0x2508128, 4, 28, 256)];
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), new(0, 0, 3, 0, 0, 0), null) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() with { ModelBuffer81F8 = 0x20DFAB8, Pause8114 = pause, Done81D8 = done } };
        NativeBattleCarbuncle278Frame frame = new(memory, effect, NativeCrtRandomState.FromColdProcess());
        int restores = 0;
        int stageCalls = 0;
        NativeBattleCarbuncle278Inputs inputs = new(NativeBattleRetainedMemory.FromColdProcess().ActionPackets, (current, _, _) => current, current => new(current, 0)) { RestoreActors = current =>
        {
            restores++;
            return current;
        } };
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (depth, flags) =>
        {
            stageCalls++;
            Assert.Equal(0u, depth);
            Assert.Equal(0, flags);
        }, inputs);
        Assert.Equal(1, stageCalls);
        Assert.Equal(released ? 1 : 0, restores);
        Assert.Equal(released ? 0u : NativeBattleCarbuncle278Entry.MainDescriptor, result.Effect.ReturnedDescriptor);
        NativeBattleModulePoolTask task = result.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0];
        Assert.Equal(released ? 0 : 1, task.Flags & 1);
        Assert.Equal(released || pause != 0 ? (ushort)280 : (ushort)281, task.ReadWord(0xC));
        Assert.Equal((ushort)280, frame.Effect.Pools[0].Slots[0].ReadWord(0xC));

        static NativeBattleEffectTaskPool Pool(uint descriptor, int offset, ushort stride, ushort count) => NativeBattleEffectTaskPool.Initialize(descriptor, checked(0x20DFAB8u + (uint)offset), stride, count);
    }
}
