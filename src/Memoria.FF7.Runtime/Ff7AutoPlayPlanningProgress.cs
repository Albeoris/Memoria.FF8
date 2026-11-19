using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF7.Runtime;

/// <summary>Actual search work, separate from live gameplay callbacks and scheduling attempts.</summary>
public sealed record Ff7AutoPlayPlanningProgress(bool Pending, long PhysicalNodes, long DetachedNativeCallbacks, int NativeCandidates)
{
    public Ff7AutoPlayNativePlanningGroup[] NativeGroups { get; init; } = [];
    public string? NativePoseKey { get; init; }
    public long NativeGraphFrontier { get; init; }
    public long NativeGraphVisited { get; init; }
    public long NativeGraphExpanded { get; init; }
}

public sealed record Ff7AutoPlayPlanningPose(int X4096, int Y4096, int Z4096, int Triangle, int Direction);
public sealed record Ff7AutoPlayNativePlanningGroup(string WaitMode, bool RequiresIntermediate, int Candidates, int StepsMin, int StepsMax, long BranchTickMin, long BranchTickMax, int DistinctTriangles, int PoseChangedFromSource, int PhysicalPending, float AdvisoryDistanceMin, float AdvisoryDistanceMax, string ClosestEvent, Ff7AutoPlayPlanningPose ClosestPose)
{
    public IReadOnlyDictionary<string, int> NativeWaitReasons { get; init; } = new Dictionary<string, int>();
    public Ff7AutoPlayNativeIdleFacts? ClosestState { get; init; }
}

public sealed record Ff7AutoPlayNativeIdleFacts(bool PlayerLocked, bool Joining, bool LadderIncomplete, bool LadderCompleted, bool SelectedLineDisabled, Ff7AutoplayInput LastInput, Ff7Line? SelectedLine, int? NativeInputDemands = null);

internal sealed record Ff7NativeCandidateSummary(string WaitMode, bool RequiresIntermediate, string EventId, int Steps, long BranchTick, Ff7AutoPlayPlanningPose Pose, bool PoseChangedFromSource, bool PhysicalPending, float AdvisoryDistance, Ff7AutoPlayNativeIdleFacts? IdleState = null);

internal sealed class Ff7NativeCandidateSummaries
{
    private readonly Dictionary<long, Ff7NativeCandidateSummary> entries = [];

    internal void Set(long id, Ff7NativeCandidateSummary summary) => entries[id] = summary;
    internal void Remove(long id) => entries.Remove(id);

    internal string? ReadPoseKey()
    {
        return entries.Count == 0 ? null : Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries.OrderBy(item => item.Key).Select(item => new { Id = item.Key, item.Value.WaitMode, item.Value.RequiresIntermediate, item.Value.EventId, item.Value.Pose }))));
    }

    internal Ff7AutoPlayNativePlanningGroup[] Read()
    {
        return entries.Values.GroupBy(item => (item.WaitMode, item.RequiresIntermediate)).OrderBy(group => group.Key.WaitMode).ThenBy(group => group.Key.RequiresIntermediate).Select(group =>
        {
            Ff7NativeCandidateSummary closest = group.MinBy(item => item.AdvisoryDistance)!;
            return new Ff7AutoPlayNativePlanningGroup(group.Key.WaitMode.ToString(), group.Key.RequiresIntermediate, group.Count(), group.Min(item => item.Steps), group.Max(item => item.Steps), group.Min(item => item.BranchTick), group.Max(item => item.BranchTick), group.Select(item => item.Pose.Triangle).Distinct().Count(), group.Count(item => item.PoseChangedFromSource), group.Count(item => item.PhysicalPending), group.Min(item => item.AdvisoryDistance), group.Max(item => item.AdvisoryDistance), closest.EventId, closest.Pose) { ClosestState = closest.IdleState, NativeWaitReasons = group.GroupBy(WaitReason).ToDictionary(reason => reason.Key, reason => reason.Count(), StringComparer.Ordinal) };
        }).ToArray();
    }
    private static string WaitReason(Ff7NativeCandidateSummary summary)
    {
        return (summary.PhysicalPending, summary.IdleState) switch
        {
            (true, _) => "physical-pending",
            (_, { LadderIncomplete: true }) => "ladder-incomplete",
            (_, { LadderCompleted: true }) => "ladder-completed",
            (_, { PlayerLocked: true }) => "player-lock",
            (_, { Joining: true }) => "join",
            (_, { SelectedLineDisabled: true }) => "selected-line-disabled",
            (_, { NativeInputDemands: > 0 }) => "native-input-demand",
            (_, { LastInput: var input }) when input.Horizontal != 0 || input.Vertical != 0 || input.Held != 0 || input.Talk => "move",
            _ => "neutral"
        };
    }

}
