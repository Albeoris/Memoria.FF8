using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSlotsLearningOutcomeTests
{
    [Fact]
    public void Slots_learning_copies_back_once_and_rejects_a_stale_canonical_before_byte_atomically()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeLimitProgress original = assets.ReadNewGameLimitProgress() with { SpecialMagicFlags = 0x80 };
        BattleEncounterOccurrence encounter = new("slots-learning-component", 512, new string('A', 64), SceneKind.Field, 999, 0, new string('B', 64), null);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, Economy = economy, LimitProgress = original, PendingBattleEncounter = encounter });
        string before = JsonSerializer.Serialize(session.State);

        Assert.Throws<InvalidDataException>(() => session.CommitBattleResult(encounter, vitals, economy, null, limitSpecialFlagsBefore: 0, limitSpecialFlagsAfter: 4, limitSpecialSetMask: 4));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        session.CommitBattleResult(encounter, vitals, economy, null, limitSpecialFlagsBefore: 0x80, limitSpecialFlagsAfter: 0x84, limitSpecialSetMask: 4);

        Assert.Equal(original with { SpecialMagicFlags = 0x84 }, session.State.LimitProgress);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitBattleResult(encounter, vitals, economy, null));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(committed)!);
        Assert.Equal((byte)0x84, restored.State.LimitProgress!.SpecialMagicFlags);
    }
}
