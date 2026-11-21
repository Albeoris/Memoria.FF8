using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGlacierMapOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GLACIER_MAP_SAVE") ?? throw new InvalidOperationException("Select a native map-acquisition fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var original = policy.State.Active!;
        var choice = policy.State.Choice!;
        var coverage = policy.State.Coverage.Choices.Single(item => item.Key == choice.Key);
        string primary = coverage.ContinuationCheckpoint!;
        byte[] primaryHash = SHA256.HashData(File.ReadAllBytes(primary));
        bool childReturned = false;
        session.NativeTickCompleted += tick => childReturned |= tick.ScriptReturns.Any(owner => owner.Entity == 4 && owner.Priority == 6);
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        for (int poll = 0; poll < 4 && policy.State.Choice is not null && controller.Error is null; poll++)
        {
            controller.Advance(1);
        }
        bool consumed = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId).Conditions?.HasExecutedEntry(original.ConditionFingerprint) == true;
        Console.WriteLine($"Map acquisition completion: consumed={consumed}, options={string.Join(',', coverage.CompletedOptions)}, choice={policy.State.Choice?.Key}, restore={policy.State.PendingRestore is not null}, childReturned={childReturned}, error={controller.Error}");
        if (!consumed || !coverage.CompletedOptions.Contains(0) || !coverage.CompletedOptions.Contains(1) || coverage.ContinuationCheckpoint != primary || policy.State.Choice is not null || policy.State.PendingRestore is not null || childReturned || session.Machine.Capture().Entities[4].Priority != 6 || session.Machine.Capture().Entities[4].ProgramCounter != 0x04DE || controller.Error is not null)
        {
            throw new InvalidOperationException("The actual acquired map did not finish its owned choice while preserving its cosmetic child and historical primary.");
        }
        policy.SetDestinationField(654);
        Stopwatch timer = Stopwatch.StartNew();
        for (int poll = 0; poll < 10000 && timer.Elapsed.TotalSeconds < 20 && session.MapId == 656 && controller.Error is null; poll++)
        {
            controller.Advance(1);
        }
        bool map = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x45) & 0x10) != 0;
        bool permission = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x82) & 0x40) != 0;
        Console.WriteLine($"Map acquisition departure: field={session.MapId}, tick={session.Machine.Tick}, callbacks={controller.Steps}, map={map}, permission={permission}, error={controller.Error}");
        if (session.MapId != 654 || !map || !permission || controller.Error is not null || !primaryHash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(primary))) || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native departure lost actual map acquisition or changed the original checkpoints.");
        }
    }
}
