namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDefeatReturnBranch(ushort PersistentFlags182)
{
    public bool ResumePreviousScene => (PersistentFlags182 & 0x200) != 0;
}

public sealed record NativeBattleAbandonedWait(long FiberSequence, int NextOperation, long WaitUntil);

public sealed record NativeBattleAbandonment(BattleEncounterOccurrence Encounter, NativeBattleDefeatReturnBranch Branch, long EntryClockCalls, long ExitClockCalls, int CombatCallbacks)
{
    public NativeBattleAbandonedWait? ScriptWait { get; init; }

    public void Validate()
    {
        Encounter.Validate();
        if (Encounter.ScriptedOwner is { } owner ? ScriptWait is null || ScriptWait.FiberSequence < 0 || ScriptWait.NextOperation != owner.Operation + 1 || ScriptWait.WaitUntil != Encounter.SourceFrame + 1 : ScriptWait is not null)
        {
            throw new InvalidDataException("Abandoned BATTLE requires its actual suspended typed wait, without a return frame or completed fiber.");
        }
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
        if (!State.AppliedBattleResultIds.Contains(abandoned.Encounter.OccurrenceId, StringComparer.Ordinal) || State.ExamTimer?.ClockCalls != abandoned.ExitClockCalls || (acceptedBattleObservation?.NativeResult ?? State.Battle?.NativeGraphics?.NativeOutcome?.ResultByte6E7) != 1)
        {
            throw new InvalidDataException("Fresh defeat allocation requires its applied native result1 and actual process clock.");
        }
        State = CaptureLiveBattle(State);
        liveBattle = null;
        acceptedBattleObservation = null;
        GameState candidate = PrepareFieldRequest(75, 0) with
        {
            FieldLoadContext = NativeFieldLoadContext.FromBattleDefeat(abandoned.Branch),
            Battle = null,
            RandomEncounters = State.RandomEncounters is { } random ? random with { Pending = null } : null,
            AbandonedNativeBattles = [.. State.AbandonedNativeBattles ?? [], abandoned]
        };
        Dialog = null;
        Change(candidate);
    }
}
