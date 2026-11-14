using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldCheckpointTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(checkpoint))
        {
            Console.WriteLine("SKIP original world checkpoint: configure MEMORIA_FF7_GAME_DIR and MEMORIA_FF7_WORLD_CHECKPOINT.");
            return;
        }
        byte[] source = File.ReadAllBytes(checkpoint);
        using MemoryStream input = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The configured world checkpoint has no FFVII state.");
        Check(saved.World is not null && saved.Battle is null, "The checkpoint must contain a standalone world continuation outside battle.");
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Ff7Session original = Ff7Session.Restore(installation, saved);
        using Ff7Session restored = Ff7Session.Restore(installation, Cold(original.Capture()));
        Check(original.CanSave && restored.CanSave && original.CanSaveMemoria && restored.CanSaveMemoria, "The original standalone must retain native and exact save permission after cold restore.");
        Check(original.CanUsePlayerMenu, $"The original world must expose the party menu at its idle saveable boundary (control lock {original.World!.Process.ControlLock}, pending tick {original.HasPendingTick}).");
        Ff7WorldEntity before = Player(original);
        string progression = JsonSerializer.Serialize(original.Progress);
        long tick = original.World!.Process.Tick;
        int callbacks = 0;
        bool movedPosition = false;
        foreach ((int horizontal, int vertical) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
        {
            original.World!.SetMovement(horizontal, vertical, true);
            restored.World!.SetMovement(horizontal, vertical, true);
            for (int i = 0; i < 4; i++)
            {
                original.AdvanceTick(advanceClock: false);
                restored.AdvanceTick(advanceClock: false);
                callbacks++;
                Ff7WorldEntity movedPlayer = Player(original);
                movedPosition |= movedPlayer.X != before.X || movedPlayer.Z != before.Z;
                Check(original.World is not null && restored.World is not null && original.Battle is null && restored.Battle is null, "The bounded movement must remain in the world outside battle.");
                Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Cold world continuation must preserve movement, terrain, EV, random, camera and progress owners on every callback.");
            }
        }
        Check(original.World!.Process.Tick == tick + callbacks && movedPosition, "Ordinary controls must advance the original world and change its controlled model's actual XZ position.");
        Check(JsonSerializer.Serialize(original.Progress) == progression, "Bounded world movement must preserve recorded progression when the external clock is unchanged.");
        original.World.SetMovement(0, 0, true);
        restored.World!.SetMovement(0, 0, true);
        int radar = original.World.Process.RadarMode;
        int view = original.World.Process.ViewMode;
        bool cameraChanged = false;
        foreach (ushort button in new ushort[] { 0x100, 1, 4, 0x100, 1 })
        {
            foreach (ushort held in new ushort[] { button, 0 })
            {
                original.World.SetButtons(held, held);
                restored.World.SetButtons(held, held);
                original.AdvanceTick(advanceClock: false);
                restored.AdvanceTick(advanceClock: false);
                callbacks++;
                cameraChanged |= original.World.Process.ViewMode != view;
                Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Native map, radar and camera controls must preserve cold continuation on press and release.");
            }
        }
        Check(original.World.Process.RadarMode == (radar + 2) % 3 && cameraChanged, "The original map/radar buttons and camera controls must change their native owners.");
        original.AdvanceTick(advanceClock: false);
        Ff7SessionSave moved = Cold(original.Capture());
        using Ff7Session resumed = Ff7Session.Restore(installation, moved);
        Check(resumed.CanSave && resumed.CanSaveMemoria, "A moved standalone must retain native save permission after cold restoration.");
        original.AdvanceTick(advanceClock: false);
        resumed.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(resumed.Capture()), "A newly captured world standalone must continue identically without replaying initialization.");
        Check(File.ReadAllBytes(checkpoint).AsSpan().SequenceEqual(source), "Checkpoint validation must leave the supplied standalone unchanged.");
        WorldCameraOcclusionTests.Run(installation, saved);
        Console.WriteLine($"PASS original standalone world movement, native save permission and exact cold continuation ({callbacks + 2} callbacks)");
    }

    private static Ff7WorldEntity Player(Ff7Session session)
    {
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The session left the world.");
        return world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
    }

    private static Ff7SessionSave Cold(Ff7SessionSave save)
    {
        return JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
