using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWorldModelPortalOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MODEL_PORTAL_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), source);
        Ff7WorldRuntime world = session.World!;
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoPlayWorldModelScriptObservation target = world.ReadAutoPlayModelFrontiers().Single(target => target.Model == 3 && target.Event == 4);
        Ff7AutoPlayWorldMovementState initial = world.CaptureAutoPlayMovementState();
        Ff7AutoPlayWorldGuide guide = world.ReadAutoPlayModelNavigationGuide(target);
        Ff7WorldTerrainReference next = guide.Faces[1].Source;
        Stopwatch timer = Stopwatch.StartNew();
        int callbacks = 0;
        while (world.CaptureAutoPlayMovementState().Input.GroundTriangle != next && callbacks < 32 && timer.Elapsed.TotalSeconds < 10)
        {
            Ff7AutoPlayWorldNavigationResult result = driver.ChooseWorldModelInput(target, 64);
            if (!result.HasInput || driver.PlanningPending)
            {
                throw new InvalidOperationException("The native portal approach did not publish its admitted physical input.");
            }
            driver.Apply(result.Input);
            session.AdvanceTick();
            callbacks++;
        }
        Ff7AutoPlayWorldMovementState final = world.CaptureAutoPlayMovementState();
        if (final.Input.GroundTriangle != next || (final.Player.X, final.Player.Z) == (initial.Player.X, initial.Player.Z) || final.Player.Model != initial.Player.Model || source.Progress.CompletedBattles.Length != session.Progress.CompletedBattles.Length || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException($"The native portal approach did not reach its advised terrain face while retaining the remote model goal: ground={final.Input.GroundTriangle}, callbacks={callbacks}.");
        }
        Console.WriteLine($"Original model portal: callbacks={callbacks}, seconds={timer.Elapsed.TotalSeconds:F3}, next={next}, final=({final.Player.X},{final.Player.Z}), controlled={final.Player.Model}, remainingGoal={target.Id}, physical={driver.PhysicalPlanningWork}.");
    }
}
