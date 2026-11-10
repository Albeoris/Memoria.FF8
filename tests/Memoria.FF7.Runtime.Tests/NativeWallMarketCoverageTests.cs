using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class NativeWallMarketCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native market catalog: select original resources and a standalone market arrival.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2", "Select the concrete native market arrival.");
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        Check(plan.Complete && plan.Events.Count == 58, "The native arrival interface must retain its complete structural inventory and required scene owner.");
        Check(plan.Events.Select(target => target.Id).Distinct().Count() == 58, "Native market event identities must be unique.");
        Check(plan.Events.Count(target => target.Id.StartsWith("line:", StringComparison.Ordinal)) == 18, "All six contact slots of the three native LINE owners must be classified.");
        Check(plan.Events.Single(target => target.Id == "actor:17:script:2").Kind == Ff7FieldEventCoverageKind.Inactive, "The touch conversation must remain separate from turning-only talk and pending physical proof.");
        Check(plan.Events.Single(target => target.Id == "conditional:2:script:1").Kind == Ff7FieldEventCoverageKind.Conditional, "The arrival scene must be required before current NPC interaction or departure.");
        Check(plan.Events.Single(target => target.Id == "party:13:script:3").Reason.Contains("07B6", StringComparison.Ordinal), "The early story write must not replace the native scene completion owner.");
        Ff7WallMarketTourPlan tour = Ff7WallMarketTourCatalog.Inspect(session);
        Check(!tour.Complete && tour.RootField == "mrkt2" && tour.Edges.Count == 9, "All native exits must retain the explicit side-room frontier.");
        Check(tour.Edges.Where(edge => edge.Kind == AutoPlayTourKind.OptionalGameplay).Count() == 6, "All six native side-room doors must remain represented.");
        Check(tour.Edges.Where(edge => edge.CatalogReady).Select(edge => edge.DestinationField).SequenceEqual([194]), "Unreviewed room catalogs must never be advertised as ready.");
        Check(tour.Edges.All(edge => edge.ReturnField == 195), "The original immediate room directory must retain its market return.");
        Check(JsonSerializer.Serialize(plan) == JsonSerializer.Serialize(Ff7FieldEventCoverageCatalog.Inspect(cold)) && JsonSerializer.Serialize(tour) == JsonSerializer.Serialize(Ff7WallMarketTourCatalog.Inspect(cold)), "Market classification changed after a JSON cold restore.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Catalog inspection changed native state.");
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        Ff7MidgarAutoPlayPolicy policy = new(session);
        Ff7MidgarAutoPlayPolicy coldPolicy = new(cold);
        bool checkedResume = false;
        bool checkedContact = false;
        int callbacks = 0;
        for (; callbacks < 3000; callbacks++)
        {
            string? resumedNext = null;
            if (!checkedResume && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188 && session.Host.Process.PlayerLocked)
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Advance(resumed, resumedCoverage, new(resumed), new(resumed));
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedResume = true;
            }
            if (!checkedContact && coverage.Capture().Current?.Events.Any(entry => entry.Id == "actor:17:script:1" && entry.Status == AutoPlayEventCoverageStatus.Completed) == true)
            {
                string native = JsonSerializer.Serialize(session.Capture());
                driver.Contact(17);
                driver.Contact(17);
                Check(native == JsonSerializer.Serialize(session.Capture()), "Contact planning advanced native input, VM, motion or random state.");
                checkedContact = true;
            }
            Advance(session, coverage, driver, policy);
            Advance(cold, coldCoverage, coldDriver, coldPolicy);
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native market continuation diverged after JSON cold restore.");
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "The active arrival scene replayed or diverged after its early story write.");
            AutoPlayEventCoverageScope? scope = coverage.Capture().Current;
            if (scope?.Id.EndsWith(":188", StringComparison.Ordinal) == true && scope.Events.All(entry => entry.Status is not (AutoPlayEventCoverageStatus.Available or AutoPlayEventCoverageStatus.Active)))
            {
                callbacks++;
                break;
            }
        }
        Check(checkedResume && checkedContact && callbacks < 3000, "Native arrival and all local events did not finish within the bounded continuation.");
        AutoPlayEventCoverageState completed = coverage.Capture();
        Check((completed.History ?? []).Any(scope => scope.Events.Any(entry => entry.Id == "conditional:2:script:1" && entry.Status == AutoPlayEventCoverageStatus.Completed && entry.Outcome!.Contains("07B6", StringComparison.Ordinal))), "The native arrival owner was not completed before leaving its early-story epoch.");
        AutoPlayEventCoverageEntry[] finished = completed.Current!.Events.Where(entry => entry.Status == AutoPlayEventCoverageStatus.Completed).ToArray();
        Check(finished.Length == 12, "Every finite current conversation and contact must complete once.");
        foreach ((string id, string returned) in new[] { ("actor:16:script:1", "0B90"), ("actor:17:script:1", "0BBE"), ("actor:17:script:2", "0C5E"), ("actor:18:script:1", "0CBB"), ("actor:19:script:1", "0D42"), ("actor:20:script:1", "0E3D"), ("actor:21:script:1", "0F1A"), ("actor:22:script:1", "0FA0"), ("line:11:script:5", "0941"), ("line:11:script:6", "094A"), ("line:24:script:5", "0FF1"), ("line:24:script:6", "0FFB") })
        {
            Check(finished.Single(entry => entry.Id == id).Outcome!.Contains(returned, StringComparison.Ordinal), "A native local interaction lacks its exact owned return.");
        }
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188 && !session.Host.Process.PlayerLocked, "Local coverage left the native field or changed its story epoch.");
        string beforeFrontier = JsonSerializer.Serialize(session.Capture() with { EventCoverage = null });
        bool blocked = false;
        try
        {
            coverage.TryChooseTourInput(driver, out _);
        }
        catch (NotSupportedException)
        {
            blocked = true;
        }
        Check(blocked && coverage.HasUnfinishedTour, "Unreviewed side-room content must block touring after local completion.");
        Check(beforeFrontier == JsonSerializer.Serialize(session.Capture() with { EventCoverage = null }), "A pending room frontier advanced native state.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Catalog inspection changed its standalone.");
        Console.WriteLine($"PASS native market catalog58/frontier9, arrival plus12 local native returns, contact-preview purity and active-scene cold continuation ({callbacks} callbacks); source unchanged, room frontiers remain pending.");
    }

    private static void Advance(Ff7Session session, Ff7AutoPlayEventCoverage coverage, Ff7AutoplayDriver driver, Ff7MidgarAutoPlayPolicy policy)
    {
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        if (!coverage.TryChooseInput(driver, out Ff7AutoplayInput action))
        {
            policy.TryChooseInput(driver, out action);
        }
        driver.Apply(action);
        session.AdvanceTick();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
