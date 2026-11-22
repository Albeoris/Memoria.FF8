using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SOURCE_GOAL_SAVE")!;
        string target = Environment.GetEnvironmentVariable("MEMORIA_FF7_SOURCE_GOAL_EVENT")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The selected diagnostic source must not need a choice anchor."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        while (controller.Error is null && policy.State.Active is { Accepted: true } && controller.Steps < 128)
        {
            controller.Advance(1);
        }
        Ff7SessionSave before = session.Capture();
        bool bound = false;
        for (int attempt = 0; attempt < 128 && controller.Error is null; attempt++)
        {
            try
            {
                policy.SetSourceGoal(target);
                bound = true;
                break;
            }
            catch (InvalidOperationException error) when (error.Message == "The current native event must return before changing its planning destination.")
            {
                controller.Advance(1);
                before = session.Capture();
            }
        }
        if (!bound)
        {
            throw new InvalidOperationException("The native source did not become ready for goal binding.", controller.Failure);
        }
        Ff7SessionSave after = session.Capture();
        if (JsonSerializer.Serialize(before.Vm) != JsonSerializer.Serialize(after.Vm) || JsonSerializer.Serialize(before.Process) != JsonSerializer.Serialize(after.Process) || JsonSerializer.Serialize(before.Progress) != JsonSerializer.Serialize(after.Progress))
        {
            throw new InvalidOperationException("Pinning a native source changed native gameplay state.");
        }
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && !controller.Paused && controller.Steps < 4096 && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        Ff7AutoPlaySourceGoal goal = policy.State.SourceGoal!;
        if (controller.Error is not null || !controller.Paused || !goal.Completed || policy.State.Active is not null || policy.State.Choice is not null || policy.State.PendingRestore is not null || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || (session.Machine.Variables.ReadByte(FieldBank.Persistent4Byte, 0x84) & 1) == 0 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException($"The selected native switch did not pause after its genuine completion: steps={controller.Steps}, target={policy.State.Active?.EventId}, goal={goal}, status={policy.State.Status}, error={controller.Error}.", controller.Failure);
        }
        long tick = session.Machine.Tick;
        controller.Advance(4);
        if (session.Machine.Tick != tick)
        {
            throw new InvalidOperationException("The completed single-source goal emitted another gameplay callback.");
        }
        policy.SetDestinationField(421);
        if (policy.State.SourceGoal is not null || policy.State.DestinationField != 421)
        {
            throw new InvalidOperationException("The completed source could not hand off to a normal destination.");
        }
        Console.WriteLine($"Original source goal: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={tick}, goal={goal.EventId}, completed={goal.Completed}, paused={controller.Paused}, switch={session.Machine.Variables.ReadByte(FieldBank.Persistent4Byte, 0x84):X2}, nextDestination=421.");
    }
}
