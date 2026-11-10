using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketItemShopCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_ITEM_SHOP_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native item-shop coverage: configure original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The item-shop fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The selected item-shop fixture must retain the reviewed market visit.");
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int fieldCallbacks = 0;
        for (; fieldCallbacks < 800 && session.Field.Name != "mkt_s3"; fieldCallbacks++)
        {
            Gateway(session, driver, 203);
            Gateway(cold, coldDriver, 203);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mkt_s3", "The original item-shop gateway was not entered within its native bound.");
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        Check(plan.Complete && plan.Events.Count == 10 && plan.Events.Count(entry => entry.Kind == Ff7FieldEventCoverageKind.Interaction) == 1, "Every original counter contact, automatic owner and exit must be classified.");
        bool counterReturned = false;
        session.NativeTickCompleted += observation => counterReturned |= observation.ScriptReturns.Any(receipt => receipt.Entity == 7 && receipt.Priority == 6 && receipt.ProgramCounter == 0x04F5);
        bool Complete(Ff7AutoPlayEventCoverage owner) => owner.CanLeaveField && owner.Capture().Current?.Id.StartsWith("mkt_s3:", StringComparison.Ordinal) == true;
        Ff7AutoPlayController controller = new(session, next => coverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, hasReachedGoal: () => Complete(coverage));
        Ff7AutoPlayController coldController = new(cold, next => coldCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, hasReachedGoal: () => Complete(coldCoverage));
        controller.Start();
        coldController.Start();
        bool checkedMenuCold = false;
        int menuCallbacks = 0;
        string? frozen = null;
        for (int callback = 0; callback < 1200 && !controller.Completed; callback++)
        {
            string? resumedNext = null;
            if (!checkedMenuCold && session.Shop is { Phase: Ff7ShopPhase.Opening, Tick: 17 })
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Ff7AutoPlayController resumedController = new(resumed, next => resumedCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, hasReachedGoal: () => Complete(resumedCoverage));
                resumedController.Start();
                resumedController.Advance(1);
                Check(resumedController.Error is null, "A saved native shop callback could not resume through the production controller.");
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedMenuCold = true;
            }
            bool inMenu = session.Shop is { Phase: not Ff7ShopPhase.Completed };
            if (inMenu)
            {
                frozen ??= JsonSerializer.Serialize(session.Machine.Capture());
                menuCallbacks++;
            }
            controller.Advance(1);
            coldController.Advance(1);
            Check(controller.Error is null && coldController.Error is null, controller.Error ?? coldController.Error ?? "Native controller failed.");
            Equal(session, cold);
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "A native shop cold resume replayed a prefix or changed controller cadence.");
            Check(!inMenu || frozen == JsonSerializer.Serialize(session.Machine.Capture()), "A native menu callback advanced the frozen field VM.");
            Check(!Complete(coverage) || counterReturned, "Coverage completed before the original child counter return.");
        }
        Check(controller.Completed && coldController.Completed && checkedMenuCold && menuCallbacks == 55 && counterReturned, "The original counter and native shop lifecycle did not complete within their bounds.");
        AutoPlayEventCoverageEntry completed = coverage.Capture().Current!.Events.Single(entry => entry.Id == "line:2:script:1");
        Check(completed is { Status: AutoPlayEventCoverageStatus.Completed, Outcome: not null } && completed.Outcome.Contains("0306", StringComparison.Ordinal), "The parent counter contact requires its exact native return after waiting for the shopkeeper.");
        for (int callback = 0; callback < 600 && session.Field.Name != "mrkt2"; callback++)
        {
            Gateway(session, driver, 195);
            Gateway(cold, coldDriver, 195);
            Equal(session, cold);
            fieldCallbacks++;
        }
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The original item-shop return gateway did not return to the market.");
        Check(session.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory) && JsonSerializer.Serialize(session.Progress.Materia) == JsonSerializer.Serialize(saved.Progress.Materia), "The observational counter visit purchased or sold an item.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The original item-shop source was modified.");
        Console.WriteLine($"PASS native item-shop complete catalog, counter ownership, production55-callback module and cold return ({controller.Steps} controller callbacks, {fieldCallbacks} travel callbacks)");
    }

    private static void Gateway(Ff7Session session, Ff7AutoplayDriver driver, int destination)
    {
        driver.Apply(session.Host.Process.PlayerLocked ? default : driver.Gateway(destination));
        session.AdvanceTick();
    }

    private static void Equal(Ff7Session session, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original item-shop continuation diverged after JSON cold restoration.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
