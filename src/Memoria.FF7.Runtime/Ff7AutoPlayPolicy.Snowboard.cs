using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native snowboard-departure preference (NG-107).
    private const string SnowboardDepartureProgram = "A513104CED946295581FA2C752FA5E3804FC542FAF7DA93C594F3A47485F9658";

    internal static bool IsNativeSnowboardDeparture(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, Ff7FieldProcess process, FieldVariables variables)
    {
        return observation.ProgramIdentity == SnowboardDepartureProgram && observation.CanAcceptInput && observation.Events.Contains(target) && target is { Id: "line:1:entity:11:script:2", Entity: 11, Script: 2, Kind: Ff7AutoPlayTargetKind.LinePush, AnalysisComplete: true } && !process.PlayerLocked && !process.MenuDisabled && process.GlobalCommand == Ff7GlobalCommand.None && process.Transition is null && process.Join is null && process.Movie is not { Playing: true } && process.Menu is not { Completed: false } && !process.Dialogues.Any(dialogue => !dialogue.Completed) && (variables.ReadByte(FieldBank.Persistent0Byte, 0x82) & 0xC2) == 0x42 && variables.ReadByte(FieldBank.Persistent1Byte, 9) == 0 && process.Actors.Any(actor => actor.EntityBound && actor.PlayerControlled && actor.PartyCharacter == 0) && process.Lines.Any(line => line.Slot == 1 && line.Entity == 11 && line.Enabled != 0);
    }

    private bool IsSnowboardPreferredEntry(AutoPlayVisit visit, AutoPlayEvent target)
    {
        return session.World is null && session.Battle is null && State.DestinationField is null && ordinaryObservation is { } observation && visit.Key == observation.VisitId && observation.Events.SingleOrDefault(current => current.Id == target.Id && current.ConditionFingerprint == target.ConditionSignature) is { } current && IsNativeSnowboardDeparture(observation, current, session.Host.Process, session.Machine.Variables);
    }

    internal static bool RetireOptionalPlanForSnowboard(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables)
    {
        if (state.DestinationField is not null || state.Choice is not null || state.PendingRestore is not null || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField is not null || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint) || !observation.Events.Any(target => target.Id != active.EventId && IsNativeSnowboardDeparture(observation, target, process, variables)))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions?.ActiveEntry == active.ConditionFingerprint)
        {
            coverage.Conditions.ActiveEntry = null;
        }
        state.Active = null;
        state.NavigationGoal = null;
        state.TravelCommitment = null;
        return true;
    }
}
