using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldControlTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_CONTROL_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original World control: configure original resources and MEMORIA_FF7_WORLD_CONTROL_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("The configured native control fixture has no World allocation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native control fixture unexpectedly requested a choice."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 512 && controller.Running && world.ReadAutoPlayControlFrontiers().Length == 0; step++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && world.ReadAutoPlayControlFrontiers().Length == 1, $"Normal native navigation did not reach a legal transport-exit source: {controller.Error}");
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayWorldControlObservation control = world.ReadAutoPlayControlFrontiers().Single();
        Ff7AutoplayInput press = world.ChooseAutoPlayControlInput(control);
        Check(press.Held == 0x40 && press.Horizontal == 0 && press.Vertical == 0, "The native transport action must begin with an ordinary short Cancel press.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Native control discovery or input selection changed authoritative state.");
        controller.Advance(1);
        Check(controller.Error is null && world.ControlledModel == control.Model && world.Process.Vehicle?.CancelHeldTicks == 1 && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldControl, Accepted: true }, "Normal planning did not own and begin the observed native transport action.");
        using MemoryStream saved = new();
        CombinedSessionCodec.Write(saved, envelope with { Ff7 = session.Capture() });
        saved.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(saved).Ff7!);
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed the native short Cancel hold or its autoplay owner.");
        using Ff7AutoPlayPolicy coldPolicy = new(cold, (_, _) => throw new InvalidOperationException("The restored native control requested a choice."));
        Ff7AutoPlayController coldController = new(cold, coldPolicy.ChooseInput, chooseDialogueSelection: coldPolicy.ChooseDialogueSelection);
        coldController.Start();
        for (int step = 0; step < 16 && coldController.Running && cold.World?.ControlledModel == control.Model; step++)
        {
            coldController.Advance(1);
        }
        Check(coldController.Error is null && cold.World?.ControlledModel == control.RiderModel, $"The native short release and collision phases did not return control to the rider: {coldController.Error}");
        coldController.Advance(1);
        Check(coldPolicy.State.Active?.Kind != Ff7AutoPlayTargetKind.WorldControl && coldPolicy.State.Coverage.Visits.SelectMany(visit => visit.Events).Any(item => item.Event.Id == control.Id && item.Completed), "Native rider ownership did not complete the observed control action.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Native control continuation changed its external standalone source.");
        Console.WriteLine($"PASS original native transport control, cold short-hold continuation and rider ownership after {coldController.Steps} callbacks; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
