using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattleDriverRecipe([property: JsonRequired] BattleInput Input, [property: JsonRequired] ushort ExpectedEffect)
{
    public double BaselineSeconds { get; init; } = 1;
    public double ReadinessTimeoutSeconds { get; init; } = 30;
    public double ActionTimeoutSeconds { get; init; } = 120;
    public double OverallTimeoutSeconds { get; init; } = 300;
    public int TailFrames { get; init; } = 2;
    public string? ProgressPath { get; init; }
    public bool RequiresCapabilityAdapter { get; init; }
    public bool RequiresCombatRepairs { get; init; }

    internal void Validate()
    {
        if (Input is null)
        {
            throw new InvalidDataException("Diagnostic driver requires a semantic input.");
        }
        Input.Validate();
        if (Input.Kind is not (BattleInputKind.Action or BattleInputKind.Attack) || Input.Ability == BattleAbility.LimitBreak || RequiresCapabilityAdapter || RequiresCombatRepairs)
        {
            throw new NotSupportedException("The preparation driver supports ordinary semantic Action/Attack only; timed limits, borrowed capabilities and combat repairs require explicit future adapters.");
        }
        if (!double.IsFinite(BaselineSeconds) || BaselineSeconds < 0 || !double.IsFinite(ReadinessTimeoutSeconds) || ReadinessTimeoutSeconds <= 0 || !double.IsFinite(ActionTimeoutSeconds) || ActionTimeoutSeconds <= 0 || !double.IsFinite(OverallTimeoutSeconds) || OverallTimeoutSeconds <= 0 || TailFrames < 2)
        {
            throw new InvalidDataException("Diagnostic driver requires finite positive deadlines, an installed effect identity and at least two presented tail frames.");
        }
    }
}

internal enum DiagnosticDriverStage
{
    Readiness,
    Baseline,
    AwaitDispatch,
    Drain,
    Tail,
    Completed,
    Blocked
}

internal sealed record DiagnosticDriverObservation(double Seconds, long Callback, long PresentedFrame, bool Ready, bool Drained, bool Terminal, string[] DrainBlockers);

/// <summary>Two submissions on one uninterrupted host; completion is a preparation candidate, never an acceptance verdict.</summary>
internal sealed class DiagnosticBattleDriver
{
    private readonly DiagnosticBattleDriverRecipe recipe;
    private readonly double started;
    private double stageStarted;
    private double executionStarted;
    private long previousCallback = -1;
    private long tailStarted;
    private long lastDispatchSerial;
    private string? blockedCampaignStage;
    private readonly List<BattleDiagnosticEffectDispatch> dispatches = [];

    internal DiagnosticBattleDriver(DiagnosticBattleDriverRecipe recipe, double seconds)
    {
        recipe.Validate();
        this.recipe = recipe;
        started = stageStarted = seconds;
    }

    internal DiagnosticDriverStage Stage { get; private set; }
    internal int Execution { get; private set; } = 1;
    internal string? Blocker { get; private set; }
    internal IReadOnlyList<BattleDiagnosticEffectDispatch> Dispatches => dispatches;
    internal string[] LastDrainBlockers { get; private set; } = [];
    internal bool Finished => Stage is DiagnosticDriverStage.Completed or DiagnosticDriverStage.Blocked;
    internal string CampaignStage => Stage == DiagnosticDriverStage.Blocked ? blockedCampaignStage ?? "initialization" : Stage == DiagnosticDriverStage.Completed ? "teardown" : Execution == 1 ? Stage is DiagnosticDriverStage.Readiness or DiagnosticDriverStage.Baseline ? "initialization" : "action_one" : Stage is DiagnosticDriverStage.Readiness ? "cleanup_rearm" : "action_two";

