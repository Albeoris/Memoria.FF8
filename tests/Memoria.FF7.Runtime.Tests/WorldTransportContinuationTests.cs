using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldTransportContinuationTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TRANSPORT_SAVE");
        if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original world transport continuation: configure MEMORIA_FF7_GAME_DIR and MEMORIA_FF7_WORLD_TRANSPORT_SAVE.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The transport fixture has no FFVII continuation.");
        Ff7WorldProcess world = saved.World ?? throw new InvalidDataException("The transport fixture must retain an active world continuation.");
        Check(!world.MovementEnabled, "The selected transport fixture must retain its script-owned control lock.");
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Ff7Session first = Ff7Session.Restore(installation, saved);
        using Ff7Session second = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(Equal(first.Capture(), second.Capture()), "Independent transport restores must preserve the same complete continuation.");
        foreach (Ff7Session session in new[] { first, second })
        {
            session.SetInput(0, 0);
            session.MovePlayer(0, 0, true);
            session.AdvanceTick(advanceClock: false);
        }
        Ff7SessionSave next = first.Capture();
        Check(Equal(next, second.Capture()), "A neutral native transport callback must retain full cold snapshot equality.");
        Check(Equal(saved.Progress, next.Progress), "The bounded neutral transport callback must preserve recorded progress, inventory and rewards.");
        Check(next.World is { } continued && continued.ControlledModel == world.ControlledModel && continued.MovementEnabled == world.MovementEnabled && continued.ControlLock == world.ControlLock, "The transport callback must preserve the recorded controlled owner and native script control lock.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The transport standalone changed during read-only continuation.");
        Console.WriteLine("PASS actual world transport standalone, complete neutral cold continuation, script control owner and unchanged source");
    }

    private static bool Equal<T>(T left, T right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
