using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRearmedStoryOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REARMED_STORY_SAVE") ?? throw new InvalidOperationException("Select a rearmed story fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native party menu."));
        string? first = null;
        FieldHostRequest? menuRequest = null;
        session.Host.NativeOperationExecuted += (request, _) =>
        {
            if (request.Command is FieldMenuCommand { Mode: 7, Parameter: 0 })
            {
                menuRequest = request;
            }
        };
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput result = policy.ChooseInput(driver);
            first ??= policy.State.Active?.EventId;
            return result;
        }
        Ff7AutoPlayController controller = new(session, Choose, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 30 && controller.Steps < 2000 && controller.Error is null && menuRequest is null && session.MapId == 73)
        {
            controller.Advance(1);
        }
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        Console.WriteLine($"Native rearmed story: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, first={first}, field={session.MapId}, story={story}, menu={menuRequest?.Entity}/{menuRequest?.ProgramCounter:X4}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || first != "actor:12:script:1" || session.MapId != 73 || story != 1033 || menuRequest is not { Entity: 12, Priority: 1, ProgramCounter: 0xA83 } || session.Host.Process.Menu is not { Mode: 7, Completed: false } || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The rearmed native story source did not execute its real party-menu boundary.", controller.Failure);
        }
    }
}
