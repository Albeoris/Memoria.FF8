using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldFacingSearchTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original world resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_SEARCH_SAVE") ?? throw new InvalidOperationException("Configure an external world search continuation.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Ff7WorldRuntime world = session.World!;
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        Ff7AutoPlayWorldMovementState facing = source with { Player = source.Player with { Direction = (source.Player.Direction + 1024) & 4095, TurnOffset = 256 } };
        Check(Ff7AutoplayDriver.WorldMovementKey(source) != Ff7AutoplayDriver.WorldMovementKey(facing), "Exact route validation lost native presentation facing.");
        Check(Ff7AutoplayDriver.WorldSearchKey(source) == Ff7AutoplayDriver.WorldSearchKey(facing), "Presentation facing still splits physical search states.");
        Check(Ff7AutoplayDriver.WorldSearchKey(source) != Ff7AutoplayDriver.WorldSearchKey(source with { Player = source.Player with { TargetDirection = (source.Player.TargetDirection + 1024) & 4095 } }), "Search discarded the free-camera fallback's required target heading.");
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(session.Capture());
        for (int horizontal = -1; horizontal <= 1; horizontal++)
        {
            for (int vertical = -1; vertical <= 1; vertical++)
            {
                Ff7AutoplayInput action = new(horizontal, vertical, Run: true);
                Ff7AutoPlayWorldMovementPreview first = world.PreviewAutoPlayInput(action, source);
                Ff7AutoPlayWorldMovementPreview second = world.PreviewAutoPlayInput(action, facing);
                Check(first.Supported && second.Supported && first.State is not null && second.State is not null, "The supplied continuation does not admit ordinary surface navigation.");
                Check(Ff7AutoplayDriver.WorldSearchKey(first.State!) == Ff7AutoplayDriver.WorldSearchKey(second.State!) && first.TerrainBoundary == second.TerrainBoundary, "Presentation facing changed a native physical successor.");
            }
        }
        Check(before.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(session.Capture())), "Comparing physical search states changed native state or RNG.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the world frontier."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 256 && controller.Running && session.FieldGeneration == saved.FieldGeneration; step++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && session.FieldGeneration != saved.FieldGeneration, $"Normal world policy did not complete its native field handoff: {controller.Error ?? policy.State.Status}");
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "The original world search continuation changed.");
        Console.WriteLine($"PASS exact facing retention, native physical equivalence and original world frontier handoff after {controller.Steps} steps to field {session.MapId}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
