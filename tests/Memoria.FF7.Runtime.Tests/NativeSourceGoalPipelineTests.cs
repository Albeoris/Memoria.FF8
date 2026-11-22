using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalPipelineTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SOURCE_PIPELINE_SAVE")!;
        ushort destination = ushort.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_SOURCE_PIPELINE_DESTINATION")!);
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        FieldMachine prior = session.Machine;
        int entry = session.Field.Scripts.Entities[4].ScriptOffsets[2];
        byte[] code = new byte[Math.Max(entry + 11, 4096)];
        new byte[] { 0x60, (byte)destination, (byte)(destination >> 8), 0, 0, 0, 0, 0, 0, 0, 0 }.CopyTo(code, entry);
        ushort[][] entries = Enumerable.Range(0, session.Field.Scripts.Entities.Count).Select(_ => new ushort[32]).ToArray();
        entries[4][2] = checked((ushort)entry);
        FieldMachine authored = new(new FieldProgram(code, entries), prior.Variables, host: session.Host);
        authored.Initialize();
        authored.ScriptReturned = prior.ScriptReturned;
        authored.ScriptDispatched = prior.ScriptDispatched;
        authored.InstructionExecuted = prior.InstructionExecuted;
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.Machine))!.SetValue(session, authored);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The authored MAP fixture cannot require a choice anchor."));
        long generation = session.FieldGeneration;
        Ff7AutoPlayActiveEvent active = new("authored-source", "line:0:entity:4:script:2", "authored-condition", generation, 4, 2, Ff7AutoPlayTargetKind.LinePush, null, 0, "before", Accepted: true, NavigationKey: "authored-nav", EntryGuardFingerprints: new Dictionary<string, string> { ["guard"] = "current" });
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, authored.ProgramIdentity, entry, authored.Tick) };
        policy.State.Active = active;
        policy.State.SourceGoal = new(active.VisitId, authored.ProgramIdentity, generation, active.NavigationKey!, active.EventId, active.ConditionFingerprint, active.Kind, 4, 2, entry, AcceptedEntry: entry);
        session.AutoPlayState = policy.State.Clone();
        Ff7NativeTickObservation? departure = null;
        session.NativeTickCompleted += observation =>
        {
            if (observation.Generation == generation && observation.Transition is not null)
            {
                departure = observation;
            }
        };
        Check(authored.RequestExternalScript(4, 2).Started, "The authored native MAP source did not start.");
        session.AdvanceTick();
        Check(session.MapId == destination && session.FieldGeneration == generation + 1 && departure?.SourceActivation?.ExecutionProvenance?.AcceptedTransition is not null, "The real Session pipeline failed to capture and load the authored native MAP.");
        Check(policy.State.Active is { TransitionOwned: true, ExecutionProvenance.AcceptedTransition: not null } tracked && ReferenceEquals(tracked.EntryGuardFingerprints, active.EntryGuardFingerprints), "The published JSON clone lost the MAP receipt or replaced live planning collections.");
        FieldHostRequest receipt = policy.State.Active!.ExecutionProvenance!.AcceptedTransition!;
        for (int tick = 0; tick < 2; tick++)
        {
            session.AdvanceTick();
            Check(policy.State.Active?.ExecutionProvenance?.AcceptedTransition == receipt, "Delayed destination initialization erased the accepted source transition.");
        }
        Check(SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The installed-resource fixture was changed.");
        Console.WriteLine($"Real native MAP pipeline retained cloned source authority across load and two destination callbacks: map={session.MapId}, tick={session.Machine.Tick}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
