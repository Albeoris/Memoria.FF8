using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayCrowdTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CROWD_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed crowd navigation: original resources or external standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7AutoplayDriver driver = new(session);
        string beforePreview = JsonSerializer.Serialize(session.Capture());
        for (int query = 0; query < 25; query++)
        {
            _ = driver.Gateway(156);
        }
        Check(JsonSerializer.Serialize(session.Capture()) == beforePreview, "Obstacle previews mutated the native world or VM.");
        Vector3 portal = (session.Field.Triggers.Gateways.Single(gateway => gateway.DestinationField == 156).A + session.Field.Triggers.Gateways.Single(gateway => gateway.DestinationField == 156).B) / 2;
        float Distance()
        {
            Ff7Actor actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            return Vector2.Distance(new(actor.X4096 / 4096f, actor.Y4096 / 4096f), new(portal.X, portal.Y));
        }
        float initial = Distance();
        float closest = initial;
        long generation = session.FieldGeneration;
        for (int tick = 0; tick < 600 && session.FieldGeneration == generation; tick++)
        {
            driver.Apply(driver.Gateway(156));
            session.AdvanceTick();
            if (session.FieldGeneration == generation)
            {
                closest = Math.Min(closest, Distance());
            }
            if (tick == 127)
            {
                using Ff7Session cold = Ff7Session.Restore(installation, session.Capture());
                Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(session.Capture()), "Crowd navigation changed native cold-restoration facts.");
            }
        }
        Check(session.FieldGeneration != generation || closest < initial - 96, $"Native crowd navigation did not escape its local collision trap: initial={initial}, closest={closest}.");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "The read-only crowd probe changed its standalone source.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
