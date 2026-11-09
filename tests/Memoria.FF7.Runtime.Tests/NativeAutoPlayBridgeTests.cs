using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayBridgeTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_BRIDGE_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed bridge navigation: original resources or external standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        Ff7AutoplayDriver driver = new(session);
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == 6);
        Vector3 target = new((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, line.Z1);
        string before = JsonSerializer.Serialize(session.Capture());
        for (int query = 0; query < 25; query++)
        {
            _ = driver.Approach(target);
        }
        Equal(before, JsonSerializer.Serialize(session.Capture()));
        for (int tick = 0; tick < 100 && !session.Host.Process.PlayerLocked; tick++)
        {
            driver.Apply(driver.Approach(target));
            session.AdvanceTick();
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Equal(true, player.X4096 > -800 * 4096);
        Equal(true, player.Triangle != 65);
        using Ff7Session cold = Ff7Session.Restore(installation, session.Capture());
        Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Equal(true, File.ReadAllBytes(path).SequenceEqual(original));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
