using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLinePushSemanticTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle triangle = new(0, new(-100, -100, 0), new(100, -100, 0), new(0, 100, 0), -1, -1, -1);
        short[] vectors = new short[512];
        byte[] angles = new byte[129];
        for (int heading = 0; heading < 256; heading++)
        {
            vectors[heading * 2] = (short)Math.Round(Math.Sin(heading * Math.PI / 128) * 4096);
            vectors[heading * 2 + 1] = (short)Math.Round(Math.Cos(heading * Math.PI / 128) * 4096);
        }
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = (byte)Math.Round(Math.Asin(index / 128.0) * 128 / Math.PI);
        }
        Ff7NativeMovement movement = new([triangle], vectors, angles);
        Ff7Line line = new(0, 5, 0, 0, 0, 50, 0, 0);
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Triangle = 0, X4096 = 20 << 12, Y4096 = 10 << 12, CollisionRadius = 20 };
        byte toward = movement.Direction(0, -10);
        byte away = unchecked((byte)(toward + 128));
        string before = JsonSerializer.Serialize(new { player, line });
        Ff7AutoplayInput? Choose(Ff7Actor actual, Ff7Line boundary, int heading, bool allowed = true, bool probe = false)
        {
            Ff7NativeMoveResult Preview(Ff7AutoplayInput input)
            {
                return probe ? new(actual with { Direction = away }, false, false, [], ProbeActor: actual with { Direction = heading }) : new(actual with { Direction = heading }, false, false, []);
            }
            return Ff7AutoplayDriver.ChooseNativeLinePushInput(actual, boundary, movement, Preview, (_, _) => allowed);
        }
        Check(Choose(player, line, away) is null, "Merely touching a LINE while facing away was treated as native Push.");
        Check(Choose(player, line, toward) is not null, "A native heading-only Push was discarded because XYZ did not move.");
        Check(Choose(player, line, toward, probe: true) is not null, "The native ProbeActor direction was replaced by the retained actor heading.");
        Check(Choose(player, line, toward, allowed: false) is null, "A native Push bypassed protected input admission.");
        Check(Choose(player, line with { Enabled = 0 }, toward) is null, "A disabled LINE admitted Push.");
        Check(Choose(player with { Y4096 = 20 << 12 }, line, toward) is null, "The strict native radius boundary admitted Push.");
        Check(Choose(player, line with { Z1 = 300, Z2 = 300 }, toward) is null, "An invalid native LINE height admitted Push.");
        Check(Choose(player with { X4096 = 0, Y4096 = 0 }, line, away) is not null, "Exact native endpoint contact lost its direction-independent Push contract.");
        Check(before == JsonSerializer.Serialize(new { player, line }), "Pure LINE input selection changed native source facts.");

        Ff7FieldResources field = new("line-push-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        foreach (byte heading in new[] { toward, away })
        {
            Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [player], Lines = [line] }) { Movement = movement };
            host.UpdateLineContacts(player, player with { Direction = heading }, moving: true);
            Check(host.Process.Lines.Single().Push == (Choose(player, line, heading) is not null), "Planner Push admission disagreed with the live host's shared contact predicate.");
        }
        Console.WriteLine("Native LINE Push facing, exact contact and host/planner agreement passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
