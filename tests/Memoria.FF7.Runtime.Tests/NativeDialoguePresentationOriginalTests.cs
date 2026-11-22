using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDialoguePresentationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DIALOGUE_PRESENTATION_SAVE")!;
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original, false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), source);
        long request = source.Process.Dialogues.Single(dialogue => !dialogue.Completed).RequestId;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("An owned native dialogue does not require an exploration anchor."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && timer.Elapsed.TotalSeconds < 15 && controller.Steps < 512)
        {
            controller.Advance(1);
            if (!(session.Machine.Capture().PendingRequests ?? []).Any(pending => pending.Id == request))
            {
                break;
            }
        }
        if (controller.Error is not null || session.Machine.Tick <= source.Vm.Tick || (session.Machine.Capture().PendingRequests ?? []).Any(pending => pending.Id == request) || !SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException($"Owned dialogue presentation did not advance through its real host completion: steps={controller.Steps}, tick={session.Machine.Tick}, error={controller.Error}", controller.Failure);
        }
        Console.WriteLine($"Owned dialogue presentation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, retiredRequest={request}, story={session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, status={policy.State.Status}.");
    }
}
