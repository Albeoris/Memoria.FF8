using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NewGamePlusDreamMagicTests
{
    [Fact]
    public void PersonalDreamStockAndOriginalExtensionRetainTheirIdentityAcrossEarningsSaveAndSelection()
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals dream = source.PrepareJunction(1, [0, 1, 2], 7).Vitals;
        Assert.Equal(10, dream.GetMagicCount(0, 2));
        Assert.Equal(0, dream.GetMagicCount(0, 1));
        Assert.Equal(0, dream.GetMagicCount(0, 40));
        Assert.Equal(0, dream.Records[8].MagicInventory.Sum(stock => stock.Count));
        dream = dream.ChangeMagicStock(0, 2, 5, MagicOperationReason.DrawStock, out _).ChangeMagicStock(0, 2, -3, MagicOperationReason.OrdinaryCast, out _);
        dream.MagicSystem.PersonalFieldMasks[0][2] = 3;
        GameplaySaveCharacters save = JsonSerializer.Deserialize<GameplaySaveCharacters>(JsonSerializer.Serialize(GameplaySaveCharacters.Capture(dream)))!;
        dream = save.Restore(MagicSystemFixture.Create());
        Assert.Equal(12, dream.GetMagicCount(0, 2));
        NativeCharacterVitals selection = dream.EnterGuestSelectionMenu();
        Assert.Equal(20, selection.GetMagicCount(0, 1));
        Assert.Equal(9, selection.GetMagicCount(0, 40));
        Assert.Equal(12, selection.GetMagicCount(8, 2));
        Assert.Equal(3, selection.MagicSystem.PersonalFieldMasks[8][2]);
        NativeCharacterVitals selected = selection.CompleteGuestSelectionMenu([0, 1, 2], 7).Vitals;
        Assert.Equal(12, selected.GetMagicCount(0, 2));
        NativeCharacterVitals ended = selected.PrepareJunction(0, [8, 9, 10], 7).Vitals;
        Assert.Equal(20, ended.GetMagicCount(0, 1));
        Assert.Equal(9, ended.GetMagicCount(0, 40));
        Assert.Equal(12, ended.GetMagicCount(8, 2));
        Assert.Equal(12, ended.PrepareJunction(1, [0, 1, 2], 7).Vitals.GetMagicCount(0, 2));
    }

    [Fact]
    public void SharedPoolAndRetainedDonorsAreNotCopiedByDreamTransitions()
    {
        NativeCharacterVitals shared = MagicModeCoordinator.Preview(Source(), new() { SharedMagicStock = true }).Vitals;
        string before = JsonSerializer.Serialize(shared.Records.Select(record => record.MagicInventory));
        NativeCharacterVitals dream = shared.PrepareJunction(1, [0, 1, 2], 7).Vitals;
        Assert.Equal(before, JsonSerializer.Serialize(dream.Records.Select(record => record.MagicInventory)));
        dream = dream.ChangeMagicStock(0, 2, -3, MagicOperationReason.OrdinaryCast, out _);
        NativeCharacterVitals ended = dream.PrepareJunction(0, [8, 9, 10], 7).Vitals;
        Assert.Equal(7, ended.GetMagicCount(0, 2));
        Assert.Equal(20, ended.GetMagicCount(0, 1));
        Assert.Equal(9, ended.GetMagicCount(0, 40));
    }

    [Fact]
    public void ModeChangesDuringDreamCanonicalizeEachBankOnce()
    {
        NativeCharacterVitals dream = Source().PrepareJunction(1, [0, 1, 2], 7).Vitals;
        NativeCharacterVitals shared = MagicModeCoordinator.Preview(dream, new() { SharedMagicStock = true }).Vitals;
        Assert.Null(shared.NewGamePlusDreamMagicBackup);
        Assert.Equal(20, shared.GetMagicCount(0, 1));
        Assert.Equal(10, shared.GetMagicCount(0, 2));
        Assert.Equal(9, shared.GetMagicCount(0, 40));
        NativeCharacterVitals personal = MagicModeCoordinator.Preview(shared, new() { SharedMagicStock = false }).Vitals;
        Assert.NotNull(personal.NewGamePlusDreamMagicBackup);
        NativeCharacterVitals ended = personal.PrepareJunction(0, [8, 9, 10], 7).Vitals;
        Assert.Equal(20, ended.GetMagicCount(0, 1));
        Assert.Equal(10, ended.GetMagicCount(0, 2));
        Assert.Equal(9, ended.GetMagicCount(0, 40));
        Assert.Equal(39, ended.Records.Sum(record => record.MagicInventory.Sum(stock => stock.Count)) + ended.MagicSystem.RecordZeroExtension.Sum(stock => stock.Count));
    }

    private static NativeCharacterVitals Source()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create().WithGuestBattleRecords() with { NewGamePlusPermanentGuests = 0 };
        source = source.ChangeMagicStock(0, 1, 20, MagicOperationReason.ScriptReward, out _).ChangeMagicStock(8, 2, 10, MagicOperationReason.ScriptReward, out _);
        source = source with { MagicSystem = source.MagicSystem with { RecordZeroExtension = [new(40, 9)] } };
        source.Records[1] = source.Records[1] with { MemberFlags = 9 };
        source.Records[2] = source.Records[2] with { MemberFlags = 9 };
        return source;
    }
}
