namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDefeatReturnBranch(ushort PersistentFlags182)
{
    public bool ResumePreviousScene => (PersistentFlags182 & 0x200) != 0;
}

public sealed record NativeBattleAbandonment(BattleEncounterOccurrence Encounter, NativeBattleDefeatReturnBranch Branch, long EntryClockCalls, long ExitClockCalls, int CombatCallbacks)
{
    public void Validate()
    {
        Encounter.Validate();
        if (Branch.ResumePreviousScene || EntryClockCalls < 0 || CombatCallbacks < 0 || ExitClockCalls != checked(EntryClockCalls + 4L * CombatCallbacks))
        {
            throw new InvalidDataException("Abandoned battle requires the actual fresh-field75 branch and executed callback interval.");
        }
    }
}

public sealed partial class GameSession
{
    public void EnterNativeDefeatField75(NativeBattleAbandonment abandoned)
    {
        abandoned.Validate();
        if (!State.AppliedBattleResultIds.Contains(abandoned.Encounter.OccurrenceId, StringComparer.Ordinal) || State.ExamTimer?.ClockCalls != abandoned.ExitClockCalls || State.Battle?.NativeGraphics?.NativeOutcome?.ResultByte6E7 != 1)
        {
            throw new InvalidDataException("Fresh defeat allocation requires its applied native result1 and actual process clock.");
        }
        GameState candidate = State with
        {
            Scene = SceneKind.Field,
            FieldId = 75,
            FieldName = "field_75",
            FieldLoadContext = NativeFieldLoadContext.FromBattleDefeat(abandoned.Branch),
            EntryPoint = 0,
            EventState = null,
            DepartingFieldState = null,
            DialogueDraft = null,
            FieldArrival = null,
            WorldArrival = null,
            World = null,
            Battle = null,
            RandomEncounters = State.RandomEncounters is { } random ? random with { Pending = null } : null,
            AbandonedNativeBattles = [.. State.AbandonedNativeBattles ?? [], abandoned]
        };
        Dialog = null;
        Change(candidate);
    }
}
