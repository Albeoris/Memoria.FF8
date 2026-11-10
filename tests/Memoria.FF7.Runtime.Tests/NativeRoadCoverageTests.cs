using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRoadCoverageTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EVENT_COVERAGE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native road coverage: configure original resources and a standalone continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no FFVII continuation.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Check(original.Field.Name is "mds5_3" or "mds6_1", "The selected fixture is outside the reviewed road continuation.");
        Check(original.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) == 176, "The road fixture must retain the completed native reunion.");
        using Ff7AutoPlayEventCoverage coverage = new(original);
        using Ff7AutoPlayEventCoverage coldCoverage = new(cold);
        Ff7AutoplayDriver driver = new(original);
        Ff7AutoplayDriver coldDriver = new(cold);
        Ff7MidgarAutoPlayPolicy policy = new(original);
        Ff7MidgarAutoPlayPolicy coldPolicy = new(cold);
        Ff7NativeTickObservation? handoff = null;
        original.NativeTickCompleted += observation =>
        {
            if (observation.Field == "mds6_1" && observation.Transition is not null)
            {
                handoff = observation;
            }
        };
        bool inspected = false;
        int callbacks = 0;
        for (; callbacks < 1600 && original.Field.Name != "mds6_2"; callbacks++)
        {
            if (original.Field.Name == "mds6_1" && original.Host.Process.Tick > 0)
            {
                Ff7FieldEventCoveragePlan plan = Ff7FieldEventCoverageCatalog.Inspect(original);
                Check(plan.Complete && plan.Events.Count == 14, "Every native road contact and handoff must be classified before navigation.");
                Check(plan.Events.Single(target => target.Id == "line:7:script:5").Kind == Ff7FieldEventCoverageKind.Exit, "The forward native Enter callback must retain its handoff classification.");
                inspected = true;
            }
            Advance(original, coverage, driver, policy);
            Advance(cold, coldCoverage, coldDriver, coldPolicy);
            Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native road continuation diverged after a JSON cold restore.");
        }
        Check(inspected && original.Field.Name == "mds6_2", "The native physical road exit did not reach the park within its bound.");
        Check(handoff is { Transition.Map: 192, TransitionOwner.Entity: 7, TransitionOwner.Priority: 1, TransitionOwner.ProgramCounter: 0x071E }, "The park handoff must come from the exact original road contact opcode.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The standalone continuation was modified.");
        Console.WriteLine($"PASS native road catalog, original contact handoff and cold continuation ({callbacks} callbacks)");
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
