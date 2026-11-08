namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNorgQuakeTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_encounter63_Quake_preserves_target_sequences_and_cold_next()
    {
        NativeBattleNorgMagicSequenceTests.Run(output, 38, "Quake", battle => battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 38, ReturnedDescriptor: > 0 } && battle.NativeEffectMemory?.Quake38?.SavedActors.Skip(3).Any(actor => actor.BouncePhase == 1) == true);
    }
}
