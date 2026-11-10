using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeOptionalInnTourTests
{
    public static void Run()
    {
        Run("mktinn", "MEMORIA_FF7_INN_TOUR_SAVE");
    }

    public static void RunItemShop()
    {
        Run("mkt_s3", "MEMORIA_FF7_ITEM_SHOP_TOUR_SAVE");
    }

    private static void Run(string destination, string variable)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP optional inn tour: configure original resources and a completed market-local fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(original.Field.Name == "mrkt2" && saved.EventCoverage?.Current is { CatalogComplete: true }, "The tour fixture must follow native main-field local completion.");
        string progress = Economy(original.Progress);
        AutoPlayEventCoverageScope mainScope = (saved.EventCoverage!.History ?? []).Append(saved.EventCoverage.Current!).Last(scope => scope.Id.StartsWith("mrkt2:", StringComparison.Ordinal) && scope.Id.EndsWith(":188", StringComparison.Ordinal));
        AutoPlayEventCoverageEntry[] mainReceipts = mainScope.Events.Where(entry => entry.Status == AutoPlayEventCoverageStatus.Completed).ToArray();
        int pending = saved.EventCoverage.Tour!.Frontiers.Count(edge => edge.Kind == AutoPlayTourKind.OptionalGameplay && edge.Status == AutoPlayTourStatus.Pending);
        Check(saved.EventCoverage.Tour.Frontiers.Single(edge => edge.Destination == destination).Status == AutoPlayTourStatus.Pending, "The selected optional room must be genuinely unvisited in this source.");
        Ff7OpeningAutoPlayPolicy policy = new(original, Ff7AutoPlayGoal.MidgarWorldMap);
        Ff7OpeningAutoPlayPolicy coldPolicy = new(cold, Ff7AutoPlayGoal.MidgarWorldMap);
        Ff7AutoplayDriver driver = new(original);
        Ff7AutoplayDriver coldDriver = new(cold);
        bool Done(Ff7Session session) => session.Field.Name == "mrkt2" && session.EventCoverage?.Tour?.Frontiers.Single(edge => edge.Destination == destination).Status == AutoPlayTourStatus.Completed;
        int callbacks = 0;
        while (!Done(original) && callbacks++ < 1000)
        {
            Tick(original, policy, driver);
            Tick(cold, coldPolicy, coldDriver);
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Production optional-room navigation, events or tour receipts diverged after cold restore.");
        }
        Check(Done(original) && callbacks < 1000, "The approved native inn visit did not complete its local interactions and return within the bounded tour.");
        AutoPlayTourFrontier inn = original.EventCoverage!.Tour!.Frontiers.Single(edge => edge.Destination == destination);
        Check(inn.BeforeWitness is not null && inn.EntryWitness is not null && inn.ReturnWitness?.Contains(destination + ":", StringComparison.Ordinal) == true, "A completed regional visit requires original entry and covered native return witnesses.");
        Check(Economy(original.Progress) == progress, "Declining the native inn question changed Gil, items, HP/MP, party or battle rewards.");
        Tick(original, policy, driver);
        Tick(cold, coldPolicy, coldDriver);
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native return initializer changed its cold continuation.");
        try
        {
            policy.ChooseInput(driver);
        }
        catch (NotSupportedException)
        {
        }
        AutoPlayEventCoverageScope returned = original.EventCoverage!.Current!;
        Check(returned.Id == mainScope.Id && mainReceipts.All(receipt => returned.Events.Single(entry => entry.Id == receipt.Id) == receipt), "Returning to the same native availability epoch discarded prior local completion or manufactured new receipts.");
        Check(original.EventCoverage.Tour!.Frontiers.Count(edge => edge.Kind == AutoPlayTourKind.OptionalGameplay && edge.Status == AutoPlayTourStatus.Pending) == pending - 1, "Completing one room must not falsely complete another optional room.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The tour fixture was modified.");
        Console.WriteLine($"PASS production optional {destination} entry, local events, native return and regional receipts ({callbacks} callbacks)");
    }

    private static void Tick(Ff7Session session, Ff7OpeningAutoPlayPolicy policy, Ff7AutoplayDriver driver)
    {
        if (session.Shop is { Phase: not Ff7ShopPhase.Completed } shop)
        {
            session.AdvanceShop(shop.Phase == Ff7ShopPhase.Interactive);
            session.AdvanceClock(TimeSpan.FromSeconds(1d / 60));
            return;
        }
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            Ff7Dialogue selected = policy.ChooseDialogueSelection(dialogue) is int selection ? Ff7DialogueCursor.Select(dialogue, selection) : dialogue;
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(selected, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        driver.Apply(policy.ChooseInput(driver));
        session.AdvanceTick();
    }

    private static string Economy(Ff7Progress progress) => JsonSerializer.Serialize(new { progress.Gil, progress.Inventory, progress.Materia, progress.Characters, progress.Party, progress.AvailableCharacters, progress.LockedCharacters, progress.CompletedBattles });

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
