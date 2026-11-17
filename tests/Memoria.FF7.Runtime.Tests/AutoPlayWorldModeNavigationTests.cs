using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldModeNavigationTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_CONTROL_SAVE");
        string? targetId = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODE_TARGET");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(targetId))
        {
            Console.WriteLine("SKIP original vehicle-to-rider route: configure resources, standalone and native target identity.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), envelope.Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("The configured mode-route fixture has no World allocation.");
        string[] parts = targetId.Split(':');
        Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(int.Parse(parts[1]), int.Parse(parts[2])).Scripts.Single(item => item.Id == targetId);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayWorldModeGuide guide = world.ReadAutoPlaySurfaceVehicleGuide(target, (source, predecessor) => world.PreviewAutoPlaySurfaceLandingAtFace(source, predecessor).Ground);
        Check(guide.TransportGuide.Faces.Length > 0 && guide.RiderGuide.Faces.Length > 0 && guide.RiderLanding is not null && guide.RiderReachableFaces.Contains(guide.RiderLanding), $"No vehicle approach and admitted rider landing reaches the original native entry: {guide.Reason}, expanded={guide.Expanded}, limited={guide.Limited}");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Mode route or landing advice changed authoritative VM, process, RNG or input.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native mode route unexpectedly requested a choice."));
        string nativeBeforeGoal = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        policy.SetDestinationField(target.FieldEntries.Select(entry => entry.Destination!.Field).Distinct().Single());
        Check(nativeBeforeGoal == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }), "Selecting a planning destination changed native gameplay facts.");
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        controller.Advance(1);
        Check(controller.Error is null && policy.State.Active is { WorldModeGoal: not null, WorldControl: null }, $"The shared planner did not select the directed native vehicle/rider approach: {controller.Error}");
        Ff7AutoPlayWorldModeGoal selected = policy.State.Active!.WorldModeGoal!;
        Ff7AutoPlayWorldScriptObservation selectedTarget = world.ReadAutoPlayFrontierCatalog(selected.Chunk.X, selected.Chunk.Z).Scripts.Single(item => item.Id == selected.TargetEventId);
        var selectedEntry = world.ReadAutoPlayMeshEntryForModel(selectedTarget, selected.RiderModel);
        Console.WriteLine($"selected={selected.TargetEventId} actual={string.Join(',', selectedEntry.Transitions.Select(transition => $"{transition.Selector}:{(transition.Selector is ushort selector ? world.ResolveFieldEntry(selector).Map : -1)}"))}");
        Check(selectedTarget.FieldEntries.Any(entry => entry.Destination is not null && target.FieldEntries.Any(expected => expected.Destination?.Field == entry.Destination.Field)), $"The native candidate ordering selected {selected.TargetEventId}, fields={string.Join(',', selectedTarget.FieldEntries.Select(entry => entry.Destination?.Field))}, before the configured nearest unseen entry.");
        Check(world.ControlledModel != guide.RiderModel && world.Process.Vehicle?.CancelHeldTicks == 0, "The planner exited before reaching the selected rider component.");
        using MemoryStream captured = new();
        CombinedSessionCodec.Write(captured, envelope with { Ff7 = session.Capture() });
        captured.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(captured).Ff7!);
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed a retained mode goal or native execution state.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Mode route advice changed its external standalone source.");
        Console.WriteLine($"PASS original vehicle/rider route: vehicle={guide.TransportGuide.Faces.Length} faces, rider={guide.RiderGuide.Faces.Length}, component={guide.RiderReachableFaces.Length}, expanded={guide.Expanded}; landing={guide.LandingSource}->{guide.RiderLanding}; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
