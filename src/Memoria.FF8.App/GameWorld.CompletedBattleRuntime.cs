using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private void ReconstructCompletedBattleRuntime(BattleEncounterOccurrence occurrence)
    {
        GameState expected = session.State;
        long generation = assets.Resources.CatalogGeneration;
        NativeCharacterVitals current = expected.CharacterVitals ?? throw new InvalidDataException("Completed combat has no canonical character progression.");
        NativeCharacterVitals installed = assets.ReadNewGameCharacterVitals();
        if (modelOverrides is GameResourceHost selected)
        {
            installed = GameplaySaveGuardianForceDefinitions.Select(installed, selected);
        }
        else if (current.GuardianForceProfile is { } profile)
        {
            installed = GameplaySaveGuardianForceDefinitions.Select(installed, profile.AppliedLayers);
        }
        NativeCharacterVitals reconstructed = GameplaySaveCharacters.Capture(current).Restore(installed);
        NativeShotAvailabilityState? shots = expected.ShotAvailability is { } retainedShots ? assets.ReadNewGameShotAvailability() with { Flags770 = retainedShots.Flags770 } : null;
        if (reconstructed.InitOutSha256 != expected.Economy?.InitOutSha256 || expected.CharacterAppearance is { } appearance && appearance.InitOutSha256 != reconstructed.InitOutSha256 || expected.SavedBattleFlags is { } flags && ((flags.Value & 1) != 0) != reconstructed.DreamEnabled)
        {
            throw new InvalidDataException("Selected character definitions contradict the retained initialization or dream owners.");
        }
        NativeBattleRetainedMemory memory = assets.ReadColdProcessNativeBattleMemory();
        if (!ReferenceEquals(expected, session.State) || generation != assets.Resources.CatalogGeneration)
        {
            throw new InvalidOperationException("Completed combat or selected resources changed during runtime reconstruction.");
        }
        session.ReconstructCompletedBattleRuntime(occurrence, memory, reconstructed, shots);
    }
}
