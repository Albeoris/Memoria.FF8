using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketAbandonedItemStoreCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_ITEM_STORE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP abandoned item-store coverage: configure original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The item-store source has no native continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2", "The item-store proof requires its concrete market continuation.");
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int travel = 0;
        for (; travel < 800 && session.Field.Name != "mkt_ia"; travel++)
        {
            Gateway(session, driver, 198);
            Gateway(cold, coldDriver, 198);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mkt_ia", "The original abandoned item-store entrance was not reached.");
        Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(session);
        Check(plan.Complete && plan.Events.Count == 26 && plan.Events.Count(entry => entry.Kind == Ff7FieldEventCoverageKind.Conditional) == 1 && plan.Events.Count(entry => entry.Kind == Ff7FieldEventCoverageKind.Exit) == 1, "Every terminal contact, empty conversation, later alternative and sole return gateway must be classified.");
        bool childReturned = false;
        bool parentReturned = false;
        session.NativeTickCompleted += observation =>
        {
            childReturned |= observation.ScriptReturns.Any(receipt => receipt.Entity == 4 && receipt.Priority == 5 && receipt.ProgramCounter == 0x0609);
            if (observation.ScriptReturns.Any(receipt => receipt.Entity == 5 && receipt.Priority == 1 && receipt.ProgramCounter == 0x0772) && childReturned)
            {
                parentReturned = true;
            }
        };
        bool Complete(Ff7AutoPlayEventCoverage owner) => owner.CanLeaveField && owner.Capture().Current?.Id.StartsWith("mkt_ia:", StringComparison.Ordinal) == true;
        Ff7AutoPlayController controller = new(session, next => coverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, hasReachedGoal: () => Complete(coverage));
        Ff7AutoPlayController coldController = new(cold, next => coldCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, hasReachedGoal: () => Complete(coldCoverage));
        controller.Start();
        coldController.Start();
        bool checkedSceneCold = false;
        for (int callback = 0; callback < 1200 && !controller.Completed; callback++)
        {
            string? resumedNext = null;
            if (!checkedSceneCold && session.Host.Process.PlayerLocked && session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.Text == 2))
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Ff7AutoPlayController resumedController = new(resumed, next => resumedCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, hasReachedGoal: () => Complete(resumedCoverage));
                resumedController.Start();
                resumedController.Advance(1);
                Check(resumedController.Error is null, "The saved native broken-terminal scene could not resume.");
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedSceneCold = true;
            }
            controller.Advance(1);
            coldController.Advance(1);
            Check(controller.Error is null && coldController.Error is null, controller.Error ?? coldController.Error ?? "The native terminal controller failed.");
            Equal(session, cold);
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "Cold terminal continuation replayed its Confirm or changed its scene timing.");
        }
        Check(controller.Completed && coldController.Completed && childReturned && parentReturned && checkedSceneCold, $"The native terminal did not complete: tick{session.Machine.Tick}, locked={session.Host.Process.PlayerLocked}, active={string.Join(',', coverage.Capture().Current?.Events.Where(entry => entry.Status == Memoria.AutoPlay.AutoPlayEventCoverageStatus.Active).Select(entry => entry.Id) ?? [])}.");
        for (int callback = 0; callback < 800 && session.Field.Name != "mrkt2"; callback++)
        {
            Gateway(session, driver, 195);
            Gateway(cold, coldDriver, 195);
            Equal(session, cold);
            travel++;
        }
        Check(session.Field.Name == "mrkt2", "The terminal visit did not use its sole original market return.");
        Check(session.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory) && JsonSerializer.Serialize(session.Progress.Materia) == JsonSerializer.Serialize(saved.Progress.Materia), "The current broken-terminal scene must not award its unavailable later unique weapon or mutate unrelated progress.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The original item-store source was modified.");
        Console.WriteLine($"PASS native abandoned item-store26-target catalog, Confirm-gated scene, cold child/parent returns and market return ({controller.Steps} scene callbacks, {travel} travel callbacks)");
    }

    private static void Gateway(Ff7Session session, Ff7AutoplayDriver driver, int destination)
    {
        driver.Apply(session.Host.Process.PlayerLocked ? default : driver.Gateway(destination));
        session.AdvanceTick();
    }

    private static void Equal(Ff7Session original, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native item-store continuation diverged after JSON cold restoration.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
