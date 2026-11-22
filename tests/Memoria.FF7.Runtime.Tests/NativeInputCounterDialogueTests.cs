using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeInputCounterDialogueTests
{
    internal static void Run()
    {
        FieldEntitySnapshot Entity(int pc, int priority) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest message = new(7, 2, 6, 64, new FieldDialogueCommand(1, 10));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity(0, 7), Entity(8, 6), Entity(64, 6)], NextRequestId: 8, PendingRequests: [message]);
        Ff7Dialogue dialogue = new(7, 1, 10, null, null, 0, AwaitingInput: true, Presentation: new(6, 100, 50));
        Ff7FieldProcess process = new() { PlayerLocked = true, MenuDisabled = true, Windows = [new(1, 0, 0, 100, 50, Flags: 1)], Dialogues = [dialogue] };
        Ff7AutoPlayActiveEvent active = new("visit", "source", "condition", 1, 0, 4, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 0, 10) with { RootReturned = true, Owners = [new(1, 6, 3, 8, 0, 1, 4, 11, false), new(2, 6, 4, 64, 1, 6, 12, 12, false)] } };
        string before = JsonSerializer.Serialize(new { vm, process, active });
        bool Read(FieldVmSnapshot? value = null, Ff7FieldProcess? state = null, Ff7AutoPlayActiveEvent? owner = null, long generation = 1, int entity = 1, int priority = 6, Ff7Dialogue? prompt = null) => Ff7AutoPlayScriptedInput.IsOwnedCounterDialogue(value ?? vm, state ?? process, owner ?? active, generation, entity, priority, prompt ?? dialogue);
        Check(Read(), "The actual counter worker lost its owned persistent hint dialogue.");
        Check(!Read(generation: 2) && !Read(entity: 0) && !Read(priority: 5) && !Read(owner: active with { Accepted = false }) && !Read(owner: active with { ExecutionProvenance = null }), "A foreign source, generation or thread received counter HUD authority.");
        Check(!Read(prompt: dialogue with { RequestId = 8 }) && !Read(prompt: dialogue with { AwaitingInput = false }) && !Read(prompt: dialogue with { FirstChoice = 1, LastChoice = 2 }) && !Read(prompt: dialogue with { Presentation = new(2, 100, 50) }), "A stale, revealing or choice prompt received counter HUD authority.");
        Check(!Read(state: process with { Windows = [new(1, 0, 0, 100, 50)] }) && !Read(state: process with { Menu = new(9, 7, 0) }) && !Read(state: process with { GlobalCommand = Ff7GlobalCommand.Battle }), "A normal dialogue or pending modal operation received counter HUD authority.");
        FieldEntitySnapshot[] foreign = vm.Entities.ToArray();
        foreign[2] = foreign[2] with { ProgramCounter = 65 };
        Check(!Read(value: vm with { Entities = foreign }) && !Read(value: vm with { PendingRequests = [] }), "A stale or missing VM message wait received counter HUD authority.");
        Ff7AutoPlayExecutionProvenance provenance = active.ExecutionProvenance!;
        Check(!Read(owner: active with { ExecutionProvenance = provenance with { Owners = provenance.Owners.Select(child => child.Entity == 2 ? child with { ParentEntity = 0 } : child).ToArray() } }), "An unrelated sibling message was attributed to the current counter worker.");
        Check(before == JsonSerializer.Serialize(new { vm, process, active }), "Counter HUD qualification changed native state or ownership.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
