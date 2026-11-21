using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaRockOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_CAVE_SAVE") ?? throw new InvalidOperationException("Select a native cave prerequisite fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Dictionary<string, Ff7SessionSave> checkpoints = [];
        using Ff7AutoPlayPolicy policy = new(session, (key, save) =>
        {
            checkpoints[key] = save;
            return "native-choice-" + key + ".ffsave";
        });
        List<string> entries = [];
        bool pushed = false;
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request is { Entity: 10, Priority: 1, ProgramCounter: 0x628, Command: FieldDialogueCommand { Window: 0, Text: 1, FirstChoice: 1, LastChoice: 2 } } && reply is { Completed: true, Value: 1 })
            {
                pushed = true;
                Console.WriteLine("Native rock prompt selected Push through its actual owner.");
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 692, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        if (policy.State.DestinationField != 692)
        {
            for (int index = 0; index < 64 && policy.State.Active is { Accepted: true } && controller.Running && controller.Error is null; index++)
            {
                controller.Advance(1);
            }
            policy.SetDestinationField(692);
        }
        Stopwatch timer = Stopwatch.StartNew();
        int map = session.MapId;
        string? first = null;
        while (timer.Elapsed.TotalSeconds < 240 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle && policy.State.PendingRestore is null)
        {
            controller.Advance(1);
            first ??= policy.State.Active?.EventId;
            if (map != session.MapId)
            {
                Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                entries.Add($"{session.MapId}:{player.Triangle}");
                map = session.MapId;
                Console.WriteLine($"Native rock route entry {entries[^1]}");
            }
        }
        bool battle = session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle && session.Host.Process.GlobalRequestId is not null;
        bool cleared = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83) & 2) != 0;
        Console.WriteLine($"Native rock route: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, initialRetained={first}, entries={string.Join(',', entries)}, pushed={pushed}, cleared={cleared}, choice={policy.State.Choice?.Key}, restore={policy.State.PendingRestore?.Path}, checkpoints={checkpoints.Count}, battle={battle}, completed={controller.Completed}, planning={controller.PlanningProgress}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || !entries.Take(2).SequenceEqual(["690:0", "691:198"]) || policy.State.PendingRestore is not null || !battle && (!controller.Completed || session.MapId != 692 || !pushed || !cleared || policy.State.Choice is not null))
        {
            throw new InvalidOperationException("The native rock prerequisite did not retain its real mechanism and onward route.", controller.Failure);
        }
    }
}