    internal void ObserveDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
        if (Finished || Stage is not (DiagnosticDriverStage.AwaitDispatch or DiagnosticDriverStage.Drain or DiagnosticDriverStage.Tail))
        {
            return;
        }
        if (dispatch.Serial <= lastDispatchSerial)
        {
            Fail("DISPATCH_LINEAGE_REGRESSED");
            return;
        }
        lastDispatchSerial = dispatch.Serial;
        if (dispatch.ActorId != recipe.Input.ActorId)
        {
            return;
        }
        if (dispatch.Ability != recipe.Input.Ability || dispatch.ActionId != recipe.Input.ActionId)
        {
            Fail("ACTION_IDENTITY_UNAVAILABLE_OR_MISMATCH");
            return;
        }
        if (dispatch.RequestedEffect != recipe.ExpectedEffect || dispatch.ExecutedEffect != recipe.ExpectedEffect)
        {
            Fail("REQUESTED_EXECUTED_EFFECT_MISMATCH");
            return;
        }
        if (Stage == DiagnosticDriverStage.AwaitDispatch)
        {
            dispatches.Add(dispatch);
            Stage = DiagnosticDriverStage.Drain;
        }
    }

    internal bool Advance(DiagnosticDriverObservation observation)
    {
        if (Finished)
        {
            return false;
        }
        LastDrainBlockers = observation.DrainBlockers;
        if (observation.Callback < previousCallback)
        {
            Fail("CALLBACK_LINEAGE_REGRESSED");
            return false;
        }
        previousCallback = observation.Callback;
        if (observation.Terminal)
        {
            Fail("REPEAT_PRECONDITION_UNSATISFIED");
            return false;
        }
        if (observation.Seconds - started > recipe.OverallTimeoutSeconds)
        {
            Fail("OVERALL_TIMEOUT");
            return false;
        }
        if (Stage is DiagnosticDriverStage.AwaitDispatch or DiagnosticDriverStage.Drain or DiagnosticDriverStage.Tail && observation.Seconds - executionStarted > recipe.ActionTimeoutSeconds)
        {
            Fail("ACTION_OR_DRAIN_TIMEOUT");
            return false;
        }
        if (Stage == DiagnosticDriverStage.Readiness)
        {
            if (observation.Seconds - stageStarted > recipe.ReadinessTimeoutSeconds)
            {
                Fail("COMMAND_OR_ENTRY_READINESS_TIMEOUT");
            }
            else if (observation.Ready && observation.Drained)
            {
                Stage = Execution == 1 ? DiagnosticDriverStage.Baseline : DiagnosticDriverStage.AwaitDispatch;
                stageStarted = observation.Seconds;
                if (Execution == 2)
                {
                    executionStarted = observation.Seconds;
                    return true;
                }
            }
        }
        else if (Stage == DiagnosticDriverStage.Baseline)
        {
            if (!observation.Ready || !observation.Drained)
            {
                Stage = DiagnosticDriverStage.Readiness;
                stageStarted = observation.Seconds;
            }
            else if (observation.Seconds - stageStarted >= recipe.BaselineSeconds)
            {
                Stage = DiagnosticDriverStage.AwaitDispatch;
                executionStarted = observation.Seconds;
                return true;
            }
        }
        else if (Stage == DiagnosticDriverStage.Drain && observation.Drained)
        {
            Stage = DiagnosticDriverStage.Tail;
            tailStarted = observation.PresentedFrame;
        }
        else if (Stage == DiagnosticDriverStage.Tail)
        {
            if (!observation.Drained)
            {
                Stage = DiagnosticDriverStage.Drain;
            }
            else if (observation.PresentedFrame - tailStarted >= recipe.TailFrames)
            {
                Stage = Execution == 2 ? DiagnosticDriverStage.Completed : DiagnosticDriverStage.Readiness;
                Execution = 2;
                stageStarted = observation.Seconds;
            }
        }
        return false;
    }

    internal void Fail(string blocker)
    {
        blockedCampaignStage = CampaignStage;
        Stage = DiagnosticDriverStage.Blocked;
        Blocker = blocker;
    }
}
