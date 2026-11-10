using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketMainStreetCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_MAIN_STREET_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native main-street subset: select original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        HashSet<(int Entity, int Pc)> returns = [];
        HashSet<int> texts = [];
        session.NativeTickCompleted += observation =>
        {
            if (observation.Field == "mrkt1")
            {
                foreach (var returned in observation.ScriptReturns.Where(value => value.Priority == 1))
                {
                    returns.Add((returned.Entity, returned.ProgramCounter));
                }
            }
        };
        int callbacks = 0;
        int entry = 0;
        bool previewPure = false;
        while (callbacks < 2400)
        {
            Ff7AutoplayInput command;
            if (session.Field.Name == "mrkt2" && entry == 0)
            {
                command = driver.Gateway(205);
            }
            else if (session.Field.Name != "mrkt1")
            {
                break;
            }
            else
            {
                if (entry == 0)
                {
                    entry = callbacks;
                }
                if (!previewPure && session.Host.Process.Actors.Any(actor => actor.Entity == 15 && actor.Visible))
                {
                    string before = JsonSerializer.Serialize(session.Capture());
                    driver.TalkTo(15);
                    driver.TalkTo(16);
                    Check(before == JsonSerializer.Serialize(session.Capture()), "Native main-street navigation preview changed the session.");
                    previewPure = true;
                    var names = (IReadOnlyList<string>)typeof(Ff7Session).GetField("mapList", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
                    Console.WriteLine("Native main-street destinations: " + string.Join(", ", session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => $"{gateway.DestinationField}:{names[gateway.DestinationField]}")));
                }
                if (!returns.Contains((15, 0x0868)))
                {
                    command = driver.TalkTo(15);
                }
                else if (!returns.Contains((16, 0x0893)))
                {
                    command = driver.TalkTo(16);
                }
                else if (!returns.Contains((8, 0x07C1)))
                {
                    Ff7Line line = session.Host.Process.Lines.Single(value => value.Entity == 8);
                    command = driver.Approach(new Vector3((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, 0));
                }
                else if (!returns.Contains((8, 0x07CA)))
                {
                    command = driver.LeaveLine(8);
                }
                else
                {
                    command = driver.Gateway(195);
                }
            }
            foreach (var current in new[] { session, cold })
            {
                foreach (var dialogue in current.Host.Process.Dialogues.Where(value => !value.Completed))
                {
                    Check(dialogue.FirstChoice is null, "An unreviewed main-street question was reached.");
                    texts.Add(dialogue.Text);
                    if (dialogue.AwaitingInput && current.Host.CanConfirmDialogue(dialogue.RequestId))
                    {
                        current.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(current.Field.Scripts.Texts[dialogue.Text].Span)));
                    }
                }
            }
            driver.Apply(command);
            coldDriver.Apply(command);
            session.AdvanceTick();
            cold.AdvanceTick();
            callbacks++;
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Native main-street subset diverged after cold restore.");
        }
        Check(returns.Contains((15, 0x0868)) && returns.Contains((16, 0x0893)) && returns.Contains((8, 0x07C1)) && returns.Contains((8, 0x07CA)), "The main-street subset did not complete all four named native owners within the bound.");
        Check(session.Field.Name == "mrkt2" && previewPure && new[] { 1, 2, 3, 4 }.All(texts.Contains), "The native subset messages, previews or original market return are incomplete.");
        Check(session.Progress.Gil == saved.Progress.Gil && session.Progress.Inventory.SequenceEqual(saved.Progress.Inventory), "The native local main-street visit changed Gil or inventory.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The main-street subset wrote its source.");
        Console.WriteLine($"PASS native main-street local subset and return: {entry} entry, {callbacks} total cold-equal callbacks. Nested destinations remain unreviewed; no full-area completion is claimed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
