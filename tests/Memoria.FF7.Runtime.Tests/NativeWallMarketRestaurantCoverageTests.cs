using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWallMarketRestaurantCoverageTests
{
    public static void RunReturn()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_RESTAURANT_SAVE") ?? throw new InvalidOperationException("Select the native restaurant return continuation.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The return fixture has no native continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mkt_s2", "The return check requires the actual restaurant boundary.");
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int callbacks = 0;
        for (; callbacks < 16 && session.Field.Name == "mkt_s2"; callbacks++)
        {
            Gateway(session, driver, 195);
            Gateway(cold, coldDriver, 195);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mrkt2", "The native restaurant return requires a longer physical continuation.");
        Check(session.Progress.Gil == saved.Progress.Gil && JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory), "The native return changed meal rewards or inventory.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The actual return source was modified.");
        Console.WriteLine($"PASS native restaurant remaining return ({callbacks} callbacks, {session.Field.Name}/{session.FieldGeneration}/{session.Machine.Tick}, story{session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)})");
    }

    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WALL_MARKET_RESTAURANT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native restaurant coverage: configure original resources and a standalone market continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The restaurant fixture has no native continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        GilReceiptContract(installation, saved);
        MenuStatisticsOwnership(installation, saved);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The restaurant proof requires its reviewed market epoch.");
        using Ff7AutoPlayEventCoverage coverage = new(session);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayDriver coldDriver = new(cold);
        int travel = 0;
        for (; travel < 800 && session.Field.Name != "mkt_s2"; travel++)
        {
            Gateway(session, driver, 202);
            Gateway(cold, coldDriver, 202);
            Equal(session, cold);
        }
        Check(session.Field.Name == "mkt_s2", "The restaurant gateway did not reach its original field.");
        bool mealReturned = false;
        session.NativeTickCompleted += observation => mealReturned |= observation.ScriptReturns.Any(receipt => receipt.Entity == 5 && receipt.Priority == 1 && receipt.ProgramCounter == 0x08AD);
        Ff7MidgarAutoPlayPolicy policy = new(session);
        Ff7MidgarAutoPlayPolicy coldPolicy = new(cold);
        bool Complete(Ff7AutoPlayEventCoverage owner) => owner.CanLeaveField && owner.Capture().Current?.Id.StartsWith("mkt_s2:", StringComparison.Ordinal) == true;
        Ff7AutoPlayController controller = new(session, next => coverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => Complete(coverage));
        Ff7AutoPlayController coldController = new(cold, next => coldCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, chooseDialogueSelection: coldPolicy.ChooseDialogueSelection, hasReachedGoal: () => Complete(coldCoverage));
        controller.Start();
        coldController.Start();
        bool checkedPaidCold = false;
        for (int callback = 0; callback < 5000 && !controller.Completed; callback++)
        {
            string? resumedNext = null;
            if (!checkedPaidCold && session.Progress.Gil == saved.Progress.Gil - 70 && session.Host.Process.PlayerLocked)
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Ff7MidgarAutoPlayPolicy resumedPolicy = new(resumed);
                Ff7AutoPlayController resumedController = new(resumed, next => resumedCoverage.TryChooseInput(next, out Ff7AutoplayInput action) ? action : default, chooseDialogueSelection: resumedPolicy.ChooseDialogueSelection, hasReachedGoal: () => Complete(resumedCoverage));
                resumedController.Start();
                resumedController.Advance(1);
                Check(resumedController.Error is null, "A paid native meal could not resume its next callback.");
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedPaidCold = true;
            }
            controller.Advance(1);
            coldController.Advance(1);
            Check(controller.Error is null && coldController.Error is null, controller.Error ?? coldController.Error ?? "Native restaurant controller failed.");
            Equal(session, cold);
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(session.Capture()), "A paid native meal cold resume repeated the charge or lost its pending script phase.");
        }
        string summary = string.Join(",", coverage.Capture().Current?.Events.Select(entry => $"{entry.Id}:{entry.Status}") ?? []);
        Check(controller.Completed && coldController.Completed && mealReturned && checkedPaidCold, $"The restaurant events did not complete: {session.Field.Name}/{session.Machine.Tick} locked={session.Host.Process.PlayerLocked}; {summary}");
        Check(session.Progress.Gil == saved.Progress.Gil - 70 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 64) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 1) != 0, $"The original meal must charge exactly70Gil and award its one-time coupon through native script writes: Gil{saved.Progress.Gil}->{session.Progress.Gil}, A0={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0):X2}, A2={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2):X2},42={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42):X2},Map6={session.Machine.Variables.ReadByte(FieldBank.MapByte, 6)}.");
        Check(JsonSerializer.Serialize(session.Progress.Inventory) == JsonSerializer.Serialize(saved.Progress.Inventory) && JsonSerializer.Serialize(session.Progress.Materia) == JsonSerializer.Serialize(saved.Progress.Materia), "The native restaurant scene changed unrelated inventory or materia.");
        for (int callback = 0; callback < 800 && session.Field.Name != "mrkt2"; callback++)
        {
            Gateway(session, driver, 195);
            Gateway(cold, coldDriver, 195);
            Equal(session, cold);
            travel++;
        }
        Check(session.Field.Name == "mrkt2" && session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == 188, "The restaurant did not return through its original market gateway.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The restaurant source changed during its read-only proof.");
        Console.WriteLine($"PASS native restaurant patron/warning/meal/coupon/return coverage ({controller.Steps} event callbacks, {travel} travel callbacks)");
    }

    private static void Gateway(Ff7Session session, Ff7AutoplayDriver driver, int destination)
    {
        driver.Apply(session.Host.Process.PlayerLocked ? default : driver.Gateway(destination));
        session.AdvanceTick();
    }

    private static void GilReceiptContract(Ff7Installation installation, Ff7SessionSave saved)
    {
        using Ff7Session unit = Ff7Session.Restore(installation, saved);
        FieldHostRequest query = new(long.MaxValue - 402, 0, 1, 0, new FieldGilQuery());
        Check(unchecked((uint)unit.Host.Execute(query).Value) == saved.Progress.Gil, "Native CHGLD lost the full unsigned Gil value.");
        FieldHostRequest subtract = new(long.MaxValue - 401, 0, 1, 0, new FieldGilCommand(uint.MaxValue, Subtract: true));
        unit.Host.Execute(subtract);
        Check(unit.Progress.Gil == 0, "Native GOLD- must saturate at zero when its unsigned amount exceeds available funds.");
        using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(unit.Capture()))!);
        resumed.Host.Execute(subtract);
        Check(resumed.Progress.Gil == 0 && JsonSerializer.Serialize(resumed.Progress) == JsonSerializer.Serialize(unit.Progress), "A cold native Gil receipt changed progress or applied its charge twice.");
    }

    private static void MenuStatisticsOwnership(Ff7Installation installation, Ff7SessionSave saved)
    {
        using Ff7WorldRuntime worldFixture = new(installation, saved.Vm.PersistentVariables.ToArray(), 1, new byte[] { 0, 1, 2 });
        using Ff7Session unit = Ff7Session.Restore(installation, saved with { World = worldFixture.Process });
        string frozen = JsonSerializer.Serialize(unit.Machine.Capture());
        long worldTick = unit.World!.Process.Tick;
        unit.PublishNativeMenuPartyStatistics();
        Check(JsonSerializer.Serialize(unit.Machine.Capture()) == frozen && unit.World.Process.Tick == worldTick, "Native menu statistics must not mutate or execute the frozen field VM or advance the world fixture.");
        Ff7SessionSave published = unit.Capture();
        Check(published.Vm.PersistentVariables.AsSpan().SequenceEqual(unit.World.Machine.CopyPersistent()), "Native menu statistics must capture the active world's actual persistent owner.");
        using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(published))!);
        Check(JsonSerializer.Serialize(resumed.Capture()) == JsonSerializer.Serialize(published), "Cold active-world menu statistics changed the persistent or native process owners.");
    }

    private static void Equal(Ff7Session original, Ff7Session cold)
    {
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The restaurant continuation diverged after JSON cold restoration.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
