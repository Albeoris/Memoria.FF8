using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF7.Runtime;

/// <summary>Actual search work, separate from live gameplay callbacks and scheduling attempts.</summary>
public sealed record Ff7AutoPlayPlanningProgress(bool Pending, long PhysicalNodes, long DetachedNativeCallbacks, int NativeCandidates)
{
    public Ff7AutoPlayNativePlanningGroup[] NativeGroups { get; init; } = [];
    public string? NativePoseKey { get; init; }
}

public sealed record Ff7AutoPlayPlanningPose(int X4096, int Y4096, int Z4096, int Triangle, int Direction);
public sealed record Ff7AutoPlayNativePlanningGroup(string WaitMode, bool RequiresIntermediate, int Candidates, int StepsMin, int StepsMax, long BranchTickMin, long BranchTickMax, int DistinctTriangles, int PoseChangedFromSource, int PhysicalPending, float AdvisoryDistanceMin, float AdvisoryDistanceMax, string ClosestEvent, Ff7AutoPlayPlanningPose ClosestPose);

internal sealed record Ff7NativeCandidateSummary(Ff7NativePlanningWait WaitMode, bool RequiresIntermediate, string EventId, int Steps, long BranchTick, Ff7AutoPlayPlanningPose Pose, bool PoseChangedFromSource, bool PhysicalPending, float AdvisoryDistance);

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
            return new Ff7AutoPlayNativePlanningGroup(group.Key.WaitMode.ToString(), group.Key.RequiresIntermediate, group.Count(), group.Min(item => item.Steps), group.Max(item => item.Steps), group.Min(item => item.BranchTick), group.Max(item => item.BranchTick), group.Select(item => item.Pose.Triangle).Distinct().Count(), group.Count(item => item.PoseChangedFromSource), group.Count(item => item.PhysicalPending), group.Min(item => item.AdvisoryDistance), group.Max(item => item.AdvisoryDistance), closest.EventId, closest.Pose);
        }).ToArray();
    }
}
