using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private void CompleteWorldBattleProfileHandoff(BattleEncounterOccurrence occurrence)
    {
        string selected = assets.NativeRuntimeProfile?.ExecutableHash ?? throw new NotSupportedException("World battle return requires supported selected runtime resources.");
        NativeBattleRetainedMemory retained = session.State.NativeBattleMemory ?? throw new InvalidDataException("World battle return lost its retired runtime owner.");
        if (retained.ColdExecutableSha256 == selected && retained.ModelCpuResources?.Cpu.EffectiveLayout == NativeBattlePartyCpuLayout.ForImage(selected))
        {
            return;
        }
        if (worldScene is null || session.State.Scene != SceneKind.WorldMap || session.State.World != worldScene.State)
        {
            throw new InvalidOperationException("World runtime reconstruction requires its current completed world allocation.");
        }
        GameState before = session.State;
        ReconstructCompletedBattleRuntime(occurrence);
        PublishEventProcess();
        if (session.State.DepartingFieldState is not null && Events.HasFieldProgram)
        {
            session.SetDepartingFieldState(Events.Capture());
        }
        if (!ReferenceEquals(before.World, session.State.World) || before.WorldProcess != session.State.WorldProcess || before.ExamTimer != session.State.ExamTimer || before.Playtime != session.State.Playtime || before.CrtRandom != session.State.CrtRandom || before.GlobalRandom != session.State.GlobalRandom || before.EventRandom != session.State.EventRandom)
        {
            throw new InvalidDataException("World runtime reconstruction changed a canonical world or clock owner.");
        }
    }

    private bool HasRetiredWorldBattleFieldAllocation()
    {
        string? selected = assets.NativeRuntimeProfile?.ExecutableHash;
        if (session.State.Scene != SceneKind.WorldMap || session.HasActiveBattle || session.State.PendingBattleEncounter is not null || session.State.NativeBattleMemory?.ColdExecutableSha256 != selected || Events.NativeRuntimeProfile?.ExecutableHash == selected || Events.Scene.NativeWorldBattleOutcome is not { } outcome)
        {
            return false;
        }
        return outcome.Encounter.SourceScene == SceneKind.WorldMap && session.State.AppliedBattleResultIds.Contains(outcome.Encounter.OccurrenceId, StringComparer.Ordinal);
    }
}
