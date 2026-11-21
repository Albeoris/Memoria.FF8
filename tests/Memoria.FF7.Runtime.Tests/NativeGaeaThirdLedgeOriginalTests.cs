using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaThirdLedgeOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_LEDGE_SAVE") ?? throw new InvalidOperationException("Select a native cliff continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The current cliff unexpectedly requested a choice checkpoint."));
        policy.SetDestinationField(690);
        List<int> landings = [];
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldActorLadder ladder && reply.Completed && session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle == ladder.Triangle)
            {
                landings.Add(ladder.Triangle);
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 690, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        string? first = null;
        for (int index = 0; index < 2400 && timer.Elapsed.TotalSeconds < 120 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle; index++)
        {
            controller.Advance(1);
            first ??= policy.State.Active?.EventId;
        }
        bool battle = session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle && session.Host.Process.GlobalRequestId is not null;
        Console.WriteLine($"Native interrupted cliff continuation: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, first={first}, field={session.MapId}, landings={string.Join(',', landings)}, battle={battle}, completed={controller.Completed}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || first != "line:4:entity:10:script:4" || controller.Error is not null || !battle && (!controller.Completed || session.MapId != 690 || !landings.SequenceEqual([7, 10])))
        {
            throw new InvalidOperationException("The interrupted native component route did not resume its authentic continuation.", controller.Failure);
        }
    }
}
