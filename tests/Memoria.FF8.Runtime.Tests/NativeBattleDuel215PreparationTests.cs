using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215PreparationTests
{
    [Fact]
    public void PreparationKeepsRawBackupWordsUnknownPaddingAndTheActualTaskResourceRow()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel215Controller.Scene, NativeBattleDuel215Controller.Arena, 5120, 1);
        (pool, int slot) = pool.Append(0x69B070);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[5108])) });
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Duel215 = new(true, parameters, null) };
        NativeBattleOriginalEffectState effect = new(215, 214, new(215, 0x69AB60, 0x69AB70), parameters, NativeBattleDuel215Controller.Root) { Pools = [pool] };
        NativeBattleDuel215Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown().Write(0x2508990, new byte[8]), NativeCrtRandomState.FromColdProcess());
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2)
        {
            Transform = new(new(0, 0, 0), new(10, 20, 30), 40, 0, 100),
            Rotation12 = 11,
            Rotation14 = 22,
            Rotation16 = 33,
            GeometryBounds52 = new(new(-20, -30, -40), new(20, 50, 40)),
            MinimumY146 = 100
        }).ToArray();
        uint boundRow = 0;
        byte sequence = 0;
        NativeBattleDuel215PreparationServices services = new(index => models[index], (index, model) => models[index] = model, index => new((short)(index == 3 ? -100 : 300), 0, (short)(index == 3 ? 500 : 900), 0), ImmutableArray.Create<byte>(3, 4), (current, actor, row) =>
        {
            Assert.Equal(0, actor);
            boundRow = row;
            return current;
        }, (current, actor, value) =>
        {
            Assert.Equal(0, actor);
            sequence = value;
            return current;
        });
        NativeBattleDuel215Frame result = NativeBattleDuel215Preparation.Initialize69AC00(frame, slot, services);
        NativeBattleModulePoolTask task = result.Effect.Pools[0].Slots[slot];
        Assert.Equal(NativeBattleDuel215Controller.Arena + 16, boundRow);
        Assert.Equal(1, sequence);
        Assert.Equal(100, task.ReadWord(384));
        Assert.Equal(900, task.ReadWord(388));
        Assert.Equal(10u | 20u << 16, task.ReadDword(48));
        Assert.Equal(30u | 40u << 16, task.ReadDword(52));
        Assert.Equal(0x1D972C0u + 3u * 156, task.ReadDword(64));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(82));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(62));
        Assert.Equal(new NativeBattleVector(100, 20, 4996), models[0].Transform!.Position28);
        Assert.Equal((short?)0, models[0].Rotation14);
        Assert.Equal((ushort)2, models[0].Flags0);
        Assert.Equal((ushort)6, models[1].Flags0);
        Assert.Equal((ushort)6, models[2].Flags0);
        Assert.Equal(NativeBattleDuel215Controller.Arena + 2300, task.ReadDword(5116));
        models[0] = models[0] with { Flags0 = (ushort)(models[0].Flags0 | 8), RotationPadding18 = 0xFFFF };
        models[3] = models[3] with { Rotation12 = -100, RotationPadding18 = 0xFFFF };
        result = NativeBattleDuel215Preparation.RestoreModels69AC00(result, services);
        Assert.Equal(2, sequence);
        Assert.Equal(new NativeBattleVector(10, 20, 30), models[0].Transform!.Position28);
        Assert.Equal(40, models[0].Transform!.Vertical34);
        Assert.Equal((short?)22, models[0].Rotation14);
        Assert.Equal((ushort)2, models[0].Flags0);
        Assert.Null(models[0].RotationPadding18);
        Assert.Equal((short?)11, models[3].Rotation12);
        Assert.Null(models[3].RotationPadding18);
    }
}
