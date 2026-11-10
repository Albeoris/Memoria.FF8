using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketMainStreetCoverageTests
{
    public static void Run() => Run(nested: false);

    public static void RunNested() => Run(nested: true);

    private static void Run(bool nested)
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
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
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
        bool parentBlocked = false;
        while (callbacks < 2400)
        {
            Ff7AutoplayInput command;
            if (nested)
            {
                coverage.RefreshTourCatalog();
                coldCoverage.RefreshTourCatalog();
                if (session.Field.Name == "mktpb")
                {
                    break;
                }
                bool handling = coverage.TryChooseInput(driver, out command);
                Check(coldCoverage.TryChooseInput(coldDriver, out Ff7AutoplayInput coldInput) == handling && command == coldInput, "Cold local coverage chose different input.");
                if (!handling)
                {
                    if (session.Field.Name == "mrkt2")
                    {
                        command = driver.Gateway(205);
                    }
                    else
                    {
                        Check(session.Field.Name == "mrkt1" && coverage.CanLeaveField, "The nested source must have completed local native coverage.");
                        try
                        {
                            coverage.TryChooseTourInput(driver, out _);
                            throw new InvalidOperationException("Production navigation admitted an unreviewed nested directory.");
                        }
                        catch (NotSupportedException exception) when (exception.Message.Contains("child catalogs", StringComparison.Ordinal))
                        {
                            parentBlocked = true;
                        }
                        command = driver.Gateway(204);
                    }
                }
            }
            else if (session.Field.Name == "mrkt2" && entry == 0)
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
        Check(new[] { 1, 2, 3, 4 }.All(texts.Contains), "The native subset messages are incomplete.");
        if (nested)
        {
            Check(parentBlocked && session.Field.Name == "mktpb", "The nested probe must reach the genuine pub doorway after production declines the incomplete directory.");
            AutoPlayTourState state = session.EventCoverage!.Tour!;
            AutoPlayTourFrontier parent = state.Frontiers.Single(edge => edge.Id == "gateway:0:205");
            AutoPlayTourFrontier child = new AutoPlayTourLedger(state).ActiveLeaf!;
            Check(parent.Status == AutoPlayTourStatus.Active && parent.EntryWitness is not null && parent.ReturnWitness is null, "The main-street parent must remain active with a native entry receipt.");
            Check(child.Id == "main-street:204" && child.ParentId == parent.Id && child.EntryWitness is not null && child.ReturnWitness is null, "The native pub entrance must produce its own active child receipt.");
            Check(state.Directories is [{ Source: "mrkt1", Complete: false, ParentId: "gateway:0:205" }], "The incomplete source directory must remain bound to the parent.");
            using Ff7Session nestedCold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
            using Ff7AutoPlayEventCoverage nestedColdCoverage = new(nestedCold);
            session.AdvanceTick();
            nestedCold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(nestedCold.Capture()), "Restoring with an active parent and child changed the next native callback.");
            Ff7AutoplayDriver nestedColdDriver = new(nestedCold);
            for (int callback = 0; session.Field.Name == "mktpb" && callback < 120; callback++)
            {
                Ff7AutoplayInput back = driver.Gateway(205);
                Check(back == nestedColdDriver.Gateway(205), "Restoring a nested entry changed native return navigation.");
                driver.Apply(back);
                nestedColdDriver.Apply(back);
                session.AdvanceTick();
                nestedCold.AdvanceTick();
                Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(nestedCold.Capture()), "An uncovered native return diverged after nested cold restore.");
            }
            Check(session.Field.Name == "mrkt1", "The original pub gateway must return to the active parent's field.");
            AutoPlayTourState returned = session.EventCoverage!.Tour!;
            Check(new AutoPlayTourLedger(returned).ActiveLeaf == child, "An uncovered native return must leave the child active without a completion receipt.");
        }
        else
        {
            Check(session.Field.Name == "mrkt2" && previewPure, "The native navigation preview or original market return is incomplete.");
        }
        Check(session.Progress.Gil == saved.Progress.Gil && session.Progress.Inventory.SequenceEqual(saved.Progress.Inventory), "The native local main-street visit changed Gil or inventory.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The main-street subset wrote its source.");
        Console.WriteLine(nested
            ? $"PASS native nested entry and incomplete-directory gate: {callbacks} cold-equal callbacks plus one nested restore callback."
            : $"PASS native main-street local subset and return: {entry} entry, {callbacks} total cold-equal callbacks.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
