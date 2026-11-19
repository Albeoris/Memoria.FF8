using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusMagicDraftTests
{
    [Fact]
    public void RemovingRetainedCopyLeavesTheVisiblePoolAndOtherOwnersIntact()
    {
        NewGamePlusDraft draft = SharedDraft();
        int pool = draft.Magic.GetMagicCount(0, 1);
        int other = draft.Magic.Records[2].MagicInventory.Single(stock => stock.SpellId == 1).Count;
        byte owner = draft.Magic.Records[1].CharacterId;
        draft.SetRetainedMagic(owner, 1, 0);
        Assert.Equal(pool, draft.Magic.GetMagicCount(0, 1));
        Assert.DoesNotContain(draft.Magic.Records[1].MagicInventory, stock => stock.SpellId == 1 && stock.Count > 0);
        Assert.Equal(other, draft.Magic.Records[2].MagicInventory.Single(stock => stock.SpellId == 1).Count);
        Assert.Equal(NewGamePlusPreset.Custom, draft.MagicPreset);
    }

    [Fact]
    public void RemovingFromEntireCollectionCannotResurrectStockOnModeChanges()
    {
        NewGamePlusDraft draft = SharedDraft();
        draft.RemoveMagicEverywhere(1);
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = false });
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = true });
        Assert.Equal(0, draft.Magic.GetMagicCount(0, 1));
        Assert.All(draft.Magic.Records, record => Assert.DoesNotContain(record.MagicInventory, stock => stock.SpellId == 1 && stock.Count > 0));
        Assert.DoesNotContain(draft.Magic.MagicSystem.RecordZeroExtension, stock => stock.SpellId == 1);
        Assert.Empty(draft.MagicOverflow);
    }

    [Fact]
    public void RevalidatingSameSharedModeRetainsTheOverflowDisclosure()
    {
        NewGamePlusDraft draft = SharedDraft();
        MagicStockMove[] before = draft.MagicOverflow;
        Assert.NotEmpty(before);
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = true });
        Assert.Equal(before, draft.MagicOverflow);
        Assert.Equal(before, draft.ResolveDefinition().InitialMagicOverflow);
    }

    private static NewGamePlusDraft SharedDraft()
    {
        GameState source = CompletedSaveFixture.State();
        NewGamePlusDraft draft = new(source, source with { Ff8Completion = null }, new());
        draft.Reset("magic", NewGamePlusPreset.Empty);
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = false });
        draft.SetMagic(0, 1, 70);
        draft.SetMagic(1, 1, 70);
        draft.SetMagic(2, 1, 70);
        draft.ConvertMagic(new GameplayPreferences { SharedMagicStock = true });
        return draft;
    }
}
