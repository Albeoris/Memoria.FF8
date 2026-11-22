using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeDialoguePresentationReadinessTests
{
    internal static void Run()
    {
        FieldEntitySnapshot owner = new(20, 1, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(7, 0, 1, 20, new FieldDialogueCommand(0, 42));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "dialogue-fixture", 1, true, new byte[1280], new byte[256], [owner], PendingRequests: [request]);
        Ff7Dialogue dialogue = new(7, 0, 42, null, null, 0, Presentation: new(2, 160, 48));
        Ff7FieldProcess process = new() { Dialogues = [dialogue], Windows = [new(0, 0, 0, 160, 48)] };
        bool Ready(FieldVmSnapshot? current = null, Ff7Dialogue? actual = null) => Ff7AutoPlayNativeReadiness.HasAdvancingDialoguePresentation(current ?? vm, process with { Dialogues = [actual ?? dialogue] });
        string before = JsonSerializer.Serialize(new { vm, process });
        foreach (int state in new[] { 0, 1, 2, 5, 7, 8 })
        {
            Check(Ready(actual: dialogue with { Presentation = dialogue.Presentation! with { NativeState = state } }), "An exact current autonomous native dialogue phase lost readiness.");
        }
        Check(Ready(actual: dialogue with { WaitTicks = 3, Presentation = dialogue.Presentation! with { NativeState = 3 } }), "A real native dialogue countdown lost readiness.");
        Check(!Ready(actual: dialogue with { Presentation = dialogue.Presentation! with { NativeState = 3 } }), "A zero-countdown stable dialogue acquired readiness.");
        foreach (int state in new[] { -1, 4, 6, 13, 14, 99 })
        {
            Check(!Ready(actual: dialogue with { Presentation = dialogue.Presentation! with { NativeState = state } }), "A stable, input-dependent or unsupported native phase acquired autonomous readiness.");
        }
        foreach (Ff7Dialogue invalid in new[]
        {
            dialogue with { RequestId = 8 },
            dialogue with { Window = 1 },
            dialogue with { Text = 43 },
            dialogue with { FirstChoice = 1, LastChoice = 2 },
            dialogue with { Completed = true },
            dialogue with { AwaitingInput = true },
            dialogue with { Presentation = null }
        })
        {
            Check(!Ready(actual: invalid), "A stale, mismatched, completed or input-waiting dialogue inherited the current presentation owner.");
        }
        foreach (FieldHostRequest invalid in new[]
        {
            request with { Id = 8 },
            request with { Entity = 1 },
            request with { Entity = -1 },
            request with { Priority = 6 },
            request with { ProgramCounter = 21 },
            request with { Command = new FieldDialogueCommand(1, 42) },
            request with { Command = new FieldDialogueCommand(0, 43) },
            request with { Command = new FieldDialogueCommand(0, 42, 1, 2) },
            request with { Command = new FieldActorMove(1, 2) }
        })
        {
            Check(!Ready(vm with { PendingRequests = [invalid] }), "An unrelated or suspended native request authenticated dialogue presentation.");
        }
        Check(!Ready(vm with { PendingRequests = [] }), "An unowned presentation acquired readiness.");
        Check(!Ready(vm with { Entities = [owner with { ProgramCounter = 21 }] }), "A resumed different instruction inherited stale dialogue debt.");
        Check(!Ready(vm with { Entities = [owner with { Priority = 6 }] }), "A preempted priority inherited stale dialogue debt.");
        foreach (int state in new[] { 4, 6, 13, 14 })
        {
            Ff7Dialogue confirmed = dialogue with { AwaitingInput = true, Presentation = dialogue.Presentation! with { NativeState = state, ConfirmPending = true } };
            Check(Ready(actual: confirmed), "A real confirmed native input could not receive its completion callback.");
            Check(!Ready(actual: confirmed with { Presentation = confirmed.Presentation! with { ConfirmPending = false } }), "An unconfirmed input wait acquired readiness.");
            Check(!Ff7AutoPlayNativeReadiness.HasAdvancingDialoguePresentation(vm, process with { Dialogues = [confirmed], Windows = [] }), "A missing native window authenticated pending confirmation.");
            Check(!Ff7AutoPlayNativeReadiness.HasAdvancingDialoguePresentation(vm, process with { Dialogues = [confirmed], Windows = [new(0, 0, 0, 160, 48, Flags: 1)] }), "A persistent HUD window authenticated pending confirmation.");
            Check(!Ready(vm with { Entities = [owner with { ProgramCounter = 21 }] }, confirmed), "A stale native owner authenticated pending confirmation.");
        }
        FieldDialogueCommand choice = new(0, 42, 1, 2);
        Check(Ready(vm with { PendingRequests = [request with { Command = choice }] }, dialogue with { FirstChoice = 1, LastChoice = 2 }), "An owned choice window could not finish its autonomous reveal before input.");
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Readiness mutated native VM, request or dialogue state.");
        Console.WriteLine("Exact native dialogue owners advance autonomous presentation without admitting input waits or stale requests.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
