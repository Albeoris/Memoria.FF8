using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFireEffectTests
{
    [Fact]
    public void InitializerCopiesItsActualAssetIntoTheClearedArenaAndOwnsItsGlobalPointer()
    {
        NativeBattleEffectBinding binding = new(2, 0x6298A0, 0x629880) { InitializerAsset = new("mag001.tim", [4, 5, 6]) };
        NativeBattleOriginalEffectState selected = new(2, 1, binding, null, null);
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        Assert.Equal(3, memory.Cursor);
        Assert.Equal(new byte[] { 4, 5, 6, 0 }, memory.Arena!.Read(0, 4));
        Assert.Equal(new NativeBattleEffectResourcePointer(0x24CBD68, 0), Assert.Single(effect.ResourcePointers));
        Assert.Null(effect.ReturnedDescriptor);
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(memory, selected with { Binding = binding with { InitializerAsset = null } }));
    }

    [Theory]
    [InlineData(1, 0x62A380u)]
    [InlineData(2, 0x629990u)]
    public void EntryPoolSetupUsesItsPhysicalDimensionsAndHeaderTargetCount(int targets, uint callback)
    {
        (NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets) = Fixture(targets);
        effect = NativeBattleFireEffect.InitializePools(effect, packets);
        effect.Validate();
        Assert.Equal((uint?)0x24CA828, effect.ReturnedDescriptor);
        Assert.Single(effect.Pools[0].Slots);
        Assert.Equal((ushort)16, effect.Pools[0].Stride);
        Assert.Equal(150, effect.Pools[1].Slots.Length);
        Assert.Equal((ushort)36, effect.Pools[1].Stride);
        Assert.Equal(callback, effect.Pools[1].Slots[0].Callback);
        Assert.Equal(targets == 1 ? (ushort)4 : (ushort)0, effect.Pools[1].Slots[0].ReadWord(32));
        Assert.All(effect.Pools[1].Slots[0].Payload.Take(20), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void MainCallbackWaitsForRealChildReleaseAcrossRestoreAndWrapsItsWordCounter()
    {
        (NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets) = Fixture(1);
        effect = NativeBattleFireEffect.InitializePools(effect, packets);
        NativeBattleEffectTaskPool main = effect.Pools[0];
        effect = effect with { Pools = effect.Pools.SetItem(0, main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, ushort.MaxValue)) }) };
        int calls = 0;
        effect = NativeBattleFireEffect.Advance(effect, (pool, slot, buffer) =>
        {
            calls++;
            Assert.Equal(0x24D5D6Cu, buffer);
            Assert.Equal(0x62A380u, pool.Slots[slot].Callback);
            return new(pool, 0);
        });
        Assert.Equal(1, calls);
        Assert.Equal((ushort)0, effect.Pools[0].Slots[0].ReadWord(12));
        Assert.NotEqual((uint?)0, effect.ReturnedDescriptor);
        string json = JsonSerializer.Serialize(effect);
        effect = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        effect.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(effect));
        effect = NativeBattleFireEffect.Advance(effect, (pool, slot, buffer) =>
        {
            calls++;
            Assert.Equal(0x24CBD6Cu, buffer);
            return new(pool, 2);
        });
        Assert.Equal(2, calls);
        Assert.Equal((uint?)0, effect.ReturnedDescriptor);
        Assert.Equal((ushort)1, effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, effect.Pools[0].Slots[0].Flags);
        Assert.Equal((ushort)0, effect.Pools[1].Slots[0].Flags);
        NativeBattleOriginalEffectState completed = effect;
        Assert.Same(completed, NativeBattleFireEffect.Advance(completed, (_, _, _) => throw new InvalidOperationException("Completed descriptors must not run.")));
    }

    private static (NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets) Fixture(int targets)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(3, 2, 11, -1, 1, 2, null));
        for (int index = 0; index < targets; index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)(4 + index), 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish((byte)targets);
        return (new(2, 1, new(2, 0x6298A0, 0x629880), new(3, 0, 0x30, 0, 0, 0), null), packets);
    }
}
