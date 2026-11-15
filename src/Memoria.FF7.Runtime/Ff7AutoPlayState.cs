using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayDeferral(string TargetId, string SourceConditionSignature, string GuardFingerprint, IReadOnlyList<AutoPlayChoiceSelection> Selections, string? ChoiceKey = null, int FirstOption = 0, int OptionCount = 0) : IAutoPlayParticipantPolicyDeferral;

public sealed record Ff7AutoPlayActiveEvent(string VisitId, string EventId, string ConditionFingerprint, long Generation, int Entity, int Script, Ff7AutoPlayTargetKind Kind, int? DestinationField, int BeforeStory, string BeforeWitness, bool Accepted = false, bool Returned = false, bool TransitionOwned = false, int LinePhase = 0, float LineSide = 0, int MovementCallbacks = 0, int StagnantCallbacks = 0, int? LastX = null, int? LastY = null, int? LastZ = null, bool CompletionRecorded = false, string? NavigationKey = null, string? NavigationProbeKey = null, string? EntryAvailabilityFingerprint = null);
public sealed record Ff7AutoPlayMovementFailure(int MinX, int MaxX, int MinY, int MaxY);
public sealed record Ff7AutoPlayNavigationGuard(string VisitId, int Entity, int[]? BlockingEntities = null, string? EventId = null, string? NavigationKey = null, int? SourceTriangle = null, Ff7AutoPlayMovementFailure? MovementFailure = null);

/// <summary>Observational planner state; native progress and execution remain owned by the session.</summary>
public sealed class Ff7AutoPlayState
{
    public int Version { get; set; } = 1;
    public AutoPlayPlannerState<Ff7AutoPlayDeferral> Coverage { get; set; } = new();
    public Ff7AutoPlayActiveEvent? Active { get; set; }
    public Ff7AutoPlayChoiceState? Choice { get; set; }
    public Ff7AutoPlayRestoreRequest? PendingRestore { get; set; }
    public Dictionary<string, Ff7AutoPlayNavigationGuard> NavigationGuards { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> NavigationProbes { get; set; } = new(StringComparer.Ordinal);
    public AutoPlayTravelGoal? NavigationGoal { get; set; }
    public Dictionary<string, Ff7AutoPlayVisitGuard> VisitGuards { get; set; } = new(StringComparer.Ordinal);
    public string? Status { get; set; }

    public Ff7AutoPlayState Clone()
    {
        return JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(this)) ?? throw new InvalidDataException("Automatic play state could not be restored.");
    }

    public void Validate()
    {
        if (Version != 1 || Coverage is null || Coverage.Visits.Count > 4096 || Coverage.Choices.Count > 4096 || Coverage.Visits.Any(visit => visit.Events.Count > 4096))
        {
            throw new InvalidDataException("The automatic play observation state is unsupported or corrupt.");
        }
        _ = new AutoPlayPlanner<Ff7AutoPlayDeferral>(Coverage);
    }
}
