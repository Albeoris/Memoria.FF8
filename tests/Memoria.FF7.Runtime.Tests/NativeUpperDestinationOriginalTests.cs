using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeUpperDestinationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_UPPER_DESTINATION_SAVE") ?? throw new InvalidOperationException("Select a native upper destination fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        HashSet<int> produced = [];
        Dictionary<long, (int Pc, int? Preferred, int? Selected)> selected = [];
        Ff7NativeGatewayDeparture? departure = null;
        session.NativeTickCompleted += _ =>
        {
            if (session.MapId == 628 && session.Host.Process.Actors.Single(actor => actor.PlayerControlled) is { Jump.Completed: true } player)
            {
                produced.Add(player.Triangle);
            }
        };
        session.NativeFieldDeparting += _ => departure = session.Capture().GatewayDeparture;
        int? Choose(Ff7Dialogue dialogue)
        {
            var source = session.Capture();
            int? preferred = Ff7AutoPlayChoiceWorkflow.ReadVinesChoicePreference(source, policy.State, dialogue);
            int pc = Ff7AutoPlayChoiceWorkflow.ReadNativeChoiceOwner(source, dialogue)?.ProgramCounter ?? -1;
            int? selection = policy.ChooseDialogueSelection(dialogue);
            if (!selected.ContainsKey(dialogue.RequestId))
            {
                selected.Add(dialogue.RequestId, (pc, preferred, selection));
                Console.WriteLine($"Native vine ASK t{session.Machine.Tick} pc{pc:X4}: preferred={preferred}, selected={selection}");
            }
            return selection;
        }
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: Choose, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (session.MapId == 628 && controller.Error is null && controller.Steps < 10000 && timer.Elapsed.TotalSeconds < 150 && selected.Values.All(choice => choice.Preferred is not null && choice.Preferred == choice.Selected))
        {
            controller.Advance(1);
        }
        bool battleBoundary = session.MapId == 628 && session.Battle is not null && controller.Failure is NotSupportedException && controller.Failure.Message == "FFVII attack has unsupported additional effect 0xFF (modifier 0xFF, formula 0x22) or status mask 0x00001000." && produced.Contains(134) && produced.Contains(82) && selected.Values.Any(choice => choice.Pc == 0x089D && choice.Selected == 1) && selected.Values.Any(choice => choice.Pc == 0x09B6 && choice.Selected == 0);
        Console.WriteLine($"Upper destination: map={session.MapId}, tick={session.Machine.Tick}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, landings={string.Join(',', produced.Order())}, choices={selected.Count}, restore={policy.State.PendingRestore is not null}, formation={session.Battle?.Capture().FormationId}, battleBoundary={battleBoundary}, active={policy.State.Active?.EventId}");
        bool choicesValid = selected.Values.All(choice => choice.Preferred is not null && choice.Preferred == choice.Selected);
        bool arrived = session.MapId == 629 && controller.Error is null && new[] { 134, 82, 48, 32, 21 }.All(produced.Contains) && departure?.GatewayIndex == 1 && departure.Transition.Map == 629;
        if ((!arrived && !battleBoundary) || !choicesValid || policy.State.PendingRestore is not null || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal upper destination continuation did not execute its native climbs, owned choices and selected exit unchanged.");
        }
    }
}
