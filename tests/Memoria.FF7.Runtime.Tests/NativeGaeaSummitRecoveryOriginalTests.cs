using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaSummitRecoveryOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_SUMMIT_SAVE") ?? throw new InvalidOperationException("Select a native summit recovery fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native summit recovery must not request a choice checkpoint."));
        policy.SetDestinationField(698);
        List<FieldHostRequest> recoveries = [];
        FieldHostRequest? battleBoundary = null;
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldPartyRecovery { AllCharacters: true } && reply.Completed)
            {
                recoveries.Add(request);
            }
            if (request.Command is FieldBattleCommand)
            {
                battleBoundary = request;
            }
        };
        bool returned = false;
        string? first = null;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput selected = policy.ChooseInput(driver);
            first ??= policy.State.Active?.EventId;
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
            returned |= Ff7AutoPlayPolicy.HasCompletedNativeSummitRecovery(policy.State, observation, session.Host.Process);
            return selected;
        }
        Ff7AutoPlayController controller = new(session, Choose, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 698, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 30 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native summit recovery: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, first={first}, recoveries={recoveries.Count}, request={recoveries.FirstOrDefault()?.Entity}/{recoveries.FirstOrDefault()?.ProgramCounter:X4}, returned={returned}, field={session.MapId}, battle={battleBoundary?.Command}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        bool nativeBattle = session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle && battleBoundary is not null && session.Host.Process.GlobalRequestId == battleBoundary.Id;
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || recoveries.Count != 1 || recoveries[0] is not { Entity: >= 3 and <= 5, Priority: 1 } recovery || recovery.ProgramCounter != 0x377 + (recovery.Entity - 3) * 0x32 || !returned || session.MapId != 698 && !nativeBattle || session.World is not null)
        {
            throw new InvalidOperationException("The native summit recovery did not return through its actual source before onward travel.", controller.Failure);
        }
    }
}
