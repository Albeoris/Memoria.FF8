using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaMoviePrerequisiteOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_MOVIE_SAVE") ?? throw new InvalidOperationException("Select a native ascent and movie fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native ascent/movie source has no choice checkpoint."));
        if (policy.State.DestinationField != 700)
        {
            policy.SetDestinationField(700);
        }
        bool landed = false;
        bool reversed = false;
        FieldHostRequest? climb = null;
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldActorLadder ladder)
            {
                if (ladder.Triangle == 2)
                {
                    climb = request;
                }
                landed |= ladder.Triangle == 2 && reply.Completed && session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle == 2;
                reversed |= ladder.Triangle == 35;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool started = false;
        while (timer.Elapsed.TotalSeconds < 30 && controller.Running && controller.Error is null && !started && session.Host.Process.GlobalCommand is not (Ff7GlobalCommand.Movie or Ff7GlobalCommand.Battle))
        {
            controller.Advance(1);
            started = session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Ladder is { TargetTriangle: 2, Step: > 0 } || landed;
        }
        Ff7Actor controlled = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native ascent prerequisite: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, request={climb?.Entity}/{climb?.Priority}/{climb?.ProgramCounter:X4}, step={controlled.Ladder?.Step}/{controlled.Ladder?.Steps}, landed2={landed}, reverse35={reversed}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || session.MapId != 695 || !started || reversed || climb is not { Entity: 8, Priority: 6, ProgramCounter: 0x3AA, Command: FieldActorLadder { Triangle: 2, Mode: 1, Animation: 3, Direction: 128, AnimationDivisor: 1 } })
        {
            throw new InvalidOperationException("The native ascent did not start its real forward ladder.", controller.Failure);
        }
    }
}
