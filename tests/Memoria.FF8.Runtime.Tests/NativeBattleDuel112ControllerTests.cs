using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112ControllerTests
{
    [Fact]
    public void RootPublishesItsNewBanksAndRunsNewChildrenInTheSamePassInPhysicalOrder()
    {
        NativeBattleDuel112Frame frame = Fixture(1);
        List<int> visited = [];
        NativeBattleDuel112Frame result = NativeBattleDuel112Controller.Advance6CBCF0(frame, () => 0, (current, slot, sequence) =>
        {
            Assert.Equal(0, slot);
            Assert.Equal(0, sequence);
            Assert.Equal((byte)1, current.Effect.Pools[0].Slots[0].ReadByte(15));
            Assert.Equal(0x6CBF90u, current.Effect.Pools[1].Slots[slot].Callback);
            Assert.All(current.Effect.Pools[1].Slots[slot].Payload, value => Assert.Equal((byte)0, value));
            Assert.Equal(new uint[] { 0x2521960, 0x2521950, 0x2521940, 0x2521930, 0x2521920 }, current.Effect.Pools.Select(pool => pool.DescriptorAddress));
            return current;
        }, (current, index, pool, slot) =>
        {
            visited.Add(index);
            if (index == 1)
            {
                NativeBattleEffectTaskPool sprites = current.Effect.Pools[3];
                (sprites, _) = sprites.Append(0x6CD750);
                current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(3, sprites) } };
            }
            return new(current, pool, 0);
        }, current =>
        {
            Assert.Equal((byte)1, current.Effect.Pools[0].Slots[0].ReadByte(14));
            return current;
        });
        Assert.Equal(new[] { 1, 3 }, visited);
        Assert.Equal(0x11E88, result.Effect.DrawArenaOffset);
        Assert.Equal((ushort)2, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((byte)1, result.Effect.Pools[0].Slots[0].ReadByte(14));
        Assert.Equal((uint)(0x20DFAB8 + 0x11E88), BitConverter.ToUInt32(result.Staging.Read(0x2521914, 4)));
    }

    [Fact]
    public void RootCompletesOnlyAfterInitializedChildrenAreConsumedAndDoesNotIncrementOnRelease()
    {
        NativeBattleDuel112Frame frame = Fixture(1);
        NativeBattleDuel112Frame Run(NativeBattleDuel112Frame current) => NativeBattleDuel112Controller.Advance6CBCF0(current, () => 0, (entered, _, _) => entered, (entered, _, pool, _) => new(entered, pool, 2), entered => entered);
        NativeBattleDuel112Frame restored = JsonSerializer.Deserialize<NativeBattleDuel112Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleDuel112Frame result = Run(frame);
        Assert.Null(result.Effect.Pools[0].Head);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Single(result.Effect.Pools);
        result.Effect.Validate();
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(restored)));
    }

    [Fact]
    public void ColdFirstTickDoesNotReadTheHeaderOrCompleteBeforeChildrenInitialization()
    {
        NativeBattleDuel112Frame frame = Fixture(0);
        NativeBattleDuel112Frame result = NativeBattleDuel112Controller.Advance6CBCF0(frame, () => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.Equal(0, result.Effect.Pools[0].Head);
        Assert.Single(result.Effect.Pools);
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((uint)1, result.Effect.Pools[0].Slots[0].ReadDword(16));
    }

    private static NativeBattleDuel112Frame Fixture(ushort counter)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        (root, _) = root.Append(0x6CBCF0);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, counter)) };
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root] };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
    }
}
