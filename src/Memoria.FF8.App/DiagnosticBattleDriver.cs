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
    public BattleDiagnosticEnemyAbility? EnemyAbility { get; init; }
    public BattleDiagnosticLimitSelection? LimitSelection { get; init; }
    public int? ExpectedNativeAction { get; init; }
    public byte? ExpectedNativeCommand { get; init; }
    public ushort[] SupportingEffects { get; init; } = [];
    public BattleDiagnosticSupportingInvocation[] SupportingInvocations { get; init; } = [];
    public BattleDiagnosticCombatRepair[] Repairs { get; init; } = [];
    public BattleDiagnosticTerminalHoldDeclaration? TerminalHold { get; init; }

    internal void Validate()
    {
        if (Input is null)
        {
            throw new InvalidDataException("Diagnostic driver requires a semantic input.");
        }
        Input.Validate();
        EnemyAbility?.Validate();
        LimitSelection?.Validate();
        if (SupportingEffects is null || SupportingEffects.Any(effect => effect == 0 || effect == ExpectedEffect) || SupportingEffects.Distinct().Count() != SupportingEffects.Length || Repairs is null || Repairs.Length > 3 || Repairs.Any(repair => repair is null))
        {
            throw new InvalidDataException("Diagnostic supporting dispatches and between-execution repairs must be explicit and bounded.");
        }
        foreach (BattleDiagnosticCombatRepair repair in Repairs)
        {
            repair.Validate();
        }
        if (SupportingInvocations is null || SupportingInvocations.Length > 32 || SupportingInvocations.Any(invocation => invocation is null) || SupportingInvocations.Distinct().Count() != SupportingInvocations.Length)
        {
            throw new InvalidDataException("Diagnostic supporting invocations require distinct bounded original reaction descriptors.");
        }
        foreach (BattleDiagnosticSupportingInvocation invocation in SupportingInvocations)
        {
            invocation.Validate();
            if (invocation.EffectId == ExpectedEffect || string.Equals(invocation.ActorId, Input.ActorId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Diagnostic supporting invocations must belong to another actor and preserve the focal effect identity.");
            }
        }
        TerminalHold?.Validate();
        if (TerminalHold is not null && (Repairs.Length == 0 || TerminalHold.PartyActorIds.Any(actorId => !Repairs.Any(repair => string.Equals(repair.ActorId, actorId, StringComparison.Ordinal)))))
        {
            throw new InvalidDataException("Diagnostic terminal hold requires explicit between-execution repairs for every declared party actor.");
        }
        if (ExpectedNativeAction is < 0 or > ushort.MaxValue || ExpectedNativeAction.HasValue != ExpectedNativeCommand.HasValue)
        {
            throw new InvalidDataException("Explicit native invocation identity requires a command/action pair.");
        }
        if (Input.Kind is not (BattleInputKind.Action or BattleInputKind.Attack) || Input.Ability == BattleAbility.LimitBreak && LimitSelection is null || RequiresCapabilityAdapter || RequiresCombatRepairs || EnemyAbility is not null && (Input.Ability != BattleAbility.EnemyAbility || Input.ActionId != EnemyAbility.ActionId) || EnemyAbility is not null && LimitSelection is not null)
        {
            throw new NotSupportedException("The driver requires ordinary semantic input or an explicit native enemy/Limit selection; unresolved capabilities and combat repairs need typed adapters.");
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
    private const double MaximumDispatchWaitSeconds = 10;
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
        if (recipe.SupportingEffects.Contains(dispatch.RequestedEffect) && dispatch.ExecutedEffect == dispatch.RequestedEffect)
        {
            return;
        }
        if (dispatch.Ability != recipe.Input.Ability || dispatch.ActionId != (recipe.ExpectedNativeAction ?? recipe.Input.ActionId) || recipe.ExpectedNativeCommand is byte command && dispatch.NativeCommand != command)
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
        if (Stage == DiagnosticDriverStage.AwaitDispatch && observation.Seconds - executionStarted >= Math.Min(MaximumDispatchWaitSeconds, recipe.ActionTimeoutSeconds))
        {
            Fail("EXPECTED_EFFECT_DISPATCH_TIMEOUT");
            return false;
        }
        if (Stage is DiagnosticDriverStage.Drain or DiagnosticDriverStage.Tail && observation.Seconds - executionStarted > recipe.ActionTimeoutSeconds)
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
