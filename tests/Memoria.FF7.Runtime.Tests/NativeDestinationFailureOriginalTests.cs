using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDestinationFailureOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DESTINATION_FAILURE_SAVE") ?? throw new InvalidOperationException("Select an external unavailable destination fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Route failure inspection must not explore a native choice."));
        ushort destination = policy.State.DestinationField ?? throw new InvalidOperationException("The fixture has no explicit destination.");
        string expected = $"No currently admitted native field connectivity route to field {destination} is established from this navigation component.";
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        int polls = 0;
        while (polls++ < 8 && controller.Error is null && policy.State.Active is null)
        {
            controller.Advance(1);
        }
        string? reason = controller.Failure?.Message;
        Console.WriteLine($"Destination decision: polls={polls}, callbacks={controller.Steps}, tick={session.Machine.Tick}, active={policy.State.Active?.EventId}, status={policy.State.Status}, pendingPlanning={controller.PlanningProgress.Pending}, reason={reason}.");
        string? expectedEvent = Environment.GetEnvironmentVariable("MEMORIA_FF7_DESTINATION_EXPECT_EVENT");
        bool correctDecision = expectedEvent is null ? reason == expected && policy.State.Active is null : controller.Error is null && policy.State.Active?.EventId == expectedEvent;
        if (!correctDecision || policy.State.PendingRestore is not null || !source.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("An unrelated ambient motion masked destination failure or inspection changed native state.");
        }
        Console.WriteLine($"PASS original destination decision: field={session.MapId}, tick={session.Machine.Tick}, destination={destination}, no restore, reason={reason}, SHA256={Convert.ToHexString(SHA256.HashData(source))}.");
    }
}
