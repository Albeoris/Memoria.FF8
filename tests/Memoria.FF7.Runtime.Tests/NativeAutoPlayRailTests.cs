using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayRailTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_RAIL_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed rail navigation: original resources or external standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7AutoplayDriver driver = new(session);
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == 2);
        Vector3 target = new((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, line.Z1);
        string before = JsonSerializer.Serialize(session.Capture());
        for (int query = 0; query < 25; query++)
        {
            _ = driver.Approach(target);
        }
        Check(JsonSerializer.Serialize(session.Capture()) == before, "Rail navigation previews changed native world facts.");
        long generation = session.FieldGeneration;
        for (int tick = 0; tick < 100 && session.FieldGeneration == generation; tick++)
        {
            driver.Apply(driver.Approach(target));
            session.AdvanceTick();
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Check(session.FieldGeneration != generation || player is not null && player.Y4096 > 1500 * 4096 && player.X4096 > 400 * 4096, "Rail navigation remained trapped at the shared triangle edge.");
        using Ff7Session cold = Ff7Session.Restore(installation, session.Capture());
        Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(session.Capture()), "Rail navigation changed native cold-restoration facts.");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "The rail navigation probe changed its standalone source.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
