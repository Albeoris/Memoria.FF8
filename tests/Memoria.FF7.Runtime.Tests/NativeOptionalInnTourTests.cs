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

    public static void RunMateria()
    {
        Run("mkt_m", "MEMORIA_FF7_MATERIA_TOUR_SAVE");
    }

    public static void RunRestaurant()
    {
        Run("mkt_s2", "MEMORIA_FF7_RESTAURANT_TOUR_SAVE", meal: true);
    }

    public static void RunCoupon()
    {
        Run("mkt_s3", "MEMORIA_FF7_COUPON_TOUR_SAVE", coupon: true);
    }

    public static void RunAbandonedItemStore()
    {
        Run("mkt_ia", "MEMORIA_FF7_ABANDONED_TOUR_SAVE");
    }

    private static void Run(string destination, string variable, bool meal = false, bool coupon = false)
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
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7SessionSave saved = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        if (coupon)
        {
            using Ff7AutoPlayEventCoverage originalCatalog = new(original);
            using Ff7AutoPlayEventCoverage coldCatalog = new(cold);
            originalCatalog.RefreshTourCatalog();
            coldCatalog.RefreshTourCatalog();
        }
        Check(original.Field.Name == "mrkt2" && saved.EventCoverage?.Current is { CatalogComplete: true }, "The tour fixture must follow native main-field local completion.");
        string progress = Economy(original.Progress);
        uint gil = original.Progress.Gil;
        string inventory = JsonSerializer.Serialize(new { original.Progress.Inventory, original.Progress.Materia, original.Progress.Party, original.Progress.CompletedBattles });
        AutoPlayEventCoverageScope mainScope = (saved.EventCoverage!.History ?? []).Append(saved.EventCoverage.Current!).Last(scope => scope.Id.StartsWith("mrkt2:", StringComparison.Ordinal) && scope.Id.EndsWith(":188", StringComparison.Ordinal));
        AutoPlayEventCoverageEntry[] mainReceipts = mainScope.Events.Where(entry => entry.Status == AutoPlayEventCoverageStatus.Completed).ToArray();
        int pending = original.EventCoverage!.Tour!.Frontiers.Count(edge => edge.Kind == AutoPlayTourKind.OptionalGameplay && edge.Status == AutoPlayTourStatus.Pending);
        Check(original.EventCoverage.Tour.Frontiers.Single(edge => edge.Destination == destination).Status == AutoPlayTourStatus.Pending, "The selected optional room predicate must be genuinely unvisited in this source.");
        Ff7OpeningAutoPlayPolicy policy = new(original, Ff7AutoPlayGoal.MidgarWorldMap);
        Ff7OpeningAutoPlayPolicy coldPolicy = new(cold, Ff7AutoPlayGoal.MidgarWorldMap);
        Ff7AutoplayDriver driver = new(original);
        Ff7AutoplayDriver coldDriver = new(cold);
        bool Done(Ff7Session session) => session.Field.Name == "mrkt2" && session.EventCoverage?.Tour?.Frontiers.Single(edge => edge.Destination == destination).Status == AutoPlayTourStatus.Completed;
        int callbacks = 0;
        int limit = meal ? 2000 : 1000;
        while (!Done(original) && callbacks++ < limit)
        {
            Tick(original, policy, driver);
            Tick(cold, coldPolicy, coldDriver);
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Production optional-room navigation, events or tour receipts diverged after cold restore.");
        }
        Check(Done(original) && callbacks < limit, "The approved native optional visit did not complete its local interactions and return within the bounded tour.");
        AutoPlayTourFrontier inn = original.EventCoverage!.Tour!.Frontiers.Single(edge => edge.Destination == destination);
        Check(inn.BeforeWitness is not null && inn.EntryWitness is not null && inn.ReturnWitness?.Contains(destination + ":", StringComparison.Ordinal) == true, "A completed regional visit requires original entry and covered native return witnesses.");
        if (meal)
        {
            Check(gil >= 70 && original.Progress.Gil == gil - 70 && JsonSerializer.Serialize(new { original.Progress.Inventory, original.Progress.Materia, original.Progress.Party, original.Progress.CompletedBattles }) == inventory, "The native meal did not charge exactly its authored price or changed unrelated inventory/party rewards.");
            Check((original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0xA0) & 0x40) != 0 && (original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0x42) & 1) != 0, "The native paid/rated meal did not grant its original coupon flags.");
        }
        else
        {
            Check(Economy(original.Progress) == progress, "The optional visit changed Gil, items, HP/MP, party or battle rewards.");
        }
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
        Check(returned.Id == mainScope.Id && mainReceipts.Where(receipt => !coupon || receipt.Id != "actor:16:script:1").All(receipt => returned.Events.Single(entry => entry.Id == receipt.Id) == receipt), "Returning to the same native availability epoch discarded unchanged local completion or manufactured new receipts.");
        if (coupon)
        {
            AutoPlayEventCoverageEntry information = returned.Events.Single(entry => entry.Id == "actor:16:script:1");
            Check(information.Status == AutoPlayEventCoverageStatus.Completed && information.Availability.EndsWith(":quest-info", StringComparison.Ordinal), "The changed native main-field quest information must be physically retalked, not reused from its previous message.");
            Check((original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0xA0) & 0x40) == 0 && (original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0x42) & 1) == 0 && (original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0xA2) & 4) != 0, "The native coupon exchange must consume the coupon and award Digestive.");
            Check(original.EventCoverage.Tour!.CompletedPredicates!.Any(edge => edge.Destination == "mkt_s3" && edge.Availability.EndsWith(":coupon40", StringComparison.Ordinal)) && original.EventCoverage.Tour.Frontiers.Single(edge => edge.Destination == "mkt_s3").Availability.EndsWith(':'), "Coupon completion and the previously observed ordinary-shop baseline must both retain their own receipts without replay.");
        }
        Check(original.EventCoverage.Tour!.Frontiers.Count(edge => edge.Kind == AutoPlayTourKind.OptionalGameplay && edge.Status == AutoPlayTourStatus.Pending) == pending - (meal ? 0 : 1), "Completing one room must retain other unvisited rooms and reopen only a newly available native predicate.");
        if (meal)
        {
            Check(original.EventCoverage.Tour.Frontiers.Single(edge => edge.Destination == "mkt_s3").Status == AutoPlayTourStatus.Pending && original.EventCoverage.Tour.CompletedPredicates!.Any(edge => edge.Destination == "mkt_s3" && edge.Status == AutoPlayTourStatus.Completed), "An earned coupon must reopen the pharmacy branch while retaining the original ordinary-shop receipt.");
        }
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The tour fixture was modified.");
        if (meal && Environment.GetEnvironmentVariable("MEMORIA_FF7_RESTAURANT_PROOF_OUTPUT") is string output)
        {
            if (Path.GetFullPath(output) == Path.GetFullPath(path))
            {
                throw new InvalidOperationException("The diagnostic meal result must not replace its source checkpoint.");
            }
            CombinedSessionCodec.Save(output, envelope with { Ff7 = original.Capture() });
        }
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
