using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRoofExitOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_EXIT_SAVE") ?? throw new InvalidOperationException("Select a native ambient spatial-exit fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The spatial exit requires no choice checkpoint."));
        bool selected = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            if (session.FieldGeneration == source.FieldGeneration && policy.State.Active is { } active)
            {
                if (active.Kind != Ff7AutoPlayTargetKind.AutonomousSpatial || active.Entity != 0 || active.SpatialOwner is not { TargetEntity: 4, TargetScript: 1, TargetPriority: 2 })
                {
                    throw new InvalidOperationException($"The ambient spatial exit lost to {active.EventId}.");
                }
                selected = true;
            }
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && session.FieldGeneration == source.FieldGeneration && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Console.WriteLine($"Native spatial exit: selected={selected}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, triangle={player?.Triangle}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !selected || session.MapId != 356 || session.FieldGeneration <= source.FieldGeneration || session.Progress.CompletedBattles.Length < source.Progress.CompletedBattles.Length || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original ambient party-qualified triangle predicate did not own its actual field exit.", controller.Failure);
        }
    }
}
