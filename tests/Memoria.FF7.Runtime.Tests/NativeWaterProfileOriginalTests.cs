using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWaterProfileOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_WATER_PROFILE_SAVE") ?? throw new InvalidOperationException("Select a native water profile fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var geometry = session.Host.NativeWaterGeometryCapture!;
        bool firstSample = true;
        bool animatedProfile = false;
        bool ordinalColors = false;
        session.Host.NativeWaterGeometryCapture = (actor, camera, parameters) =>
        {
            if (firstSample)
            {
                Console.WriteLine($"Actual native water sample: tick{session.Machine.Tick}, actor{actor.Entity}, animation{actor.Animation}, profile{parameters.EnvironmentProfile}");
                firstSample = false;
            }
            var sample = geometry(actor, camera, parameters);
            animatedProfile |= parameters.EnvironmentProfile == 6 && actor.Animation != 0;
            ordinalColors |= sample.ColorRegion is { ProfileSix: true } && sample.Parts.Count > 0 && sample.Parts.All(part => part.BlueOffsets?.Length == part.ModifiedVertices.Length);
            return sample;
        };
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 1000 && timer.Elapsed.TotalSeconds < 20 && controller.Error is null && session.MapId == 641 && session.Host.Process.Movie?.PreparedId != 51; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native water continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, story={session.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0)}, control={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, director={session.Machine.Capture().Entities[0].ProgramCounter:X4}, status={policy.State.Status}, error={controller.Error}");
        bool sourceUnchanged = hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)));
        var captured = session.Capture();
        using var cold = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), captured);
        bool coldColors = System.Text.Json.JsonSerializer.Serialize(captured.Process.Actors.Select(actor => actor.NativeWaterEffect)) == System.Text.Json.JsonSerializer.Serialize(cold.Host.Process.Actors.Select(actor => actor.NativeWaterEffect));
        if (!sourceUnchanged || !animatedProfile || !ordinalColors || !coldColors || controller.Error is not null || session.Host.Process.Movie?.PreparedId != 51)
        {
            throw new InvalidOperationException("The native water color lifecycle did not preserve its sampled cold state and reach the movie boundary, or changed the original fixture.");
        }
    }
}
