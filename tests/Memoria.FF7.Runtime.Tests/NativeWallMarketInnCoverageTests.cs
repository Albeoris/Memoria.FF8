using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketInnCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_INN_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native inn catalog: select original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "Select the concrete completed outer-market continuation.");
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int callbacks = 0;
        for (; callbacks < 1500 && session.Field.Name != "mktinn"; callbacks++)
        {
            StepGateway(session, driver, 199);
            StepGateway(cold, coldDriver, 199);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mktinn" && session.Host.Process.Tick == 0, "The physical inn gateway was not entered within the bound.");
        int entryCallbacks = callbacks;
        bool checkedQuestion = false;
        bool checkedLeave = false;
        bool inspected = false;
        for (; callbacks < 3000; callbacks++)
        {
            string? resumedNext = null;
            if (!checkedLeave && coverage.Capture().Current?.Events.Any(entry => entry.Id == "line:10:script:5" && entry.Status == AutoPlayEventCoverageStatus.Completed) == true)
            {
                string native = JsonSerializer.Serialize(session.Capture());
                driver.LeaveLine(10);
                driver.LeaveLine(10);
                Check(native == JsonSerializer.Serialize(session.Capture()), "Planning a reachable inn-door leave changed native state.");
                checkedLeave = true;
            }
            if (!checkedQuestion && session.Host.Process.Dialogues.Any(dialogue => dialogue is { Completed: false, AwaitingInput: true, FirstChoice: 2, LastChoice: 3 }))
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                StepInn(resumed, resumedCoverage, new(resumed));
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedQuestion = true;
            }
            StepInn(session, coverage, driver);
            StepInn(cold, coldCoverage, coldDriver);
            Equal(session, cold);
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "The live inn question diverged after JSON cold restore.");
            if (!inspected)
            {
                Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
                Check(plan.Complete && plan.Events.Count == 48, "All native inn interfaces and alternatives must be classified after initialization.");
                Check(plan.Events.Count(target => target.Id.StartsWith("line:", StringComparison.Ordinal)) == 24, "Every callback slot of the four inn contacts must be classified.");
                Check(plan.Events.Single(target => target.Id == "line:7:script:4").Kind == Ff7FieldEventCoverageKind.Automatic, "An empty counter-touch return must not complete the inn conversation.");
                inspected = true;
            }
            AutoPlayEventCoverageScope? scope = coverage.Capture().Current;
            if (scope?.Id.StartsWith("mktinn:", StringComparison.Ordinal) == true && scope.Events.All(entry => entry.Status is not (AutoPlayEventCoverageStatus.Available or AutoPlayEventCoverageStatus.Active)))
            {
                callbacks++;
                break;
            }
        }
        string pending = string.Join(",", coverage.Capture().Current?.Events.Where(entry => entry.Status is AutoPlayEventCoverageStatus.Available or AutoPlayEventCoverageStatus.Active).Select(entry => $"{entry.Id}/{entry.Status}") ?? []);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(checkedQuestion && checkedLeave && callbacks < 3000, $"The native question and every current inn event must finish within the bound; question={checkedQuestion}, field={session.Field.Name}, tick={session.Host.Process.Tick}, pending={pending}, player={player.X4096}/{player.Y4096}/{player.Triangle}.");
        AutoPlayEventCoverageEntry[] completed = coverage.Capture().Current!.Events.Where(entry => entry.Status == AutoPlayEventCoverageStatus.Completed).ToArray();
        Check(completed.Length == 7, "The inn requires its actual decline conversation and six guest-door callbacks.");
        foreach ((string id, string returned) in new[] { ("conditional:11:script:3", "06EF"), ("line:8:script:5", "060E"), ("line:8:script:6", "0617"), ("line:9:script:5", "0633"), ("line:9:script:6", "063C"), ("line:10:script:5", "0658"), ("line:10:script:6", "0661") })
        {
            Check(completed.Single(entry => entry.Id == id).Outcome!.Contains(returned, StringComparison.Ordinal), "A native inn interaction lacks its exact owned return.");
        }
        Check(session.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory), "Declining the inn must not charge Gil or change inventory.");
        Check(session.Progress.Characters.Select(character => (character.Id, character.HitPoints, character.MagicPoints)).SequenceEqual(saved.Progress.Characters.Select(character => (character.Id, character.HitPoints, character.MagicPoints))), "Declining the inn must not claim a rest or healing.");
        int roomCallbacks = callbacks - entryCallbacks;
        for (; callbacks < 4500 && session.Field.Name == "mktinn"; callbacks++)
        {
            StepGateway(session, driver, 195);
            StepGateway(cold, coldDriver, 195);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mrkt2" && session.MapId == 195 && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The native inn return must reach its original market gateway.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The inn proof changed its standalone.");
        Console.WriteLine($"PASS native inn physical entry/7 owned events/decline/no charge or healing/return and question cold restore ({entryCallbacks} entry, {roomCallbacks} room, {callbacks} total callbacks); source unchanged.");
    }

    private static void StepInn(Ff7Session session, Ff7AutoPlayEventCoverage coverage, Ff7AutoplayDriver driver)
    {
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            int? reply = new Ff7MidgarAutoPlayPolicy(session).ChooseDialogueSelection(dialogue);
            Ff7Dialogue selected = dialogue.FirstChoice is null ? dialogue : Ff7DialogueCursor.Select(dialogue, reply ?? throw new InvalidOperationException("The inn question requires its reviewed production reply policy."));
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(selected, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        coverage.TryChooseInput(driver, out Ff7AutoplayInput action);
        driver.Apply(action);
        session.AdvanceTick();
    }

    private static void StepGateway(Ff7Session session, Ff7AutoplayDriver driver, int destination)
    {
        driver.Apply(driver.Gateway(destination));
        session.AdvanceTick();
    }

    private static void Equal(Ff7Session session, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The physical inn continuation diverged after JSON cold restore.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
