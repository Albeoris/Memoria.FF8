using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeQueuedMessageTests
{
    public static void Run()
    {
        RunOwnership(false);
        RunOwnership(true);
    }

    private static void RunOwnership(bool cold)
    {
        Ff7FieldResources field = NativeDialogueTests.Field([0x21, 255]);
        Ff7FieldHost host = new(field, (_, _) => 1, new()
        {
            Windows = [new(0, 48, 28, 212, 57), new(1, 48, 104, 212, 57)],
            BlinkStateInitialized = true
        });
        FieldHostRequest first = new(1, 0, 1, 104, new FieldDialogueCommand(1, 0));
        FieldHostRequest second = new(2, 1, 1, 104, new FieldDialogueCommand(0, 0));
        FieldHostRequest queued = new(3, 2, 1, 104, new FieldDialogueCommand(0, 0));
        for (int callback = 0; callback < 12; callback++)
        {
            Check(!host.Execute(first).Completed && !host.Execute(second).Completed, "Unconfirmed messages completed.");
        }
        Check(host.Process.Dialogues.All(dialogue => dialogue.AwaitingInput && dialogue.Presentation?.NativeState == 6), "Both window owners must reach their final confirmation wait.");
        Check(!host.Execute(queued).Completed, "A queued message acquired an occupied window.");
        host = RestoreIfCold(host, field, cold);
        CheckQueued(host, first, second, queued);

        host.CompleteDialogue(second.Id, 0);
        for (int callback = 0; callback < 8 && !Dialogue(host, second.Id).Completed; callback++)
        {
            Check(!host.Execute(queued).Completed, "Queued message completed while its predecessor was closing.");
            Check(!host.CanConfirmDialogue(queued.Id), "Queued message intercepted input while its predecessor was closing.");
            host.Execute(second);
            host = RestoreIfCold(host, field, cold);
            Check(host.CanConfirmDialogue(first.Id), "Closing one window blocked the independent window owner.");
        }
        Check(Dialogue(host, second.Id).Completed, "The original window owner did not finish closing.");
        Check(!host.CanConfirmDialogue(queued.Id), "An unpolled queued message became an input owner after release.");
        Check(!host.Execute(queued).Completed, "The newly acquired message skipped opening.");
        Check(Dialogue(host, queued.Id).Presentation?.NativeState == 1, "The queued request did not acquire the released window.");
        for (int callback = 0; callback < 12 && !Dialogue(host, queued.Id).AwaitingInput; callback++)
        {
            host.Execute(queued);
            host = RestoreIfCold(host, field, cold);
        }
        Check(Dialogue(host, queued.Id).AwaitingInput && host.CanConfirmDialogue(queued.Id), "The queued request did not become confirmable after acquiring its window.");
        Check(host.CanConfirmDialogue(first.Id), "The independent window owner was lost during queued-message admission.");
        host.CompleteDialogue(first.Id, 0);
        host.CompleteDialogue(queued.Id, 0);
        for (int callback = 0; callback < 8; callback++)
        {
            host.Execute(first);
            host.Execute(queued);
            host = RestoreIfCold(host, field, cold);
        }
        Check(host.Process.Dialogues.All(dialogue => dialogue.Completed), "The independent and newly admitted window owners did not complete.");
    }

    private static void CheckQueued(Ff7FieldHost host, FieldHostRequest first, FieldHostRequest second, FieldHostRequest queued)
    {
        Ff7Dialogue waiting = Dialogue(host, queued.Id);
        Check(waiting.Presentation?.NativeState == 0 && !waiting.AwaitingInput, "The occupied window advanced its queued request.");
        Check(!host.CanConfirmDialogue(queued.Id), "An unclaimed queued message must not intercept confirmation input.");
        Check(host.CanConfirmDialogue(first.Id) && host.CanConfirmDialogue(second.Id), "Queued ownership blocked an active window's confirmation.");
        Ff7Dialogue selected = host.Process.Dialogues.Last(dialogue => !dialogue.Completed && host.CanConfirmDialogue(dialogue.RequestId));
        Check(selected.RequestId == second.Id, "Input selection was starved by the later queued request.");
        host.CompleteDialogue(queued.Id, 0);
        Check(Dialogue(host, queued.Id) == waiting, "Confirmation changed an unclaimed queued request.");
    }

    private static Ff7FieldHost RestoreIfCold(Ff7FieldHost host, Ff7FieldResources field, bool cold)
    {
        return cold ? new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!) : host;
    }

    private static Ff7Dialogue Dialogue(Ff7FieldHost host, long requestId) => host.Process.Dialogues.Single(dialogue => dialogue.RequestId == requestId);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
