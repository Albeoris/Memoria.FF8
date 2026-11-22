using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNavigationChoiceOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_CHOICE_SAVE") ?? throw new InvalidOperationException("Select an external owned navigation choice continuation.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        ushort nextDestination = ushort.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_NEXT_DESTINATION")!);
        int nextEntity = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_NEXT_ENTITY")!);
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), saved);
        int writes = 0;
        using Ff7AutoPlayPolicy policy = new(session, (_, actual) =>
        {
            Check(actual.FieldName == saved.FieldName && actual.Progress.Gil == saved.Progress.Gil, "Navigation settlement published a pre-transfer field or refunded the actual paid fare.");
            return "live-navigation-" + ++writes;
        });
        Check(policy.State.Choice is { NativeExecutionCommitted: true, NativeNavigationRequested: null }, "The actual legacy paid transfer was not recovered from its retained native proof.");
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        int polls = 0;
        while (controller.Error is null && (policy.State.Choice is not null || policy.State.Active is not null) && polls++ < 200)
        {
            controller.Advance(1);
            Check(policy.State.PendingRestore is null && session.Field.Name == saved.FieldName, "The paid native arrival queued or applied a historical choice restore.");
        }
        Console.WriteLine($"Paid arrival settlement: polls={polls}, steps={controller.Steps}, tick={session.Machine.Tick}, choice={policy.State.Choice?.NativeExecutionCommitted}, active={policy.State.Active?.EventId}/{policy.State.Active?.CompletionRecorded}, writes={writes}, playerLocked={session.Host.Process.PlayerLocked}, menuDisabled={session.Host.Process.MenuDisabled}, status={policy.State.Status}, error={controller.Error}.");
        Check(controller.Error is null && policy.State.Choice is null && policy.State.Active is null && writes == 1 && session.Progress.Gil == saved.Progress.Gil, "The paid transfer did not settle its actual continuation and source owner without rollback.");
        bool admitted = false;
        session.NativeInteractionScriptRequested += request => admitted |= request is { Accepted: true, Started: true, Priority: 1, Script: 4 } && request.Entity == nextEntity;
        policy.SetDestinationField(nextDestination);
        int afterReturn = 0;
        while (controller.Error is null && !admitted && session.MapId != nextDestination && afterReturn++ < 250)
        {
            controller.Advance(1);
            Check(policy.State.PendingRestore is null, "Forward native navigation restored an old optional branch.");
        }
        Check(controller.Error is null && (session.MapId == nextDestination || admitted && policy.State.Active is { Accepted: true } active && active.Entity == nextEntity && session.Host.Process.Lines.Any(line => line.Entity == nextEntity && line.Enabled == 0)), "The retained paid arrival did not reach the next actual owned navigation branch.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The authoritative paid-arrival standalone was changed.");
        Console.WriteLine($"PASS original committed navigation: returnPolls={polls}, nextPolls={afterReturn}, field={session.MapId}, nativeCallbacks={controller.Steps}, writes={writes}, admitted={admitted}, gil={session.Progress.Gil}, no restore/error, source SHA256={Convert.ToHexString(SHA256.HashData(source))}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
