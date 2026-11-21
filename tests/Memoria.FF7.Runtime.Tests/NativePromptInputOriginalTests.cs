using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePromptInputOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_PROMPT_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native prompt input fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The prompt boundary test does not store choices."));
        FieldScriptInputDemand[] demands = session.Machine.ReadCurrentInputDemands().ToArray();
        FieldHostRequest prompt = session.Machine.Capture().PendingRequests!.Single(request => request.Command is FieldDialogueCommand { FirstChoice: null });
        Console.WriteLine(string.Join(";", demands.Select(demand => $"{demand.Identity}:awaiting={demand.AwaitingInput}:mask={demand.Mask}:proof={session.Machine.ProveCurrentPromptChoiceInput(demand, prompt)}")));
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 10000 && controller.Steps < 64 && timer.Elapsed.TotalSeconds < 15 && controller.Error is null && !session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null); polls++)
        {
            controller.Advance(1);
        }
        Ff7Dialogue? choice = session.Host.Process.Dialogues.SingleOrDefault(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null);
        Console.WriteLine($"Original prompt input: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Host.Process.Tick}, choiceText={choice?.Text}, choiceOwner={session.Machine.Capture().PendingRequests?.SingleOrDefault(request => request.Id == choice?.RequestId)?.Entity}, activeAccepted={policy.State.Active?.Accepted}, status={policy.State.Status}, error={controller.Error}");
        if (choice is null || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The current native prompt did not admit its real choice workflow.");
        }
    }
}
