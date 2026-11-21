using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAltarContinuationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_ALTAR_SAVE") ?? throw new InvalidOperationException("Select a native altar continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var observation = new Ff7AutoPlayDiscovery(session).Observe();
        bool admitted = false;
        bool returned = false;
        var selected = observation.Events.Single(target => target.Entity == 6 && target.Script == 3);
        session.NativeTickCompleted += tick => returned |= tick.ScriptReturns.Any(value => value.Entity == 6 && value.Priority == 1);
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission is { Script: 3, Accepted: true, Started: true })
            {
                admitted |= admission.Entity == 6;
                Console.WriteLine($"Native altar crossing admission: entity{admission.Entity}, tick{session.Machine.Tick}");
            }
        };
        foreach (var target in observation.Events)
        {
            Console.WriteLine($"Altar event: {target.Id}, complete={target.AnalysisComplete}, changes={target.ChangesStory}/{target.MayChangeNavigation}/{target.ChangesField}, contact={new Ff7AutoplayDriver(session).HasNativeGeometricContact(target)}, reason={target.SupportReason}");
        }
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool consumed = false;
        for (int polls = 0; polls < 100000 && controller.Steps < 1000 && timer.Elapsed.TotalSeconds < 30 && controller.Error is null && !consumed; polls++)
        {
            controller.Advance(1);
            consumed = returned && policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == selected.Id).Conditions?.HasExecutedEntry(selected.ConditionFingerprint) == true;
        }
        Console.WriteLine($"Altar continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}, status={policy.State.Status}, error={controller.Error}");
        Console.WriteLine($"Altar completion: admitted={admitted}, returned={returned}, consumed={consumed}, story={session.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0)}");
        if (!admitted || !returned || !consumed || session.Machine.Variables.ReadWord(Memoria.FF7.Events.FieldBank.Persistent0Word, 0) != 669 || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal altar crossing did not complete its actual scene and preserve the original fixture.");
        }
    }
}
