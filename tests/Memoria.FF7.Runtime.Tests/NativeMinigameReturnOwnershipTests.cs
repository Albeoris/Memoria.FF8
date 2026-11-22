using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeMinigameReturnOwnershipTests
{
    internal static void Run()
    {
        Ff7AutoPlayActiveEvent active = new("source-visit", "line:3:entity:0:script:4", "source-condition", 3, 0, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, NavigationKey: "source-nav", SpatialDestinations: [324], TravelSelections: [new("choice", 2)], EntryGuardFingerprints: new Dictionary<string, string> { ["guard"] = "exact" });
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "source-program", 20, 5) };
        Ff7NativeScriptActivity dispatch = new(Dispatch: new(0, 1, 22, 1, 2, 6, 3, true, false, 10));
        Ff7AutoPlayActiveEvent source = Ff7AutoPlayExecutionOwners.Observe(active, 3, "source-program", 10, [dispatch], (entity, script) => entity == 1 && script == 2 ? 40 : throw new InvalidOperationException("Unexpected child entry."));
        source = JsonSerializer.Deserialize<Ff7AutoPlayActiveEvent>(JsonSerializer.Serialize(source))!;
        Check(!ReferenceEquals(source.EntryGuardFingerprints, active.EntryGuardFingerprints) && !ReferenceEquals(source.TravelSelections, active.TravelSelections), "The fixture did not reproduce cloned publication metadata.");
        Ff7AutoPlayExecutionProvenance provenance = source.ExecutionProvenance!;
        FieldHostRequest request = new(7, 1, 6, 44, new FieldMinigameCommand(new(324, 10, 20, 2, 64), 4));
        Ff7NativeMinigameReturn receipt = new(3, 4, 11, request, source);
        string before = JsonSerializer.Serialize(new { active, source, receipt });
        Ff7AutoPlayActiveEvent adopted = Read(active, receipt) ?? throw new InvalidOperationException("An authenticated minigame return lost its source activation.");
        Check(adopted.TransitionOwned && !adopted.Returned && !adopted.CompletionRecorded && adopted.ExecutionProvenance is { RootReturned: false, AcceptedTransitionTick: 11 }, "A real module return fabricated native RET or early source completion.");
        Check(adopted.ExecutionProvenance!.AcceptedTransition == request && adopted.ExecutionProvenance.Owners.SequenceEqual(source.ExecutionProvenance!.Owners), "Return adoption changed the original request or actual owner lineage.");
        Check(ReferenceEquals(adopted.EntryGuardFingerprints, active.EntryGuardFingerprints) && ReferenceEquals(adopted.SpatialDestinations, active.SpatialDestinations) && ReferenceEquals(adopted.TravelSelections, active.TravelSelections), "Return adoption replaced live planning collections with cloned source metadata.");

        Ff7AutoPlayState state = new() { SourceGoal = new(active.VisitId, "source-program", 3, active.NavigationKey!, active.EventId, active.ConditionFingerprint, active.Kind, 0, 4, 20, AcceptedEntry: 20) };
        string coverage = JsonSerializer.Serialize(state.Coverage);
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(state, adopted), "A return receipt completed its source before normal completion accounting.");
        Check(Ff7AutoPlayPolicy.CompleteSourceGoal(state, adopted with { CompletionRecorded = true }) && state.SourceGoal!.Completed, "Recorded native minigame return failed to complete the exact source goal.");
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(state, adopted with { CompletionRecorded = true }), "Repeated source completion manufactured additional progress.");
        Check(coverage == JsonSerializer.Serialize(state.Coverage) && !adopted.ExecutionProvenance.RootReturned && !adopted.Returned, "Source goal completion changed coverage or fabricated root RET.");

        Ff7AutoPlayActiveEvent returnedRoot = active with { Returned = true, ExecutionProvenance = active.ExecutionProvenance! with { RootReturned = true } };
        Ff7AutoPlayActiveEvent childAfterReturn = source with { Returned = true, ExecutionProvenance = provenance with { RootReturned = true, Owners = provenance.Owners.Where(owner => owner.ParentEntity != -1).ToArray() } };
        Ff7AutoPlayActiveEvent adoptedAfterReturn = Read(returnedRoot, receipt with { SourceActivation = childAfterReturn }) ?? throw new InvalidOperationException("A genuine child outliving its returned root lost minigame ownership.");
        Check(adoptedAfterReturn.Returned && adoptedAfterReturn.ExecutionProvenance!.RootReturned, "Module return erased a genuine earlier root RET.");

        foreach (Ff7NativeMinigameReturn invalid in new[]
        {
            receipt with { SourceGeneration = 2 },
            receipt with { SourceGeneration = long.MaxValue },
            receipt with { DestinationGeneration = 5 },
            receipt with { Tick = 4 },
            receipt with { Request = request with { Command = new FieldMinigameCommand(new(324, 10, 20, 2, 64), 3) } },
            receipt with { Request = request with { Command = new FieldTransitionCommand(324, 10, 20, 2, 64) } },
            receipt with { Request = request with { Entity = 2 } },
            receipt with { Request = request with { Priority = 5 } },
            receipt with { SourceActivation = source with { Accepted = false } },
            receipt with { SourceActivation = source with { VisitId = "foreign-visit" } },
            receipt with { SourceActivation = source with { NavigationKey = "foreign-nav" } },
            receipt with { SourceActivation = source with { EventId = "foreign-event" } },
            receipt with { SourceActivation = source with { ConditionFingerprint = "foreign-condition" } },
            receipt with { SourceActivation = source with { ExecutionProvenance = null } },
            receipt with { SourceActivation = source with { ExecutionProvenance = provenance with { ProgramIdentity = "foreign-program" } } },
            receipt with { SourceActivation = source with { ExecutionProvenance = provenance with { RootDispatchTick = 6 } } },
            receipt with { SourceActivation = source with { ExecutionProvenance = provenance with { Owners = provenance.Owners.Where(owner => owner.Entity != request.Entity).ToArray() } } },
            receipt with { SourceActivation = source with { ExecutionProvenance = provenance with { Owners = provenance.Owners.Select(owner => owner.Entity == request.Entity ? owner with { Queued = true } : owner).ToArray() } } }
        })
        {
            Check(Read(active, invalid) is null, "A foreign, unstarted or mismatched minigame receipt acquired source ownership.");
        }
        Check(Ff7AutoPlayPolicy.ReadCompletedMinigameReturn(active, receipt, 325, 4) is null && Ff7AutoPlayPolicy.ReadCompletedMinigameReturn(active, receipt, 324, 5) is null, "A return acquired ownership in a different actual field or generation.");
        foreach (Ff7AutoPlayActiveEvent replaced in new[]
        {
            active with { Accepted = false },
            active with { CompletionRecorded = true },
            active with { Generation = 4 },
            active with { EventId = "replacement" },
            active with { ConditionFingerprint = "replacement" },
            active with { ExecutionProvenance = active.ExecutionProvenance! with { RootDispatchTick = 6 } }
        })
        {
            Check(Read(replaced, receipt) is null, "A new active root lifetime borrowed a prior module return.");
        }
        Check(before == JsonSerializer.Serialize(new { active, source, receipt }), "Inspecting or accounting for a module return mutated source ownership or ordered native metadata.");
        Console.WriteLine("Owned native minigame returns preserve exact source lifetime without fabricated root RET.");
    }

    private static Ff7AutoPlayActiveEvent? Read(Ff7AutoPlayActiveEvent active, Ff7NativeMinigameReturn receipt) => Ff7AutoPlayPolicy.ReadCompletedMinigameReturn(active, receipt, 324, 4);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
