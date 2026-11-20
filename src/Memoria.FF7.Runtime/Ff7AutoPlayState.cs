using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayDeferral(string TargetId, string SourceConditionSignature, string GuardFingerprint, IReadOnlyList<AutoPlayChoiceSelection> Selections, string? ChoiceKey = null, int FirstOption = 0, int OptionCount = 0) : IAutoPlayParticipantPolicyDeferral;

public sealed record Ff7AutoPlaySpatialOwner(int SourceProgramCounter, int TargetEntity, int TargetScript, int TargetPriority, int RequestMode, int SourcePriority = 7);

public sealed record Ff7AutoPlayWorldModeGoal(string TargetEventId, Ff7AutoPlayWorldChunk Chunk, int Script, int RiderModel);

public sealed record Ff7AutoPlayActiveEvent(string VisitId, string EventId, string ConditionFingerprint, long Generation, int Entity, int Script, Ff7AutoPlayTargetKind Kind, int? DestinationField, int BeforeStory, string BeforeWitness, bool Accepted = false, bool Returned = false, bool TransitionOwned = false, int LinePhase = 0, float LineSide = 0, int MovementCallbacks = 0, int StagnantCallbacks = 0, int? LastX = null, int? LastY = null, int? LastZ = null, bool CompletionRecorded = false, string? NavigationKey = null, string? NavigationProbeKey = null, string? EntryAvailabilityFingerprint = null, int[]? SpatialDestinations = null, AutoPlayEventRoute? TravelRoute = null, IReadOnlyList<AutoPlayChoiceSelection>? TravelSelections = null, Ff7AutoPlaySpatialOwner? SpatialOwner = null, IReadOnlyDictionary<string, string>? EntryGuardFingerprints = null, FieldSpatialEffectOwner? DirectEffectOwner = null, Ff7AutoPlayWorldControlObservation? WorldControl = null, int WorldControlPhase = 0, Ff7AutoPlayWorldModeGoal? WorldModeGoal = null);
public sealed record Ff7AutoPlayMovementFailure(int MinX, int MaxX, int MinY, int MaxY, int? MinZ = null, int? MaxZ = null);
public sealed record Ff7AutoPlayPhysicalFailureReceipt(int Revision, string Contract, string NativeSource, long Expanded);
public sealed record Ff7AutoPlayNavigationGuard(string VisitId, int Entity, int[]? BlockingEntities = null, string? EventId = null, string? NavigationKey = null, int? SourceTriangle = null, Ff7AutoPlayMovementFailure? MovementFailure = null, int[]? SpatialBoundaryTriangles = null, string? ObservedFingerprint = null, bool? MovementSourceInside = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Ff7AutoPlayPhysicalFailureReceipt? PhysicalFailure = null);
public sealed record Ff7AutoPlayGatewayTopology(string NavigationKey, string EventId, string? ConditionSignature, int Revision, int SourceTriangle, int TargetTriangle);

/// <summary>Observational planner state; native progress and execution remain owned by the session.</summary>
public sealed record Ff7AutoPlaySourceRevalidationTask(string NavigationKey, string EventId, string Condition, string DepartureReceiptKey, bool ArrivalAcknowledged = false);
public sealed record Ff7AutoPlayTravelCommitment(AutoPlayTravelGoal Goal, string NavigationKey, ushort? DestinationField);
public sealed record Ff7AutoPlayChoicePreference(int Selection, string NativeContext, string GuestContext, Dictionary<string, string>? NativeComponents = null);

