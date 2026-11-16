using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayDirectSpatialTransitionTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original field resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_TRANSITION_SAVE") ?? throw new InvalidOperationException("Configure an external automatic spatial transition source.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        session.AdvanceTick(advanceClock: false);
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(session.Capture());
        Ff7AutoPlayObservation observed = new Ff7AutoPlayDiscovery(session).Observe();
        Check(observed.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.DestinationField == session.MapId), "A direct automatic native self-transition remains absent from discovery.");
        Check(before.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(session.Capture())), "Spatial discovery changed native state, progress or RNG.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the spatial boundary."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 256 && controller.Running && session.Battle is null && session.FieldGeneration == saved.FieldGeneration; step++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && session.FieldGeneration != saved.FieldGeneration, $"Normal spatial planning did not reach its native boundary: {controller.Error ?? policy.State.Status}");
        controller.Advance(1);
        Check(policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Any(item => item.Completed && item.Event.Id.StartsWith("spatial:", StringComparison.Ordinal)), "The actual direct spatial boundary was not recorded as completed.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The original spatial source changed.");
        Console.WriteLine($"PASS original direct spatial transition discovery, native handoff and completion after {controller.Steps} steps");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
