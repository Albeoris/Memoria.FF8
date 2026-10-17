namespace Memoria.FF8.Runtime;

public sealed record NativeWorldBattleOutcome(BattleEncounterOccurrence Encounter, byte Result, long EntryClockCalls, long ExitClockCalls)
{
    public NativeBattleDefeatReturnBranch? DefeatBranch { get; init; }

    public void Validate()
    {
        Encounter.Validate();
        if (Encounter.SourceScene != SceneKind.WorldMap || Encounter.ScriptedOwner is not null || Result is not (1 or 2 or 4) || (Result == 1 ? DefeatBranch is not { ResumePreviousScene: true } : DefeatBranch is not null) || EntryClockCalls < 0 || ExitClockCalls < EntryClockCalls || (ExitClockCalls - EntryClockCalls) % 4 != 0)
        {
            throw new InvalidDataException("World battle outcome requires its actual native result and executed entry/exit clocks.");
        }
    }
}
