using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeJunonEscapeOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_JUNON_ESCAPE_SAVE") ?? throw new InvalidOperationException("Select a native input-puzzle fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native chair puzzle has no ASK branch."));
        bool returned = false;
        session.Machine.ScriptReturned += value => returned |= value is { Entity: 3, Priority: 6, ProgramCounter: 0x09EF };
        List<int> stages = [session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x13)];
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => returned && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && session.Host.Process.Dialogues.All(dialogue => dialogue.Completed), readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 60 && controller.Steps < 3000 && controller.Running && controller.Error is null && !controller.Completed)
        {
            controller.Advance(1);
            int stage = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x13);
            if (stage != stages[^1])
            {
                stages.Add(stage);
            }
        }
        Console.WriteLine($"Native chair escape: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, stages={string.Join(',', stages)}, actualReturn={returned}, locked={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !controller.Completed || !returned || !stages.Take(6).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }) || session.MapId != 402 || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original chair puzzle did not complete through its native controls and return.", controller.Failure);
        }
    }
}
