using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaWarmthOriginalTests
{
    internal static void ReadProfiles()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        foreach (string name in new[] { "gaia_1", "gaia_2" })
        {
            Ff7FieldResources field = Ff7FieldResources.Load(installation, name);
            Console.WriteLine($"Native cliff {name}: {FieldProgram.FromPcScripts(field.Scripts).Identity}");
        }
    }

    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_WARMTH_SAVE") ?? throw new InvalidOperationException("Select a native cliff ascent fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native first cliff unexpectedly requested a choice checkpoint."));
        if (Ff7AutoPlayPolicy.ReadNativeGaeaWarmthInput(session.Machine.Capture(), session.Host.Process, session.Machine.Variables, session.Machine.Input) is null)
        {
            throw new InvalidOperationException("The current native temperature HUD blocked its live warmth worker.");
        }
        List<int> landings = [];
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldActorLadder ladder && reply.Completed && session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle == ladder.Triangle)
            {
                landings.Add(ladder.Triangle);
                Console.WriteLine($"Native climb completed: triangle={ladder.Triangle}, temperature={session.Machine.Variables.ReadWord(FieldBank.MapWord, 9)}");
            }
        };
        policy.SetDestinationField(690);
        int pulses = 0;
        bool reachedWarm = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput selected = policy.ChooseInput(driver);
            if (session.MapId == 689)
            {
                reachedWarm |= session.Machine.Variables.ReadWord(FieldBank.MapWord, 9) == 38;
                if ((selected.Held & 0x80) != 0)
                {
                    pulses++;
                }
            }
            return selected;
        }
        Ff7AutoPlayController controller = new(session, Choose, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 690, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int poll = 0; poll < 6000 && timer.Elapsed.TotalSeconds < 180 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle; poll++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native cliff warmth: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, pulses={pulses}, reached38={reachedWarm}, ladders={string.Join(',', landings)}, status={policy.Status}, completed={controller.Completed}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native cliff fixture changed during continuation.");
        }
        bool battleBoundary = session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle && session.Host.Process.GlobalRequestId is not null && landings.Take(3).SequenceEqual([0, 3, 5]);
        bool finished = controller.Completed && session.MapId == 690 && landings.SequenceEqual([0, 3, 5, 7, 10]);
        if ((!finished && !battleBoundary) || controller.Error is not null || session.World is not null || !reachedWarm || pulses < 12)
        {
            throw new InvalidOperationException("The native warmed ascent reached neither its real field return nor the owned battle boundary.", controller.Failure);
        }
    }
}