public sealed class Ff7AutoPlayState
{
    public Dictionary<string, Ff7AutoPlayTalkConditionDomain> TalkConditionDomains { get; set; } = new(StringComparer.Ordinal);
    public int Version { get; set; } = 1;
    public AutoPlayPlannerState<Ff7AutoPlayDeferral> Coverage { get; set; } = new();
    public ushort? DestinationField { get; set; }
    public Ff7AutoPlayActiveEvent? Active { get; set; }
    public Ff7AutoPlayNativeRoute? NativeRoute { get; set; }
    public Ff7AutoPlayChoiceState? Choice { get; set; }
    public Ff7AutoPlayRestoreRequest? PendingRestore { get; set; }
    public Dictionary<string, Ff7AutoPlayNavigationGuard> NavigationGuards { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Ff7AutoPlayGatewayTopology> GatewayTopology { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> NavigationProbes { get; set; } = new(StringComparer.Ordinal);
    public Ff7AutoPlaySourceRevalidationTask? NativeRevalidation { get; set; }
    public HashSet<string> SourceRevalidationAttempts { get; set; } = new(StringComparer.Ordinal);
    public AutoPlayTravelGoal? NavigationGoal { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7AutoPlayTravelCommitment? TravelCommitment { get; set; }
    public Dictionary<string, Ff7AutoPlayVisitGuard> VisitGuards { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Ff7AutoPlayWorldVisitState> WorldVisits { get; set; } = new(StringComparer.Ordinal);
    public Ff7AutoPlayWorldDispatchReceipt? WorldDispatch { get; set; }
    public WorldHostRequest? WorldDialogueOwner { get; set; }
    public string? Status { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChoiceContinuationDiagnostic { get; set; }
    public Dictionary<string, Ff7AutoPlayChoicePreference> ChoicePreferences { get; set; } = new(StringComparer.Ordinal);
    public string? FrontierProgressKey { get; set; }
    public int FrontierIdleSteps { get; set; }

    public Ff7AutoPlayState Clone()
    {
        return JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(this)) ?? throw new InvalidDataException("Automatic play state could not be restored.");
    }

    public void Validate()
    {
        SourceRevalidationAttempts ??= new(StringComparer.Ordinal);
        if (NavigationGuards.Values.Any(guard => guard.PhysicalFailure is { } receipt && (receipt.Expanded < 0 || receipt.Contract is not { Length: 64 } || !receipt.Contract.All(Uri.IsHexDigit) || receipt.NativeSource is not { Length: 64 } || !receipt.NativeSource.All(Uri.IsHexDigit))))
        {
            throw new InvalidDataException("The local physical search receipt is corrupt.");
        }
        if (NativeRevalidation is { } task && (string.IsNullOrEmpty(task.NavigationKey) || string.IsNullOrEmpty(task.EventId) || task.Condition is null || task.DepartureReceiptKey is not { Length: 64 } || !task.DepartureReceiptKey.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("The native source revalidation task is corrupt.");
        }
        if (SourceRevalidationAttempts.Count > 65536 || SourceRevalidationAttempts.Any(key => key is null || key.Length != 64 || !key.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("The native source revalidation cache is corrupt.");
        }
        if (Version != 1 || Coverage is null || Coverage.Visits.Count > 4096 || Coverage.Choices.Count > 4096 || Coverage.Visits.Any(visit => visit.Events.Count > 4096) || FrontierIdleSteps is < 0 or > Ff7AutoPlayPolicy.FrontierIdleLimit)
        {
            throw new InvalidDataException("The automatic play observation state is unsupported or corrupt.");
        }
        _ = new AutoPlayPlanner<Ff7AutoPlayDeferral>(Coverage);
        if (NativeRoute is { SourceReleased: true } released && (released.BattleBoundary || released.GatewayDeparture is not null))
        {
            throw new InvalidDataException("A native source release cannot claim a battle or gateway completion.");
        }
        if (NativeRoute?.GatewayDeparture is { } gateway && (gateway.Generation != NativeRoute.Generation || gateway.GatewayIndex < 0 || string.IsNullOrEmpty(gateway.Field) || gateway.Transition is null || gateway.From is null || gateway.To is null || NativeRoute.BattleBoundary))
        {
            throw new InvalidDataException("The bounded native gateway departure is corrupt.");
        }
        if (NativeRoute is { } route && (route.Generation < 0 || string.IsNullOrEmpty(route.EventId) || string.IsNullOrEmpty(route.ConditionFingerprint) || route.Steps is null || route.Steps.Length < 1 || route.Cursor < 0 || route.Cursor > route.Steps.Length || route.After is not { Length: 64 } || route.Steps.Any(step => step is null || step.Before is not { Length: 64 } || step.Input.Horizontal is < -1 or > 1 || step.Input.Vertical is < -1 or > 1)))
        {
            throw new InvalidDataException("The bounded native navigation continuation is corrupt.");
        }
    }
}
