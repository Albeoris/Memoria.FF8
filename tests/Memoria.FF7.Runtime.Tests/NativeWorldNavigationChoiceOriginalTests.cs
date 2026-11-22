using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWorldNavigationChoiceOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_NAVIGATION_SAVE") ?? throw new InvalidOperationException("Select an external owned world navigation continuation.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Check(saved.World is not null && saved.AutoPlay?.Choice is { NativeNavigationRequested: true } && saved.AutoPlay.Active is { Accepted: true, TransitionOwned: true }, "The selected checkpoint does not contain an actual requested native world handoff.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), saved);
        string native = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null, EventCoverage = null });
        int writes = 0;
        using Ff7AutoPlayPolicy policy = new(session, (_, actual) =>
        {
            Check(actual.World is { } world && world.EntrySelector == saved.World!.EntrySelector && actual.FieldGeneration == saved.FieldGeneration, "Choice settlement published a historical field instead of the actual native world arrival.");
            writes++;
            return "diagnostic-live-world-continuation-" + writes;
        });
        Check(policy.State.Choice is { NativeExecutionCommitted: true }, "The restored authenticated world handoff remained replayable.");
        Ff7AutoplayDriver driver = new(session);
        policy.ChooseInput(driver);
        Check(policy.State.PendingRestore is null && policy.State.Choice is null && policy.State.Active?.Kind is not (Ff7AutoPlayTargetKind.ActorTalk or Ff7AutoPlayTargetKind.LinePush), "The actual world handoff did not retire its field choice without rollback.");
        Check(writes > 0 && native == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null, EventCoverage = null }), "Navigation bookkeeping changed native world, field, wallet or execution state.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The selected standalone checkpoint was changed.");
        Console.WriteLine($"PASS original world navigation: world={session.World!.Process.WorldId}, entry={session.World.Process.EntrySelector}, tick={session.World.Process.Tick}, generation={session.FieldGeneration}, liveWrites={writes}, no native callbacks/restore, SHA256={Convert.ToHexString(SHA256.HashData(source))}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
