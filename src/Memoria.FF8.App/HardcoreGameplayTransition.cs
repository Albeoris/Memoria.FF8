using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class HardcoreGameplayTransition
{
    public static MagicModePreview? Apply(GameSession session, GameplayPreferences preferences, MagicModePreview? reviewed, Func<BattleDifficulty, bool> publishDifficulty)
    {
        BattleDifficulty previous = session.State.GameplayPreferences.Difficulty;
        bool applied = false;
        try
        {
            if (!publishDifficulty(BattleDifficulty.Hardcore))
            {
                throw new InvalidOperationException(GameResourceHost.GameplayDifficultyProfilePending);
            }
            // Publishing a difficulty profile can update GF definitions and party vitals.
            // Build the migration from that committed state, retaining the reviewed consequences.
            MagicModePreview current = session.PreviewGameplayPreferences(preferences);
            if (reviewed is not null && (!reviewed.Moves.SequenceEqual(current.Moves) || !reviewed.Overflow.SequenceEqual(current.Overflow) || !reviewed.RemovedJunctions.SequenceEqual(current.RemovedJunctions) || !reviewed.HitPointChanges.SequenceEqual(current.HitPointChanges)))
            {
                return current;
            }
            session.ApplyGameplayPreferences(current);
            applied = true;
            return null;
        }
        finally
        {
            if (!applied)
            {
                publishDifficulty(previous);
            }
        }
    }
}
