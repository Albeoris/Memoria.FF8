using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeParkCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EVENT_COVERAGE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native park coverage: configure original resources and a standalone continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(original.Field.Name == "mds6_2", "The selected fixture is outside the reviewed park continuation.");
        Check(original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) is 176 or 179 or 183 or 185, "The park fixture must retain a reviewed native scene phase.");
        using Ff7AutoPlayEventCoverage coverage = new(original);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(original);
        Ff7AutoplayDriver coldDriver = new(cold);
        Ff7MidgarAutoPlayPolicy policy = new(original);
        Ff7MidgarAutoPlayPolicy coldPolicy = new(cold);
        bool alreadyComplete = original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 185;
        bool checkedMidScene = false;
        int callbacks = 0;
        for (; !alreadyComplete && callbacks < 3500; callbacks++)
        {
            string? resumedNext = null;
            if (!checkedMidScene && original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 183 && original.Host.Process.PlayerLocked)
            {
                using Ff7Session resumed = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(original.Capture()))!);
                using Ff7AutoPlayEventCoverage resumedCoverage = new(resumed);
                Advance(resumed, resumedCoverage, new(resumed), new(resumed));
                resumedNext = JsonSerializer.Serialize(resumed.Capture());
                checkedMidScene = true;
            }
            Advance(original, coverage, driver, policy);
            Advance(cold, coldCoverage, coldDriver, coldPolicy);
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native park continuation diverged after a JSON cold restore.");
            Check(resumedNext is null || resumedNext == JsonSerializer.Serialize(original.Capture()), "A post-story-write cold restore changed the active native park scene.");
            if (original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 185 && !original.Host.Process.PlayerLocked)
            {
                callbacks++;
                break;
            }
        }
        var state = coverage.Capture();
        var scopes = (state.History ?? []).Append(state.Current!).Where(scope => scope.Id.StartsWith("mds6_2:", StringComparison.Ordinal)).ToArray();
        Check(scopes.Any(scope => scope.Events.Any(entry => entry.Id == "conditional:0:phase:entry" && entry.Status == Memoria.AutoPlay.AutoPlayEventCoverageStatus.Completed)), "The native arrival scene did not publish its unlocked intermission witness.");
        Check(scopes.Any(scope => scope.Events.Any(entry => entry.Id == "actor:2:script:1" && entry.Status == Memoria.AutoPlay.AutoPlayEventCoverageStatus.Completed && entry.Outcome!.Contains("0822", StringComparison.Ordinal))), "The actual intermission conversation did not retain its original return.");
        Check(scopes.Any(scope => scope.Events.Any(entry => entry.Id == "conditional:0:phase:departure" && entry.Status == Memoria.AutoPlay.AutoPlayEventCoverageStatus.Completed && entry.Outcome!.Contains("05D3", StringComparison.Ordinal))), "The final native scene return was not observed before departure.");
        Check((alreadyComplete || checkedMidScene) && original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 185 && !original.Host.Process.PlayerLocked && !original.Host.Process.MenuDisabled, "The park scene did not finish its native state/control publications within the bound.");
        int departureCallbacks = 0;
        bool inspectedCorridor = false;
        for (; departureCallbacks < 1600 && original.Field.Name != "mrkt2"; departureCallbacks++)
        {
            if (original.Field.Name == "mds6_3" && original.Host.Process.Tick > 0)
            {
                Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(original);
                Check(plan.Complete && plan.Events.Count == 8 && plan.Events.Single(target => target.Id == "line:4:script:5").Kind == Ff7FieldEventCoverageKind.Exit, "Every native corridor contact and alternative handoff must be classified before departure.");
                inspectedCorridor = true;
            }
            Advance(original, coverage, driver, policy);
            Advance(cold, coldCoverage, coldDriver, coldPolicy);
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native park departure diverged after a JSON cold restore.");
        }
        Check(inspectedCorridor && original.Field.Name == "mrkt2", "The original park and corridor gateways did not reach the market within their bound.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The standalone continuation was modified.");
        Console.WriteLine($"PASS native park scene phases, actual conversation and cold continuation ({callbacks} scene callbacks, {departureCallbacks} departure callbacks)");
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
