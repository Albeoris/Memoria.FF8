using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketMateriaCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_MATERIA_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native materia-room catalog: select original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "Select the concrete current outer-market continuation.");
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int callbacks = 0;
        for (; callbacks < 1500 && session.Field.Name != "mkt_m"; callbacks++)
        {
            Gateway(session, driver, 200);
            Gateway(cold, coldDriver, 200);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mkt_m", "The native gateway did not physically enter the materia room.");
        int entryCallbacks = callbacks;
        bool inspected = false;
        bool resumedMessage = false;
        bool checkedPreview = false;
        bool sawShop = false;
        HashSet<int> messages = [];
        for (; callbacks < 3000; callbacks++)
        {
            string? resumedNext = null;
            if (!checkedPreview && inspected)
            {
                string native = JsonSerializer.Serialize(session.Capture());
                driver.TalkTo(5);
                driver.TalkTo(5);
                Check(native == JsonSerializer.Serialize(session.Capture()), "Reachable keeper approach planning changed native state.");
                checkedPreview = true;
            }
            if (!resumedMessage && session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.AwaitingInput))
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Advance(resumed, resumedCoverage, new(resumed));
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                resumedMessage = true;
            }
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
            {
                Check(dialogue.FirstChoice is null, "A later quest question must not be forced at this native epoch.");
                messages.Add(dialogue.Text);
            }
            Advance(session, coverage, driver);
            Advance(cold, coldCoverage, coldDriver);
            Equal(session, cold);
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "The native keeper conversation diverged after cold restore.");
            sawShop |= session.Host.Process.Menu is { Mode: 8 };
            if (!inspected)
            {
                Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
                Check(plan.Complete && plan.Events.Count == 16 && session.Host.Process.Lines.Length == 0, "Every native materia-room interface and inactive branch must be classified.");
                Check(plan.Events.Single(target => target.Id == "alternative:5:shop:7").Kind == Ff7FieldEventCoverageKind.Inactive, "The later Shop7 branch must remain unavailable.");
                inspected = true;
            }
            AutoPlayEventCoverageScope? scope = coverage.Capture().Current;
            if (scope?.Id.StartsWith("mkt_m:", StringComparison.Ordinal) == true && scope.Events.All(entry => entry.Status is not (AutoPlayEventCoverageStatus.Available or AutoPlayEventCoverageStatus.Active)))
            {
                callbacks++;
                break;
            }
        }
        Check(callbacks < 3000 && resumedMessage && checkedPreview && !sawShop, "The current native conversation did not complete without forcing later shop/quest content.");
        AutoPlayEventCoverageEntry[] completed = coverage.Capture().Current!.Events.Where(entry => entry.Status == AutoPlayEventCoverageStatus.Completed).ToArray();
        Check(completed is [{ Id: "actor:5:script:1" }] && completed[0].Outcome!.Contains("0682", StringComparison.Ordinal), "The actual keeper interaction must retain its exact native return.");
        int expectedText = (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x40) & 0xC0) != 0 || (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x41) & 1) != 0 ? 5 : 1;
        Check(messages.SetEquals([expectedText]), "The informational message must follow the original current-epoch native predicate.");
        Check(session.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory) && JsonSerializer.Serialize(session.Progress.Materia) == JsonSerializer.Serialize(saved.Progress.Materia), "The current informational visit must not invent a purchase or reward.");
        int roomCallbacks = callbacks - entryCallbacks;
        for (; callbacks < 4500 && session.Field.Name == "mkt_m"; callbacks++)
        {
            Gateway(session, driver, 195);
            Gateway(cold, coldDriver, 195);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mrkt2" && session.MapId == 195 && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The room must physically return through its original market gateway.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The read-only room proof changed its standalone.");
        Console.WriteLine($"PASS native materia-room16target catalog/physical keeper RET0682/current text/no forced shop or reward/cold message/return ({entryCallbacks} entry, {roomCallbacks} room, {callbacks} total callbacks); source unchanged.");
    }

    private static void Advance(Ff7Session session, Ff7AutoPlayEventCoverage coverage, Ff7AutoplayDriver driver)
    {
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        coverage.TryChooseInput(driver, out Ff7AutoplayInput action);
        driver.Apply(action);
        session.AdvanceTick();
    }

    private static void Gateway(Ff7Session session, Ff7AutoplayDriver driver, int destination)
    {
        driver.Apply(driver.Gateway(destination));
        session.AdvanceTick();
    }

    private static void Equal(Ff7Session session, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The physical materia-room continuation diverged after JSON cold restore.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
