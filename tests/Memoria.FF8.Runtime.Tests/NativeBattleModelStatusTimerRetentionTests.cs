using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelStatusTimerRetentionTests
{
    [Fact]
    public void ColdTimerRowsSeedPhysicalModelsAndModelLoadPreservesAliasCounters()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        ImmutableArray<ImmutableArray<byte?>> rows = Assert.IsType<ImmutableArray<ImmutableArray<byte?>>>(cold.ModelStatusTimers);
        Assert.Equal(7, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.Equal(32, row.Length);
            Assert.All(row, timer => Assert.Equal((byte)0, timer));
        });
        rows = rows.SetItem(3, rows[3].SetItem(18, 42).SetItem(8, 99));
        NativeBattleGraphicsModel model = NativeBattleModelBoundary.FromRetainedMemory(cold with { ModelStatusTimers = rows }).ReadOriginalCell(NativeBattleModelSlot.Decode(3), new BattleActorModels([]));
        NativeGlobalRandomState random = new(0);
        model = NativeBattleGraphicsModels.CompleteLoad(model, 28, 0x100, isParty: false, ref random, 0);
        Assert.Equal((byte)0, model.StatusTimers[8]);
        Assert.Equal((byte)42, model.StatusTimers[18]);
        Assert.Equal((byte)0, model.StatusTimers[17]);
    }

    [Fact]
    public void MissingHistoricalTimerOwnerRemainsUnknownAcrossJsonAndModelLoad()
    {
        NativeBattleRetainedMemory old = NativeBattleRetainedMemory.FromColdProcess() with { ModelStatusTimers = null };
        string json = JsonSerializer.Serialize(old);
        Assert.DoesNotContain("ModelStatusTimers", json);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json)!;
        Assert.Null(restored.ModelStatusTimers);
        NativeBattleGraphicsModel model = NativeBattleModelBoundary.FromRetainedMemory(restored).ReadOriginalCell(NativeBattleModelSlot.Decode(0), new BattleActorModels([]));
        NativeGlobalRandomState random = new(0);
        model = NativeBattleGraphicsModels.CompleteLoad(model, 0, 0x100, isParty: true, ref random);
        Assert.Equal((byte)0, model.StatusTimers[8]);
        Assert.Null(model.StatusTimers[18]);
    }

    [Fact]
    public void BankEntryPreservesDistinctRowsAndRejectsMalformedRetainedStorage()
    {
        ImmutableArray<ImmutableArray<byte?>> rows = Enumerable.Range(0, 7).Select(actor => Enumerable.Repeat<byte?>((byte)(10 + actor), 32).ToImmutableArray()).ToImmutableArray();
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { ModelStatusTimers = rows };
        var models = NativeBattleModelBoundary.FromRetainedMemory(retained).CaptureOriginalCells(new BattleActorModels([]));
        for (int actor = 0; actor < 7; actor++)
        {
            Assert.Equal((ushort)0, models[actor].Flags0);
            Assert.All(models[actor].StatusTimers, timer => Assert.Equal((byte)(10 + actor), timer));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleModelBoundary.FromRetainedMemory(retained with { ModelStatusTimers = rows.RemoveAt(0) }));
        Assert.Throws<InvalidDataException>(() => (NativeBattleRetainedMemory.FromColdProcess() with { ModelStatusTimers = rows.SetItem(0, []) }).Validate());
    }
}
