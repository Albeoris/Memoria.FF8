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
        RunVisit(coupon: false);
    }

    public static void RunCoupon()
    {
        RunVisit(coupon: true);
    }

    private static void RunVisit(bool coupon)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable(coupon ? "MEMORIA_FF7_WALL_MARKET_COUPON_SAVE" : "MEMORIA_FF7_WALL_MARKET_ITEM_SHOP_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native item-shop coverage: configure original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The item-shop fixture has no FFVII continuation.");
        if (!coupon)
        {
            AutoPlayEventCoverageScope[] scopes = (saved.EventCoverage?.History ?? []).Concat(saved.EventCoverage?.Current is { } current ? new[] { current } : []).ToArray();
            Check(!scopes.Any(scope => scope.Id.StartsWith("mkt_s3:", StringComparison.Ordinal) && scope.Events.Any(entry => entry.Id == "line:2:script:1" && entry.Status == AutoPlayEventCoverageStatus.Completed && entry.Availability.EndsWith("coupon0", StringComparison.Ordinal))), "The fresh item-shop lifecycle fixture must precede its completed native counter receipt; completed gameplay must not be replayed or its coverage cleared for this check.");
        }
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
        Check(plan.Complete && plan.Events.Count == (coupon ? 12 : 10) && plan.Events.Count(entry => entry.Kind == Ff7FieldEventCoverageKind.Interaction) == 1, "Every original counter contact, automatic owner, exclusive reward alternative and exit must be classified.");
        bool counterReturned = false;
        bool parentReturned = false;
        session.NativeTickCompleted += observation =>
        {
            counterReturned |= observation.ScriptReturns.Any(receipt => receipt.Entity == 7 && receipt.Priority == 6 && receipt.ProgramCounter == 0x04F5);
            if (observation.ScriptReturns.Any(receipt => receipt.Entity == 2 && receipt.Priority == 1 && receipt.ProgramCounter == 0x0306))
            {
                Check(counterReturned, "The parent counter returned before its native child completed.");
                parentReturned = true;
            }
        };
        bool Complete(Ff7AutoPlayEventCoverage owner) => owner.CanLeaveField && owner.Capture().Current?.Id.StartsWith("mkt_s3:", StringComparison.Ordinal) == true;
        Ff7MidgarAutoPlayPolicy policy = new(session);
        Ff7MidgarAutoPlayPolicy coldPolicy = new(cold);
        Ff7AutoPlayController controller = new(session, next => coverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => Complete(coverage));
        Ff7AutoPlayController coldController = new(cold, next => coldCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, chooseDialogueSelection: coldPolicy.ChooseDialogueSelection, hasReachedGoal: () => Complete(coldCoverage));
        controller.Start();
        coldController.Start();
        bool checkedMenuCold = false;
        int menuCallbacks = 0;
        string? frozen = null;
        for (int callback = 0; callback < 1200 && !controller.Completed; callback++)
        {
            string? resumedNext = null;
            bool coldPoint = coupon
                ? session.Host.Process.Dialogues.Any(dialogue => dialogue is { Completed: false, AwaitingInput: true, Text: 3, FirstChoice: 0, LastChoice: 2 })
                : session.Shop is { Phase: Ff7ShopPhase.Opening, Tick: 17 };
            if (!checkedMenuCold && coldPoint)
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Ff7MidgarAutoPlayPolicy resumedPolicy = new(resumed);
                Ff7AutoPlayController resumedController = new(resumed, next => resumedCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, chooseDialogueSelection: resumedPolicy.ChooseDialogueSelection, hasReachedGoal: () => Complete(resumedCoverage));
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
        Check(controller.Completed && coldController.Completed && checkedMenuCold && menuCallbacks == (coupon ? 0 : 55) && counterReturned && parentReturned, "The original counter and its native module/question lifecycle did not complete within their bounds.");
        if (coupon)
        {
            Check((saved.Vm.PersistentVariables[0xA0] & 64) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 64) == 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 1) == 0, "The original pharmacy must consume the actual coupon predicate and key-item bit.");
            Check((session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 7) == 4 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 14) == 8, "Choice2 must award only the native Digestive flags, without claiming its exclusive alternatives.");
            coverage.TryChooseInput(driver, out Ff7AutoplayInput afterReward);
            Check(Complete(coverage) && afterReward == default, "Consuming the coupon must not reopen its completed counter interaction.");
            using Ff7Session rewardedCold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
            using Ff7AutoPlayEventCoverage rewardedCoverage = new(rewardedCold);
            rewardedCoverage.TryChooseInput(new(rewardedCold), out Ff7AutoplayInput rewardedInput);
            Check(Complete(rewardedCoverage) && rewardedInput == default, "Cold reward coverage must not repeat the native coupon question or reward.");
        }
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
        Console.WriteLine($"PASS native {(coupon ? "coupon Digestive" : "item-shop")} complete catalog, counter ownership, cold question/module and return ({controller.Steps} controller callbacks, {fieldCallbacks} travel callbacks)");
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
