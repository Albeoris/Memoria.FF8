using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeTemporalPlanningTests
{
    internal static void Run()
    {
        Ff7AutoplayInput requested = new(1, -1, Held: 32, Talk: true, Run: false);
        Check(Ff7AutoplayDriver.ReadNativePlanningWaitInput(Ff7NativePlanningWait.RetainPhysicalSearch, true, requested) is null, "A retained static search advanced native time while still pending.");
        Check(Ff7AutoplayDriver.ReadNativePlanningWaitInput(Ff7NativePlanningWait.AdvanceNativeTime, true, requested) == default(Ff7AutoplayInput), "A temporal pending edge installed an unproven movement/interaction input.");
        foreach (Ff7NativePlanningWait mode in Enum.GetValues<Ff7NativePlanningWait>())
        {
            Check(Ff7AutoplayDriver.ReadNativePlanningWaitInput(mode, false, requested) == requested, "A committable native input changed between planning strategies.");
        }
        CheckNativeWaitAndFairDisposal();
        CheckCandidateSummaries();
        Console.WriteLine("Temporal native planning wait, real owner completion and fair static retention checks passed.");
    }

    private static void CheckNativeWaitAndFairDisposal()
    {
        Ff7FieldHost sourceHost = new(NativeDialogueTests.Field(), (_, _) => 1, new() { PlayerLocked = true });
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram program = new([0, 0x24, 3, 0, 0x33, 0, 0], [entries]);
        FieldMachine source = new(program, host: sourceHost);
        source.Initialize();
        Check(source.TryRequestScript(0, 1), "The authored native interaction owner was not admitted.");
        string unchanged = JsonSerializer.Serialize(source.Capture());
        Ff7FieldHost branchHost = new(NativeDialogueTests.Field(), (_, _) => 1, sourceHost.Capture());
        FieldMachine branch = FieldMachine.Restore(program, source.Capture(), branchHost);
        int ownerEffects = 0;
        bool returned = false;
        branchHost.NativeOperationExecuted += (request, reply) =>
        {
            Check(request.Entity == 0 && request.Priority == 1 && request.ProgramCounter == 4 && request.Command is FieldControlCommand { Property: FieldControlKind.PlayerLocked, Value: 0 } && reply.Completed, "A temporal callback credited an unrelated host owner.");
            ownerEffects++;
        };
        branch.ScriptReturned += item => returned |= item.Entity == 0 && item.Priority == 1 && item.ProgramCounter == 6;
        int staticPolls = 0;
        int temporalCallbacks = 0;
        int disposed = 0;
        List<Ff7AutoplayInput> recorded = [];
        IEnumerable<int> Static()
        {
            try
            {
                while (true)
                {
                    yield return 0;
                    staticPolls++;
                    Check(Ff7AutoplayDriver.ReadNativePlanningWaitInput(Ff7NativePlanningWait.RetainPhysicalSearch, true, new(1, 0)) is null, "The static sibling lost its pending physical search.");
                }
            }
            finally
            {
                disposed++;
            }
        }
        IEnumerable<int> Temporal()
        {
            using Ff7NativeRouteClockWitness clock = new(branch);
            try
            {
                while (!returned)
                {
                    yield return 0;
                    Ff7AutoplayInput? neutral = Ff7AutoplayDriver.ReadNativePlanningWaitInput(Ff7NativePlanningWait.AdvanceNativeTime, true, new(1, 0));
                    Check(neutral == default(Ff7AutoplayInput), "A temporal branch used the unverified physical input.");
                    recorded.Add(neutral!.Value);
                    branch.AdvanceTick();
                    temporalCallbacks++;
                    Check(clock.Independent, "The authored temporal wait bypassed native clock-dependency authentication.");
                }
            }
            finally
            {
                disposed++;
            }
        }
        using IEnumerator<int> cursor = Ff7AutoplayDriver.EnumerateFairNativeCandidates([Static(), Temporal()], () => returned).GetEnumerator();
        int scheduler = 0;
        while (!returned && scheduler++ < 64)
        {
            long before = branch.Tick;
            Check(cursor.MoveNext() || returned, "A pending static sibling became a terminal negative before the native wait completed.");
            Check(branch.Tick - before is 0 or 1, "One planning allowance advanced more than one native callback.");
        }
        cursor.Dispose();
        Check(returned && ownerEffects == 1 && !branchHost.Process.PlayerLocked && temporalCallbacks > 1 && staticPolls > 1 && disposed == 2, $"Temporal wait incomplete: returned={returned}, effects={ownerEffects}, locked={branchHost.Process.PlayerLocked}, callbacks={temporalCallbacks}, staticPolls={staticPolls}, disposed={disposed}, pc={branch.Capture().Entities[0].ProgramCounter}, priority={branch.Capture().Entities[0].Priority}.");
        Check(recorded.Count == temporalCallbacks && recorded.All(input => input == default) && JsonSerializer.Serialize(source.Capture()) == unchanged && sourceHost.Process.PlayerLocked, "Recorded neutral timing changed its immutable source or omitted native callbacks.");
    }

    private static void CheckCandidateSummaries()
    {
        Ff7AutoPlayPlanningProgress older = JsonSerializer.Deserialize<Ff7AutoPlayPlanningProgress>("{\"Pending\":true,\"PhysicalNodes\":3,\"DetachedNativeCallbacks\":5,\"NativeCandidates\":0}")!;
        Check(older.NativeGroups.Length == 0, "Older health data required new candidate summary metadata.");
        Ff7NativeCandidateSummaries summaries = new();
        summaries.Set(1, new(Ff7NativePlanningWait.RetainPhysicalSearch, false, "one", 4, 10, new(100, 200, 0, 1, 32), true, true, 10));
        summaries.Set(2, new(Ff7NativePlanningWait.RetainPhysicalSearch, false, "two", 6, 12, new(300, 400, 0, 2, 64), false, false, 20));
        summaries.Set(3, new(Ff7NativePlanningWait.AdvanceNativeTime, false, "three", 20, 26, new(500, 600, 0, 3, 96), true, true, 5));
        Ff7AutoPlayNativePlanningGroup[] groups = summaries.Read();
        Ff7AutoPlayNativePlanningGroup retained = groups.Single(group => group.WaitMode == nameof(Ff7NativePlanningWait.RetainPhysicalSearch));
        Check(groups.Length == 2 && retained.Candidates == 2 && retained.StepsMin == 4 && retained.StepsMax == 6 && retained.BranchTickMin == 10 && retained.BranchTickMax == 12 && retained.DistinctTriangles == 2 && retained.PoseChangedFromSource == 1 && retained.PhysicalPending == 1 && retained.AdvisoryDistanceMin == 10 && retained.AdvisoryDistanceMax == 20 && retained.ClosestEvent == "one" && retained.ClosestPose.Triangle == 1, "Candidate health grouped unrelated strategies or lost exact pending/pose facts.");
        string unchanged = JsonSerializer.Serialize(groups);
        string poseKey = summaries.ReadPoseKey()!;
        for (int poll = 0; poll < 100; poll++)
        {
            Ff7AutoPlayPlanningProgress view = older with { NativeGroups = summaries.Read() };
            Check(summaries.ReadPoseKey() == poseKey && view.PhysicalNodes == 3 && view.DetachedNativeCallbacks == 5 && JsonSerializer.Serialize(view.NativeGroups) == unchanged, "Read-only health polling advanced work or changed retained candidate facts.");
        }
        summaries.Set(1, new(Ff7NativePlanningWait.RetainPhysicalSearch, false, "one", 9, 15, new(100, 200, 0, 1, 32), true, true, 10));
        Check(summaries.Read().Sum(group => group.Candidates) == 3, "A latest-only candidate update retained historical snapshots.");
        Check(summaries.ReadPoseKey() == poseKey, "Native ticks or planning step counters changed semantic pose health.");
        summaries.Set(1, new(Ff7NativePlanningWait.RetainPhysicalSearch, false, "one", 9, 15, new(101, 200, 0, 1, 32), true, true, 10));
        Check(summaries.ReadPoseKey() != poseKey, "An actual fork pose change was absent from semantic health.");
        string moved = summaries.ReadPoseKey()!;
        summaries.Set(4, new(Ff7NativePlanningWait.RetainPhysicalSearch, false, "one", 9, 15, new(101, 200, 0, 1, 32), true, true, 10));
        Check(summaries.ReadPoseKey() != moved, "A new active candidate was absent from semantic health.");
        summaries.Remove(4);
        Check(summaries.ReadPoseKey() == moved, "Disposed sibling metadata remained in the native pose key.");
        Ff7Line actualLine = new(0, 14, -100, 20, 0, 100, 20, 0, 0);
        Ff7AutoPlayNativeIdleFacts idle = new(false, false, false, false, true, default, actualLine);
        summaries.Set(3, new(Ff7NativePlanningWait.AdvanceNativeTime, false, "three", 20, 26, new(500, 600, 0, 3, 96), true, false, 5, idle));
        Ff7AutoPlayNativePlanningGroup idleGroup = summaries.Read().Single(group => group.WaitMode == nameof(Ff7NativePlanningWait.AdvanceNativeTime));
        Check(idleGroup.NativeWaitReasons.GetValueOrDefault("selected-line-disabled") == 1 && idleGroup.ClosestState?.SelectedLine == actualLine && idleGroup.ClosestState.NativeInputDemands is null, "Idle health lost actual selected LINE facts or fabricated a native input analysis result.");
        string idleView = JsonSerializer.Serialize(idleGroup);
        Check(JsonSerializer.Serialize(summaries.Read().Single(group => group.WaitMode == nameof(Ff7NativePlanningWait.AdvanceNativeTime))) == idleView, "Reading idle facts changed candidate work.");
        summaries.Remove(2);
        Check(summaries.Read().Single(group => group.WaitMode == nameof(Ff7NativePlanningWait.RetainPhysicalSearch)).Candidates == 1, "Retired candidate health remained active.");
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
