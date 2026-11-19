using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativePlanningHealthTests
{
    internal static void Run()
    {
        CheckCandidateSummaries();
        Console.WriteLine("Native planning health summary checks passed.");
    }

    private static void CheckCandidateSummaries()
    {
        Ff7AutoPlayPlanningProgress older = JsonSerializer.Deserialize<Ff7AutoPlayPlanningProgress>("{\"Pending\":true,\"PhysicalNodes\":3,\"DetachedNativeCallbacks\":5,\"NativeCandidates\":0}")!;
        Check(older.NativeGroups.Length == 0, "Older health data required new candidate summary metadata.");
        Ff7NativeCandidateSummaries summaries = new();
        summaries.Set(1, new("Portal", false, "one", 4, 10, new(100, 200, 0, 1, 32), true, true, 10));
        summaries.Set(2, new("Portal", false, "two", 6, 12, new(300, 400, 0, 2, 64), false, false, 20));
        summaries.Set(3, new("Wait", false, "three", 20, 26, new(500, 600, 0, 3, 96), true, true, 5));
        Ff7AutoPlayNativePlanningGroup[] groups = summaries.Read();
        Ff7AutoPlayNativePlanningGroup retained = groups.Single(group => group.WaitMode == "Portal");
        Check(groups.Length == 2 && retained.Candidates == 2 && retained.StepsMin == 4 && retained.StepsMax == 6 && retained.BranchTickMin == 10 && retained.BranchTickMax == 12 && retained.DistinctTriangles == 2 && retained.PoseChangedFromSource == 1 && retained.PhysicalPending == 1 && retained.AdvisoryDistanceMin == 10 && retained.AdvisoryDistanceMax == 20 && retained.ClosestEvent == "one" && retained.ClosestPose.Triangle == 1, "Candidate health grouped unrelated strategies or lost exact pending/pose facts.");
        string unchanged = JsonSerializer.Serialize(groups);
        string poseKey = summaries.ReadPoseKey()!;
        for (int poll = 0; poll < 100; poll++)
        {
            Ff7AutoPlayPlanningProgress view = older with { NativeGroups = summaries.Read() };
            Check(summaries.ReadPoseKey() == poseKey && view.PhysicalNodes == 3 && view.DetachedNativeCallbacks == 5 && JsonSerializer.Serialize(view.NativeGroups) == unchanged, "Read-only health polling advanced work or changed retained candidate facts.");
        }
        summaries.Set(1, new("Portal", false, "one", 9, 15, new(100, 200, 0, 1, 32), true, true, 10));
        Check(summaries.Read().Sum(group => group.Candidates) == 3, "A latest-only candidate update retained historical snapshots.");
        Check(summaries.ReadPoseKey() == poseKey, "Native ticks or planning step counters changed semantic pose health.");
        summaries.Set(1, new("Portal", false, "one", 9, 15, new(101, 200, 0, 1, 32), true, true, 10));
        Check(summaries.ReadPoseKey() != poseKey, "An actual fork pose change was absent from semantic health.");
        string moved = summaries.ReadPoseKey()!;
        summaries.Set(4, new("Portal", false, "one", 9, 15, new(101, 200, 0, 1, 32), true, true, 10));
        Check(summaries.ReadPoseKey() != moved, "A new active candidate was absent from semantic health.");
        summaries.Remove(4);
        Check(summaries.ReadPoseKey() == moved, "Disposed sibling metadata remained in the native pose key.");
        Ff7Line actualLine = new(0, 14, -100, 20, 0, 100, 20, 0, 0);
        Ff7AutoPlayNativeIdleFacts idle = new(false, false, false, false, true, default, actualLine);
        summaries.Set(3, new("Wait", false, "three", 20, 26, new(500, 600, 0, 3, 96), true, false, 5, idle));
        Ff7AutoPlayNativePlanningGroup idleGroup = summaries.Read().Single(group => group.WaitMode == "Wait");
        Check(idleGroup.NativeWaitReasons.GetValueOrDefault("selected-line-disabled") == 1 && idleGroup.ClosestState?.SelectedLine == actualLine && idleGroup.ClosestState.NativeInputDemands is null, "Idle health lost actual selected LINE facts or fabricated a native input analysis result.");
        string idleView = JsonSerializer.Serialize(idleGroup);
        Check(JsonSerializer.Serialize(summaries.Read().Single(group => group.WaitMode == "Wait")) == idleView, "Reading idle facts changed candidate work.");
        summaries.Remove(2);
        Check(summaries.Read().Single(group => group.WaitMode == "Portal").Candidates == 1, "Retired candidate health remained active.");
        summaries.Remove(1);
        summaries.Remove(3);
        Check(summaries.Read().Length == 0 && summaries.ReadPoseKey() is null, "Disposed candidate groups leaked beyond their workset.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
