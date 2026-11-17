namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNorgQuakeTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_encounter63_Quake_preserves_target_sequences_and_cold_next()
    {
        NativeBattleNorgMagicSequenceTests.Run(output, 38, "Quake", battle => battle.Presentation?.Graphics?.OriginalEffect is { Binding.Effect: 38, ReturnedDescriptor: > 0 } && battle.Presentation?.Memory?.Quake38?.SavedActors.Any(entry => entry.Key is Memoria.FF8.Runtime.BattleEnemyModelOwner && entry.Value.BouncePhase == 1) == true);
    }
}
