namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private string originalAchievementActionBranch = Guid.NewGuid().ToString("N");

    private GameState StageOriginalAchievementNativeMutations(GameState candidate, bool salaryPaid = false)
    {
        if (salaryPaid)
        {
            candidate = StageOriginalAchievementNativeMutation(candidate, OriginalAchievementActionKind.FirstSalary, "salary");
        }
        NativeDrawPointMemory? before = State.EventRandom?.DrawPoints;
        NativeDrawPointMemory? after = candidate.EventRandom?.DrawPoints;
        if (before is null || after is null)
        {
            return candidate;
        }
        before.Validate();
        after.Validate();
        for (int index = 0; index < 256; index++)
        {
            int shift = 2 * (index & 3);
            int previousMasked = before.Depletion[index / 4] & (3 << shift);
            int next = (after.Depletion[index / 4] >> shift) & 3;
            // The original compares the previous masked byte without normalizing its bit position.
            if (!(previousMasked == 0 && next > 0 || previousMasked == 1 && next > 1))
            {
                continue;
            }
            candidate = StageOriginalAchievementNativeMutation(candidate, OriginalAchievementActionKind.DrawPoint, "draw-point");
        }
        return candidate;
    }

    private GameState StageOriginalAchievementNativeMutation(GameState candidate, OriginalAchievementActionKind kind, string operation, uint? squallExperience = null)
    {
        string context = originalAchievementActionBranch;
        long sequence = candidate.OriginalAchievementMutationContext == context ? checked(candidate.OriginalAchievementMutationSequence + 1) : 1;
        candidate = candidate with { OriginalAchievementMutationContext = context, OriginalAchievementMutationSequence = sequence };
        return StageOriginalAchievementAction(candidate, kind, $"{operation}/{sequence}", "original-mutations", squallExperience);
    }

    public void RecordOriginalSalaryPayment()
    {
        State = StageOriginalAchievementNativeMutation(State, OriginalAchievementActionKind.FirstSalary, "salary");
    }

    private GameState StageOriginalMovieAchievement(GameState candidate, int movieIndex)
    {
        if (candidate.MovieSource is not { } source || source.MovieIndex != movieIndex)
        {
            return candidate;
        }
        if (source.DiscIndex == 3 && movieIndex == 6)
        {
            return StageOriginalAchievementNativeMutation(candidate, OriginalAchievementActionKind.FinishGame, "ending", candidate.CharacterVitals?.Records[0].Experience);
        }
        return source.DiscIndex == 2 && movieIndex == 25 ? StageOriginalAchievementNativeMutation(candidate, OriginalAchievementActionKind.Ragnarok, "ragnarok") : candidate;
    }
}
