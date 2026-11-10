using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineTalkInputTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_TALK_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native line talk input: configure original resources and a native line-facing fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Line line = session.Host.Process.Lines.First(line => line.Enabled == 1 && line.Touch && line.OnLine && ((line.ProximityAngle - player.Direction + 32) & 255) >= 64);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayInput planned = driver.TalkToLine(line.Entity, run: false);
        Check(planned == driver.TalkToLine(line.Entity, run: false) && planned == new Ff7AutoplayDriver(cold).TalkToLine(line.Entity, run: false), "Line-talk preview changed repeated or cold input selection.");
        Check(!planned.Talk && planned.Held == 0 && !planned.Run, "Line-talk planning confirmed before native facing admission or lost the requested walking choice.");
        (int X, int Y, int Direction)[] directions = [(-1, -1, 32), (0, -1, 0), (1, -1, 224), (-1, 0, 64), (1, 0, 192), (-1, 1, 96), (0, 1, 128), (1, 1, 160)];
        int actual = (directions.Single(direction => direction.X == planned.Horizontal && direction.Y == planned.Vertical).Direction + session.Field.Triggers.ControlDirection) & 255;
        Check(((line.ProximityAngle - actual + 32) & 255) < 64, "Selected physical controls cannot face inside the native line-talk window.");
        Check(JsonSerializer.Serialize(session.Capture()) == before && JsonSerializer.Serialize(cold.Capture()) == before, "Input planning changed native state without an input callback.");
        Check(SHA256.HashData(source).SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Read-only line input verification changed its fixture.");
        Console.WriteLine("PASS native line-facing input, repeated preview purity and cold equality without VM callbacks");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
