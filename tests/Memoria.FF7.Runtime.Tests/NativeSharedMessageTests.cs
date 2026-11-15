using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSharedMessageTests
{
    public static void Run()
    {
        var field = NativeDialogueTests.Field([0x21, 255]);
        FieldHostRequest suspended = new(1, 16, 6, 0x0C7A, new FieldDialogueCommand(0, 0));
        FieldHostRequest interrupting = new(2, 16, 0, 0x0C8F, new FieldDialogueCommand(0, 0));
        Ff7FieldProcess process = new()
        {
            Windows = [new(0, 48, 28, 212, 57)],
            Dialogues = [new(suspended.Id, 0, 0, null, null, 0, TokenPosition: 1, AwaitingInput: true, Presentation: new(6, 212, 57))],
            Receipts = [new(suspended, new(false))]
        };
        Ff7FieldHost host = new(field, (_, _) => 1, process);
        Check(!host.Execute(interrupting).Completed, "A same-entity MESSAGE at another priority must poll the shared physical window without completing its waiting confirmation.");
        Check(host.Process.Dialogues.Single(dialogue => dialogue.RequestId == suspended.Id) is { Completed: false, TokenPosition: 1, Presentation.NativeState: 6 } && host.Process.Dialogues.Single(dialogue => dialogue.RequestId == interrupting.Id).Presentation?.NativeState == 0, "The shared MESSAGE poll must retain the original physical cursor rather than opening a second presentation.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        foreach (Ff7FieldHost current in new[] { host, cold })
        {
            Check(current.CanConfirmDialogue(suspended.Id) && !current.CanConfirmDialogue(interrupting.Id), "Input must address the one physical window while the second request retains no presentation.");
            current.CompleteDialogue(suspended.Id, 0);
            for (int callback = 0; callback < 4; callback++)
            {
                bool complete = current.Execute(interrupting).Completed;
                Check(complete == (callback == 3), "The same-entity poll must preserve the native four-callback close cadence and complete only its current request.");
            }
            Ff7FieldProcess closed = current.Capture();
            Check(closed.Dialogues.Single(dialogue => dialogue.RequestId == suspended.Id) is { Completed: false, TokenPosition: 0, Presentation.NativeState: 0 }, "Closing the shared physical window must not complete or retain an exhausted cursor for the suspended MESSAGE request.");
            Check(closed.Dialogues.Single(dialogue => dialogue.RequestId == interrupting.Id).Completed && !closed.Receipts.Single(receipt => receipt.Request.Id == suspended.Id).Reply.Completed && closed.Receipts.Single(receipt => receipt.Request.Id == interrupting.Id).Reply.Completed, "Only the currently polling priority's receipt may finish when the shared window closes.");
            Check(!current.Execute(suspended).Completed && current.Process.Dialogues.Single(dialogue => dialogue.RequestId == suspended.Id) is { TokenPosition: 0, Presentation.NativeState: 1 }, "A still-pending suspended MESSAGE must initialize its own message normally when resumed after the shared window was released.");
            string beforeCompletedPoll = JsonSerializer.Serialize(current.Capture());
            Check(current.Execute(interrupting).Completed && JsonSerializer.Serialize(current.Capture()) == beforeCompletedPoll, "An already completed interrupting request must not replay or touch the resumed physical window.");
        }
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The shared entity/window continuation changed through an independent cold restore.");
        FieldHostRequest foreign = new(3, 17, 0, 0x0C8F, new FieldDialogueCommand(0, 0));
        Ff7FieldHost foreignHost = new(field, (_, _) => 1, process);
        Check(!foreignHost.Execute(foreign).Completed && foreignHost.Process.Dialogues.Single(dialogue => dialogue.RequestId == suspended.Id).Presentation?.NativeState == 6, "A different native entity must wait without advancing another entity's physical window.");
        Console.WriteLine("PASS same-entity shared MESSAGE cursor, distinct priority completion, native close cadence, suspended request and cold restore");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
