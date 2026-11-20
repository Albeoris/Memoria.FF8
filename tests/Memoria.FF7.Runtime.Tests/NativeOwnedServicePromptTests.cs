using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeOwnedServicePromptTests
{
    private const long Generation = 7;
    private const string Program = "owned-prompt-program";

    public static void Run()
    {
        RootPromptRequiresItsActualExecutionSlot();
        ReturnedRootRetainsItsRecordedChildPrompt();
        MissingProvenanceUsesOnlyExistingSynchronousFacts();
        AmbiguousPromptsRequireTheActualRequestIdentity();
        Console.WriteLine("PASS exact native service prompt ownership and legacy synchronous scope");
    }

    private static int Entry(int entity, int script)
    {
        return 20 + entity * 100 + script * 4;
    }

    private static Ff7AutoPlayActiveEvent Root()
    {
        Ff7AutoPlayActiveEvent active = new("ff7:1:" + Program, "actor:0:script:1", "condition", Generation, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "before", Accepted: true, NavigationKey: "component");
        return active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, Program, Entry(0, 1), 10) };
    }

    private static FieldVmSnapshot Vm(params (int Entity, int Priority, int Pc)[] executing)
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 3).Select(entity => new FieldEntitySnapshot(Entry(entity, 1), 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        foreach (var owner in executing)
        {
            entities[owner.Entity] = entities[owner.Entity] with { Priority = owner.Priority, ProgramCounter = owner.Pc };
        }
        return new(1, FieldMachine.SemanticProfile, Program, 10, true, new byte[1280], new byte[256], entities);
    }

    private static FieldHostRequest Prompt(int entity, int priority, int pc, long id = 10)
    {
        return new(id, entity, priority, pc, new FieldDialogueCommand(0, 1, FirstChoice: 1, LastChoice: 2));
    }

    private static Ff7SessionSave Source(FieldVmSnapshot vm, params FieldHostRequest[] requests)
    {
        return new()
        {
            ResourceProfile = "test",
            Language = "test",
            FieldName = "field",
            FieldGeneration = Generation,
            Progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] },
            Vm = vm with { PendingRequests = requests },
            Process = new()
        };
    }

    private static void RootPromptRequiresItsActualExecutionSlot()
    {
        Ff7AutoPlayActiveEvent active = Root();
        FieldHostRequest request = Prompt(0, 1, 40);
        Ff7SessionSave source = Source(Vm((0, 1, 40)), request);
        string before = JsonSerializer.Serialize(new { active, source });
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active) == request, "An actual priority-one root prompt lost its owner.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active, request.Id) == request, "The exact current prompt identity was rejected.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active, request.Id + 1) is null, "Another prompt request borrowed ownership.");
        foreach (FieldHostRequest wrong in new[]
        {
            request with { Entity = 1 },
            request with { Priority = 6 },
            request with { ProgramCounter = 41 },
            request with { Command = new FieldDialogueCommand(0, 1) },
            request with { Command = new FieldCameraCommand(FieldCameraOperation.Wait) }
        })
        {
            Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source with { Vm = source.Vm with { PendingRequests = [wrong] } }, active) is null, "An unrelated slot, PC or command authenticated a choice prompt.");
        }
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source with { Vm = source.Vm with { PendingRequests = null } }, active) is null, "A prompt was invented without a pending native request.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source with { Vm = Vm((0, 7, 40)) with { PendingRequests = [request] } }, active) is null, "An ambient execution inherited the old priority-one prompt.");
        foreach (Ff7AutoPlayActiveEvent changed in new[]
        {
            active with { Accepted = false },
            active with { Generation = Generation + 1 },
            active with { VisitId = "another-source" },
            active with { EventId = "another-event" },
            active with { Kind = Ff7AutoPlayTargetKind.ActorContact },
            active with { Entity = 1 },
            active with { Script = 2 },
            active with { ConditionFingerprint = "changed-condition" },
            active with { ExecutionProvenance = active.ExecutionProvenance! with { Owners = [] } }
        })
        {
            Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, changed) is null, "Changed or cleared provenance regained an unrelated service prompt.");
        }
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source with { FieldGeneration = Generation + 1 }, active) is null, "Another field allocation inherited prompt ownership.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source with { Vm = source.Vm with { ProgramIdentity = "different-program" } }, active) is null, "Another native program inherited prompt ownership.");
        Check(before == JsonSerializer.Serialize(new { active, source }), "Reading service ownership changed its source state.");
    }

    private static Ff7AutoPlayActiveEvent ChildOwner(bool returned)
    {
        Ff7AutoPlayActiveEvent active = Root();
        List<Ff7NativeScriptActivity> activities = [new(Dispatch: new(0, 1, 40, 1, 2, 6, 2, true, false, 10))];
        if (returned)
        {
            activities.Add(new(Return: new(0, 1, 44, 10)));
        }
        active = Ff7AutoPlayExecutionOwners.Observe(active, Generation, Program, 10, activities, Entry);
        return active with { Returned = returned };
    }

    private static void ReturnedRootRetainsItsRecordedChildPrompt()
    {
        Ff7AutoPlayActiveEvent active = ChildOwner(returned: true);
        FieldHostRequest request = Prompt(1, 6, 150);
        Ff7SessionSave source = Source(Vm((1, 6, 150)), request);
        string before = JsonSerializer.Serialize(new { active, source });
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active, request.Id) == request, "A returned root lost its actual live asynchronous child's service prompt.");
        Ff7AutoPlayActiveEvent ended = Ff7AutoPlayExecutionOwners.Observe(active, Generation, Program, 11, [new(Return: new(1, 6, 154, 11))], Entry);
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, ended) is null, "A later occupant of the same child slot inherited the returned execution's prompt.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active with { ExecutionProvenance = null }) is null, "A missing old-save record invented asynchronous ancestry after root return.");
        Check(before == JsonSerializer.Serialize(new { active, source }), "Reading a child service prompt changed native or observational state.");
    }

    private static void MissingProvenanceUsesOnlyExistingSynchronousFacts()
    {
        Ff7AutoPlayActiveEvent active = Root() with { ExecutionProvenance = null };
        FieldVmSnapshot vm = Vm((0, 1, 40), (1, 6, 150));
        vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting;
        vm.Entities[1].SyncOwners[6] = 0;
        FieldHostRequest request = Prompt(1, 6, 150);
        Ff7SessionSave source = Source(vm, request);
        string before = JsonSerializer.Serialize(new { active, source });
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active) == request, "An older save lost its proved synchronous child prompt.");
        FieldVmSnapshot unrelated = Vm((0, 1, 40), (1, 6, 150));
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source with { Vm = unrelated with { PendingRequests = [request] } }, active) is null, "An unrelated child execution was treated as legacy synchronous work.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, Root() with { ExecutionProvenance = Root().ExecutionProvenance! with { Owners = [] } }) is null, "Legacy synchronous inference resurrected explicitly cleared provenance.");
        Check(before == JsonSerializer.Serialize(new { active, source }), "Legacy prompt inspection changed saved synchronous facts.");
    }

    private static void AmbiguousPromptsRequireTheActualRequestIdentity()
    {
        Ff7AutoPlayActiveEvent active = ChildOwner(returned: false);
        FieldHostRequest root = Prompt(0, 1, 40);
        FieldHostRequest child = Prompt(1, 6, 150, id: 11);
        Ff7SessionSave source = Source(Vm((0, 1, 40), (1, 6, 150)), root, child);
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active) is null, "Two real owned prompts were guessed into one service source.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(source, active, child.Id) == child, "An explicit actual child prompt could not disambiguate owned requests.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
