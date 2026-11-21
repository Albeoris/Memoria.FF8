using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeReturnedWorldOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RETURNED_WORLD_SAVE") ?? throw new InvalidOperationException("Select a returned native world activation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(stream).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during native world control return."));
        Ff7AutoPlayActiveEvent original = policy.State.Active ?? throw new InvalidOperationException("The fixture has no original world activation.");
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The fixture is not in the native world module.");
        if (!original.Accepted || policy.State.WorldDispatch is null)
        {
            throw new InvalidOperationException("The fixture has no actually admitted native world owner.");
        }
        bool handedOff = false;
        Ff7AutoplayInput handoff = default;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoPlayActiveEvent? before = policy.State.Active;
            Ff7AutoplayInput result = policy.ChooseInput(driver);
            if (before?.EventId == original.EventId && policy.State.Active is null)
            {
                handedOff = true;
                handoff = result;
            }
            return result;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (!handedOff && controller.Error is null && controller.Steps < 128 && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            Ff7WorldEntity before = world.Process.Entities.Single(entity => entity.Model == world.Process.ControlledModel);
            controller.Advance(1);
            if (handedOff)
            {
                Ff7WorldEntity after = world.Process.Entities.Single(entity => entity.Model == world.Process.ControlledModel);
                if ((before.X, before.Y, before.Z) != (after.X, after.Y, after.Z))
                {
                    throw new InvalidOperationException("World owner completion advanced a newly selected movement plan in the same callback.");
                }
            }
        }
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
        if (controller.Error is not null || !handedOff || policy.State.Active is not null || policy.State.WorldDispatch is not null || handoff.Horizontal != 0 || handoff.Vertical != 0 || handoff.Held != 0 || handoff.Talk || !world.Process.MovementEnabled || world.Process.Dialogue is { Completed: false } || coverage.Conditions?.HasExecutedEntry(original.ConditionFingerprint) != true)
        {
            throw new InvalidOperationException("Actual native world owner return did not produce a neutral completed planning handoff.", controller.Failure);
        }
        policy.SetDestinationField(353);
        if (policy.State.DestinationField != 353 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The standard destination setter failed after control return or the retained fixture changed.");
        }
        Console.WriteLine($"Native returned world: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, nativeTick={world.Process.Tick}, originalCompleted=true, neutralHandoff=true, destination={policy.State.DestinationField}");
    }
}
