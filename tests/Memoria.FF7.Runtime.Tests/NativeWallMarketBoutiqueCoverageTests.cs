using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketBoutiqueCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_BOUTIQUE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native boutique subset: select original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "Select the concrete current outer-market continuation.");
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        bool counter = false;
        bool customer = false;
        bool directTailor = false;
        bool resumedMessage = false;
        HashSet<int> texts = [];
        session.NativeTickCompleted += observation =>
        {
            if (observation.Field != "mkt_s1")
            {
                return;
            }
            counter |= observation.ScriptReturns.Any(value => value.Entity == 9 && value.Priority == 6 && value.ProgramCounter == 0x0DC0);
            customer |= observation.ScriptReturns.Any(value => value.Entity == 11 && value.Priority == 1 && value.ProgramCounter == 0x0F1C);
            directTailor |= observation.ScriptReturns.Any(value => value.Entity == 9 && value.Priority == 1);
        };
        int callbacks = 0;
        for (; callbacks < 1500 && session.Field.Name != "mkt_s1"; callbacks++)
        {
            Advance(session, driver, driver.Gateway(201));
            Advance(cold, coldDriver, coldDriver.Gateway(201));
            Equal(session, cold);
        }
        Check(session.Field.Name == "mkt_s1", "The native boutique entry did not complete within the bound.");
        int entryCallbacks = callbacks;
        for (; callbacks < 2500 && !(counter && customer); callbacks++)
        {
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
            {
                Check(dialogue.FirstChoice is null, "Later wardrobe questions must not be forced at this epoch.");
                texts.Add(dialogue.Text);
            }
            string? resumedNext = null;
            if (!resumedMessage && session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.AwaitingInput))
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                Ff7AutoplayDriver resumedDriver = new(resumed);
                Advance(resumed, resumedDriver, Choose(resumed, resumedDriver, counter));
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                resumedMessage = true;
            }
            bool counterBefore = counter;
            Advance(session, driver, Choose(session, driver, counterBefore));
            Advance(cold, coldDriver, Choose(cold, coldDriver, counterBefore));
            Equal(session, cold);
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "The boutique subset message diverged after cold restore.");
        }
        Check(counter && customer && resumedMessage && !directTailor && texts.SetEquals([35, 36]), "Both researched current conversations must retain their actual owned returns without claiming direct tailor talk.");
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        Check(!plan.Complete && plan.Events.Count == 38 && plan.Reason.Contains("unproved", StringComparison.Ordinal), "The unresolved direct-talk frontier must keep the boutique catalog incomplete.");
        Check(session.Machine.Variables.ReadByte(FieldBank.MapByte, 4) == 0 && session.Host.Process.Lines.Single(line => line.Entity == 3).Enabled == 0, "The current native layout and disabled fitting room must not be changed by this subset.");
        Check(session.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory), "The informational subset must not invent a wardrobe purchase or reward.");
        string native = JsonSerializer.Serialize(session.Capture() with { EventCoverage = null });
        using (Ff7AutoPlayEventCoverage coverage = new(session))
        {
            bool rejected = false;
            try
            {
                coverage.TryChooseInput(driver, out _);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected && native == JsonSerializer.Serialize(session.Capture() with { EventCoverage = null }), "An incomplete boutique catalog must reject autoplay without advancing native state.");
        }
        int roomCallbacks = callbacks - entryCallbacks;
        for (; callbacks < 3500 && session.Field.Name == "mkt_s1"; callbacks++)
        {
            Advance(session, driver, driver.Gateway(195));
            Advance(cold, coldDriver, coldDriver.Gateway(195));
            Check(JsonSerializer.Serialize(session.Capture() with { EventCoverage = null }) == JsonSerializer.Serialize(cold.Capture() with { EventCoverage = null }), "The original boutique return diverged after cold restore.");
        }
        Check(session.Field.Name == "mrkt2" && session.MapId == 195 && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The researched subset must use the original market return.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The read-only boutique subset changed its standalone.");
        Console.WriteLine($"PASS boutique researched subset: counter9/p6 RET0DC0/customer11/p1 RET0F1C/cold message/return ({entryCallbacks} entry, {roomCallbacks} room, {callbacks} total callbacks). Catalog remains INCOMPLETE; direct talk was not executed or aliased.");
    }

    private static Ff7AutoplayInput Choose(Ff7Session session, Ff7AutoplayDriver driver, bool counterDone)
    {
        if (session.Host.Process.Tick == 0 || session.Host.Process.PlayerLocked || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return default;
        }
        if (counterDone)
        {
            return driver.TalkTo(11);
        }
        Ff7Line line = session.Host.Process.Lines.Single(value => value.Entity == 2);
        return line.Touch ? new(Held: (ushort)((session.Machine.Input.Held & 0x20) == 0 ? 0x20 : 0)) : driver.Approach(new((line.X1 + line.X2) / 2f, (line.Y1 + line.Y2) / 2f, (line.Z1 + line.Z2) / 2f));
    }

    private static void Advance(Ff7Session session, Ff7AutoplayDriver driver, Ff7AutoplayInput action)
    {
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        driver.Apply(action);
        session.AdvanceTick();
    }

    private static void Equal(Ff7Session session, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The physical boutique subset diverged after JSON cold restore.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
