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
        Ff7AutoplayDriver driver = new(session);
        driver.Apply(press);
        session.AdvanceTick(advanceClock: false);
        Check(world.ControlledModel == control.Model && world.Process.Vehicle?.CancelHeldTicks == 1, "The ordinary native Cancel press did not begin its short hold.");
        using MemoryStream saved = new();
        CombinedSessionCodec.Write(saved, envelope with { Ff7 = session.Capture() });
        saved.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(saved).Ff7!);
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed the native short Cancel hold or its autoplay owner.");
        Ff7AutoplayDriver coldDriver = new(cold);
        for (int step = 0; step < 2; step++)
        {
            coldDriver.Apply(cold.World!.ChooseAutoPlayControlInput(control));
            cold.AdvanceTick(advanceClock: false);
        }
        Check(cold.World?.Process.Vehicle?.DismountPhase == 2, "The native short release did not reach its collision phase.");
        string landingBefore = JsonSerializer.Serialize(cold.Capture());
        Ff7AutoPlayWorldLandingPreview landing = cold.World!.PreviewAutoPlaySurfaceLanding();
        Check(landing.Supported && landing.Rider is not null && landing.Ground is not null, "The native phase-two source has no admitted landing preview.");
        Check(landingBefore == JsonSerializer.Serialize(cold.Capture()), "Landing advice changed native VM, caches, RNG or ownership.");
        coldDriver.Apply(cold.World.ChooseAutoPlayControlInput(control));
        cold.AdvanceTick(advanceClock: false);
        Ff7WorldEntity actualRider = cold.World.Process.Entities.Single(entity => entity.Model == control.RiderModel);
        Check(actualRider.X == landing.Rider!.X && actualRider.Y == landing.Rider.Y && actualRider.Z == landing.Rider.Z && cold.World.Process.Input?.GroundTriangle == landing.Ground, $"The original native collision phase disagrees with its landing advice: actual={actualRider.X},{actualRider.Y},{actualRider.Z}/{cold.World.Process.Input?.GroundTriangle}, advised={landing.Rider.X},{landing.Rider.Y},{landing.Rider.Z}/{landing.Ground}.");
        var passive = cold.World.Machine.AnalyzeModelDispatch(control.Model, 3, currentEntryOnly: true);
        Check(passive.Complete && passive.IsInert && passive.HasDispatchHandler == false, "The configured native passive transport callback must have no handler.");
        bool passiveMoved = false;
        for (int horizontal = -1; horizontal <= 1; horizontal++)
        {
            for (int vertical = -1; vertical <= 1; vertical++)
            {
                if (horizontal == 0 && vertical == 0)
                {
                    continue;
                }
                var preview = cold.World.PreviewAutoPlayInput(new(horizontal, vertical));
                if (preview.Moved && preview.ModelBoundaries?.Any(boundary => boundary.Model == control.Model && boundary.Event == 3) == true)
                {
                    Check(preview.ModelBoundaries.All(boundary => boundary.Inert), "An absent passive callback was reported as an effectful physical boundary.");
                    passiveMoved = true;
                }
            }
        }
        var confirm = cold.World.PreviewAutoPlayInput(new(Held: 0x20));
        Check(confirm.ModelBoundaries?.Any(boundary => boundary.Model == control.Model && boundary.Event == 4 && !boundary.Inert) == true && !Ff7AutoplayDriver.AdmitsUnselectedWorldModels(confirm.ModelBoundaries), "An effectful native boarding callback was admitted as passive transit.");
        Check(passiveMoved, "The native rider fixture did not exercise an admitted movement through its passive transport contact.");
        Check(cold.World.HasAutoPlayControlCompleted(control), "The native release/collision phases did not return ownership to the rider.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Native control continuation changed its external standalone source.");
        Console.WriteLine("PASS original native transport control, cold short-hold continuation and rider ownership through native collision; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
