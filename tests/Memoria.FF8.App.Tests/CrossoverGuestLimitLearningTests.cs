using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestLimitLearningTests
{
    [Fact]
    public void Learning_projection_preserves_unrelated_staged_owners_and_rejects_stale_source_bytes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVIII installation.");
        using GameAssets game = new(directory!);
        GameplaySaveComponentDocument initialized = GameplaySaveSessionLoaderTests.Document(game, world: false);
        GameplaySaveComponentDocument original = initialized with { Limits = initialized.Limits with { SpecialMagicFlags = 0x80 }, Progression = initialized.Progression! with { Collections = initialized.Progression!.Collections with { ShotAvailability = 0x40 } } };
        GameplaySaveComponentDocument staged = original with { Economy = original.Economy with { Gil = original.Economy.Gil + 1 } };
        Guid encounter = Guid.NewGuid();
        CrossoverGuestLimitLearningSettlement learning = new(encounter, 0x40, 3, 0x80, 4);
        string before = JsonSerializer.Serialize(original);
        GameplaySaveComponentDocument applied = CrossoverBattleVitalsSettlement.ApplyLimitLearning(original, staged, learning, encounter, [2, 5]);
        Assert.Equal((byte)0x43, applied.Progression!.Collections.ShotAvailability);
        Assert.Equal((byte)0x84, applied.Limits.SpecialMagicFlags);
        Assert.Equal(staged.Economy, applied.Economy);
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.Throws<InvalidDataException>(() => CrossoverBattleVitalsSettlement.ApplyLimitLearning(original, staged, learning with { ShotBefore = 0 }, encounter, [2, 5]));
        Assert.Throws<InvalidDataException>(() => CrossoverBattleVitalsSettlement.ApplyLimitLearning(original, staged, learning with { SlotsBefore = 0 }, encounter, [2, 5]));
        Assert.Throws<InvalidDataException>(() => CrossoverBattleVitalsSettlement.ApplyLimitLearning(original, staged, learning, encounter, [2]));
        Assert.Throws<InvalidDataException>(() => CrossoverBattleVitalsSettlement.ApplyLimitLearning(original, staged, learning, Guid.NewGuid(), [2, 5]));
        Assert.Same(staged, CrossoverBattleVitalsSettlement.ApplyLimitLearning(original, staged, new(encounter), encounter, []));
    }
}
