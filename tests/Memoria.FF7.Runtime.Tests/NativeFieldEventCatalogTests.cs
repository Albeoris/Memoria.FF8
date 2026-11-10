using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeFieldEventCatalogTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EVENT_COVERAGE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native field catalog continuation: configure original resources and a standalone coverage fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The coverage fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(original.Field.Name is "ealin_2" or "ealin_1" or "eals_1" or "mds5_1" or "mds5_3", "The selected fixture is outside the reviewed field coverage sequence.");
        using Ff7AutoPlayEventCoverage coverage = new(original);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(original);
        Ff7AutoplayDriver coldDriver = new(cold);
        Ff7MidgarAutoPlayPolicy policy = new(original);
        Ff7MidgarAutoPlayPolicy coldPolicy = new(cold);
        int callbacks = 0;
        bool checkedMidSceneCold = false;
        if (original.Field.Name == "ealin_2")
        {
            Ff7FieldEventCoveragePlan upstairs = Ff7FieldEventCoverageCatalog.Inspect(original);
            Check(upstairs.Complete && upstairs.WalkOnly, "The upstairs fixture requires its complete native walking catalog.");
            Check(upstairs.Events.Count(target => target.Id.StartsWith("line:", StringComparison.Ordinal)) == 36, "Every upstairs LINE contact slot needs classification.");
            bool rewardAvailable = upstairs.Events.Single(target => target.Id == "actor:15:script:1").Kind == Ff7FieldEventCoverageKind.Interaction;
            int[] before = [Quantity(original, 0), Quantity(original, 7)];
            Cover("ealin_2", 1800);
            Check(coverage.Capture().Current!.Events.Single(target => target.Id == "line:9:script:2").Status == AutoPlayEventCoverageStatus.Completed, "The bed requires its observed native return.");
            Check(coverage.Capture().Current!.Events.Single(target => target.Id == "line:11:script:2").Status == AutoPlayEventCoverageStatus.Completed && coverage.Capture().Current!.Events.Single(target => target.Id == "line:14:script:2").Status == AutoPlayEventCoverageStatus.Completed, "Each finite room reset requires its exact native child handoff before departure.");
            Check(Quantity(original, 0) == (rewardAvailable ? Math.Min(99, before[0] + 1) : before[0]) && Quantity(original, 7) == (rewardAvailable ? Math.Min(99, before[1] + 1) : before[1]), "The one-time drawer reward was lost or duplicated.");
            Leave("ealin_2", "ealin_1", 450);
        }
        if (original.Field.Name == "ealin_1")
        {
            Cover("ealin_1", 300);
            Check(coverage.Capture().Current!.Events.Single(target => target.Id == "line:14:script:2").Status == AutoPlayEventCoverageStatus.Completed, "The downstairs warning requires native completion before departure.");
            Leave("ealin_1", "eals_1", 450);
        }
        if (original.Field.Name == "eals_1")
        {
            int etherBefore = Quantity(original, 3);
            int coverBefore = original.Progress.Materia.Count(materia => materia.MateriaId == 16);
            byte rewardsBefore = original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0x30);
            Cover("eals_1", 1800);
            Check(Quantity(original, 3) == ((rewardsBefore & 4) == 0 ? Math.Min(99, etherBefore + 1) : etherBefore), "The native garden Ether reward was lost or duplicated.");
            Check(original.Progress.Materia.Count(materia => materia.MateriaId == 16) == ((rewardsBefore & 8) == 0 ? coverBefore + 1 : coverBefore), "The native garden Cover reward was lost or duplicated.");
            Check(coverage.Capture().Current!.Events.Count(entry => entry.Status == AutoPlayEventCoverageStatus.Completed) >= 3, "Garden departure requires its native instruction and save-area contact returns.");
            Leave("eals_1", "mds5_1", 450);
        }
        if (original.Field.Name == "mds5_1")
        {
            Cover("mds5_1", 1200);
            Check(coverage.Capture().Current!.Events.Single(entry => entry.Id == "actor:15:script:1") is { Status: AutoPlayEventCoverageStatus.Completed, Outcome: not null }, "The dog's original party facing, bark, movement and wait must reach their native script return.");
            Leave("mds5_1", "mds5_3", 700);
        }
        Cover("mds5_3", 1600);
        Check(original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 176 && original.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.MapByte, 2) == 1 && !original.Host.Process.PlayerLocked, "The native reunion did not complete its original state/control publications.");
        Check(coverage.Capture().Current!.Events.Single(entry => entry.Id == "conditional:2:script:0").Status == AutoPlayEventCoverageStatus.Completed, "A triangle-conditioned scene requires its exact native default-owner return.");
        Check(checkedMidSceneCold, "The conditional scene did not verify a cold resume between its story write and native completion.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The coverage fixture was modified.");
        Console.WriteLine($"PASS native field catalogs, physical rewards/stairs/warnings/save area and cold continuation ({callbacks} callbacks)");

        bool CurrentComplete(string field)
        {
            return coverage.CanLeaveField && coverage.Capture().Current!.Id.StartsWith(field + ":", StringComparison.Ordinal);
        }

        void Tick(bool navigation)
        {
            string? resumedNext = null;
            if (!checkedMidSceneCold && original.Field.Name == "mds5_3" && original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 176 && coverage.Capture().Current?.Events.Any(entry => entry.Status == AutoPlayEventCoverageStatus.Active && entry.Id == "conditional:2:script:0") == true)
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Advance(resumed, resumedCoverage, new(resumed));
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedMidSceneCold = true;
            }
            Advance(original, coverage, driver, navigation ? policy : null);
            Advance(cold, coldCoverage, coldDriver, navigation ? coldPolicy : null);
            callbacks++;
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold physical coverage diverged from native state or input.");
            Check(resumedNext is null || JsonSerializer.Serialize(original.Capture()) == resumedNext, "A mid-scene cold resume changed an active native conditional owner or replayed its completed prefix.");
        }

        void Cover(string field, int limit)
        {
            long generation = original.FieldGeneration;
            int ticks = 0;
            while (!CurrentComplete(field) && ticks++ < limit)
            {
                Tick(false);
                long currentGeneration = original.FieldGeneration;
                bool ownedReset = field == "ealin_2" && original.Field.Name == field && currentGeneration > generation && currentGeneration <= generation + 2 && coverage.Capture().Current!.Events.Where(entry => entry.Id is "line:11:script:2" or "line:14:script:2").Count(entry => entry.Status == AutoPlayEventCoverageStatus.Completed) >= currentGeneration - generation;
                Check(original.Field.Name == field && (original.FieldGeneration == generation || ownedReset), "A coverage interaction crossed an unproved native field handoff.");
            }
            Check(CurrentComplete(field) && ticks < limit, "The native interactions did not complete within their bounded continuation.");
            Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(original);
            Check(plan.Complete && plan.Events.Count(target => target.Id.StartsWith("line:", StringComparison.Ordinal)) == original.Host.Process.Lines.Length * 6, "The current native LINE slots lack a complete source-reviewed classification.");
        }

        void Leave(string field, string destination, int limit)
        {
            long generation = original.FieldGeneration;
            int ticks = 0;
            while (original.FieldGeneration == generation && ticks++ < limit)
            {
                Tick(true);
            }
            Check(original.Field.Name == destination && original.FieldGeneration == generation + 1 && ticks < limit, $"The physical {field} exit failed or triggered a competing native handoff.");
        }
    }
    private static int Quantity(Ff7Session session, int id) => session.Progress.Inventory.SingleOrDefault(item => item.ItemId == id)?.Quantity ?? 0;

    private static void Advance(Ff7Session session, Ff7AutoPlayEventCoverage coverage, Ff7AutoplayDriver driver, Ff7MidgarAutoPlayPolicy? policy = null)
    {
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            session.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span)));
        }
        if (!coverage.TryChooseInput(driver, out Ff7AutoplayInput input) && policy is not null)
        {
            policy.TryChooseInput(driver, out input);
        }
        driver.Apply(input);
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
