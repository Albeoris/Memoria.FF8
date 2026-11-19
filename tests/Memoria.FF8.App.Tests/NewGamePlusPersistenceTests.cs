using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusPersistenceTests
{
    [Fact]
    public void OrdinaryReloadRestoresEarnedProgressAndRulesWithoutReapplyingStartingRecipe()
    {
        GameState native = CompletedSaveFixture.State() with
        {
            Scene = SceneKind.Field,
            FieldId = 74,
            FieldLoadContext = NativeFieldLoadContext.NewGame(),
            WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(),
            Ff8Completion = null,
            EventState = null
        };
        NewGamePlusDraft draft = new(CompletedSaveFixture.State(), native, new());
        draft.ExperienceMultiplierTenths = 15;
        draft.ApMultiplierTenths = 4;
        draft.Reset("items", NewGamePlusPreset.Empty);
        draft.Reset("magic", NewGamePlusPreset.Empty);
        GameState started = draft.ApplyTo(native);
        NativeCharacterVitals currentCharacters = started.CharacterVitals!.ChangeMagicStock(0, 1, 37, MagicOperationReason.ScriptReward, out _);
        currentCharacters.Records[0] = currentCharacters.Records[0] with { Experience = currentCharacters.Records[0].Experience + 59 };
        NativeEconomy currentItems = started.Economy!.AddItem(17, 23, out _);
        NativeCardInventory currentCards = started.CardInventory!.Acquire(77, out _);
        GameState current = started with { CharacterVitals = currentCharacters, Economy = currentItems, CardInventory = currentCards };
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(current, new(1, GameplaySaveResumeKind.Field, 74, null, new(0, 0, 0, 0), null));
        document = document with { Progression = document.Progression! with { SceneFacts = new(GameplaySaveSceneFactsOrigin.Field, 74, [], [], [0, 255, 255], 0, true, true, false, 0, null, null, null) { PersistentBattleMode = 0, ActiveBattleMode = 0 } } };
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        stream.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(stream);
        GameState restored = GameplaySaveStateRestoration.Restore(decoded, native);
        Assert.Null(restored.CompletedGame);
        Assert.Null(restored.Ff8Completion);
        Assert.Equal(37, restored.CharacterVitals!.GetMagicCount(0, 1));
        Assert.Equal(currentCharacters.Records[0].Experience, restored.CharacterVitals.Records[0].Experience);
        Assert.Equal(23, restored.Economy!.Items.Single(item => item.ItemId == 17).Count);
        Assert.Equal(1, restored.CardInventory!.AvailableCount(77));
        Assert.Equal(15, restored.NewGamePlusRewards!.ExperienceTenths);
        Assert.Equal(4, restored.NewGamePlusRewards.AbilityPointTenths);
        Assert.True(restored.NewGamePlus!.StartingChoicesApplied);
        Assert.All(restored.NewGamePlus.InitialItems, item => Assert.Equal(0, item.Count));
        Assert.All(restored.NewGamePlus.InitialMagic.SelectMany(bank => bank.Stock), spell => Assert.Equal(0, spell.Count));
        _ = new GameSession(restored);
    }
}
