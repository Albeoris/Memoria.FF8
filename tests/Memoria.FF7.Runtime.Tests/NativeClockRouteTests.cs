using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeClockRouteTests
{
    internal static void Run()
    {
        for (int first = 0; first < 12; first++)
        {
            for (int second = 0; second < 12; second++)
            {
                Check(Ff7AutoPlayScriptedInput.IsClockStoryBridge(first, second) == (first == 6 && second == 10 || first == 10 && second == 6), "The clock bridge criterion accepted another room pair or depended on which native hand owns the endpoint.");
            }
        }
        Console.WriteLine("PASS native clock bridge endpoint pair criterion");
    }

    internal static void RunOriginal()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CLOCK_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            Console.WriteLine("SKIP native clock route: configure original resources and standalone fixture.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        ushort field = session.MapId;
        Ff7AutoPlayScriptedInput scripted = new(session);
        List<Ff7AutoplayInput> chosenInputs = [];
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            string before = Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(session.Capture(), string.Empty);
            Ff7AutoplayInput selected = scripted.ChooseInput() ?? default;
            Check(before == Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(session.Capture(), string.Empty), "Predicting a clock input mutated actual native VM, progress, process, clock or RNG before applying the chosen physical input.");
            chosenInputs.Add(selected);
            return selected;
        }
        Ff7AutoPlayController controller = new(session, Choose);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (session.Host.Process.PlayerLocked && controller.Running && timer.Elapsed < TimeSpan.FromSeconds(60))
        {
            controller.Advance(1);
            Check(session.MapId == field && session.Battle is null && session.World is null, "Clock control left its native room before its owned script returned.");
        }
        Check(controller.Error is null && !session.Host.Process.PlayerLocked, $"Clock native control did not unlock: {controller.Error}; callbacks={controller.Steps}");
        int first = session.Machine.Variables.ReadWord(FieldBank.Persistent1Word, 0xE2);
        int second = session.Machine.Variables.ReadWord(FieldBank.Persistent1Word, 0xE4);
        Check(Ff7AutoPlayScriptedInput.IsClockStoryBridge(first, second), "Native stop/coast and proceed did not preserve the required bridge endpoints.");
        var gateway = session.Field.Triggers.Gateways.Single(gateway => gateway.DestinationField == 610);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(Ff7AutoplayDriver.HasGatewayTopologyPath(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, gateway, out _), "The actual unlocked clock floor does not connect its current player to the required native gateway.");
        Ff7SessionSave after = session.Capture();
        using Ff7Session replay = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        int cursor = 0;
        Ff7AutoplayInput ReplayInput(Ff7AutoplayDriver driver)
        {
            Check(cursor < chosenInputs.Count, "Independent native replay requested an input absent from the authenticated clock continuation.");
            return chosenInputs[cursor++];
        }
        Ff7AutoPlayController replayController = new(replay, ReplayInput);
        replayController.Start();
        for (long callback = 0; callback < controller.Steps; callback++)
        {
            Check(replayController.Running, $"Independent native clock replay stopped early: {replayController.Error}");
            replayController.Advance(1);
        }
        Ff7SessionSave replayed = replay.Capture();
        Check(replayController.Error is null && cursor == chosenInputs.Count && !replay.Host.Process.PlayerLocked && Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(after, string.Empty) == Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(replayed, string.Empty), "Independent cold native replay differed in VM, native requests, player/process, progress, clock or exact field RNG.");
        Check(after.Progress.FieldRandomIndex == replayed.Progress.FieldRandomIndex, "The clock planner consumed or reset live RNG outside the reproduced native callback sequence.");
        JsonElement beforeProgress = JsonSerializer.SerializeToElement(source.Progress with { Clock = after.Progress.Clock, FieldRandomIndex = after.Progress.FieldRandomIndex });
        JsonElement afterProgress = JsonSerializer.SerializeToElement(after.Progress);
        string changed = string.Join(", ", beforeProgress.EnumerateObject().Where(property => property.Value.GetRawText() != afterProgress.GetProperty(property.Name).GetRawText()).Select(property => property.Name));
        Check(changed.Length == 0, $"Clock continuation changed progress fields: {changed}.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Clock planning changed its authoritative source file.");
        Console.WriteLine($"PASS native clock controls, settled bridge {first}/{second}, unlock and required gateway topology; callbacks={controller.Steps}; choices={chosenInputs.Count}; exact cold replay; elapsed={timer.Elapsed.TotalSeconds:F3}s; source unchanged");
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_CLOCK_CHECK_DEPARTURE") == "1")
        {
            using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The required clock departure must not create optional choice checkpoints."));
            // Match the existing explicit App Start contract: rearm only the diagnostic observation window.
            policy.State.FrontierProgressKey = null;
            policy.State.FrontierIdleSteps = 0;
            policy.SetDestinationField(610);
            Ff7AutoPlayController travel = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
            travel.Start();
            Stopwatch departure = Stopwatch.StartNew();
            while (session.MapId == field && travel.Running && session.Battle is null && departure.Elapsed < TimeSpan.FromSeconds(60))
            {
                travel.Advance(1);
            }
            Check(session.MapId == 610 && travel.Error is null, $"The public clock departure did not reach the required room: field={session.MapId}, status={policy.Status}, error={travel.Error}, callbacks={travel.Steps}.");
            Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The public clock departure changed its authoritative source file.");
            Console.WriteLine($"PASS public ordinary clock departure to native field {session.MapId}; callbacks={travel.Steps}; elapsed={departure.Elapsed.TotalSeconds:F3}s; source unchanged");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
