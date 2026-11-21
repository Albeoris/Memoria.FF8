using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class FieldConnectivityOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_CONNECTIVITY_SAVE") ?? throw new InvalidOperationException("Select a native ladder destination fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The native ladder route must not explore optional choices."));
        ushort destination = policy.State.DestinationField ?? throw new InvalidOperationException("The source must retain its actual destination intent.");
        int player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Entity;
        bool admitted = false;
        bool ladderCompleted = false;
        int? ladderTriangle = null;
        bool returned = false;
        session.NativeInteractionScriptRequested += request => admitted |= request is { Entity: 12, Script: 4, Accepted: true, Started: true };
        session.NativeTickCompleted += observation =>
        {
            returned |= observation.ScriptReturns.Any(owner => owner.Entity == 12 && owner.Priority == 1);
        };
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 2000 && timer.Elapsed.TotalSeconds < 45 && controller.Error is null && session.MapId != destination; polls++)
        {
            controller.Advance(1);
            if (session.MapId != destination)
            {
                var actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                if (actor.Ladder is { Mode: 1 } ladder)
                {
                    ladderTriangle = ladder.TargetTriangle;
                }
                ladderCompleted |= ladderTriangle is int target && actor.Triangle == target && actor.Ladder is not { Completed: false };
            }
        }
        Console.WriteLine($"Original ladder destination: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Host.Process.Tick}, destination={destination}, admitted={admitted}, ladderCompleted={ladderCompleted}, returned={returned}, harp={(session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) & 8) != 0}, active={policy.State.Active?.EventId}, status={policy.State.Status}, error={controller.Error}");
        if (session.MapId != destination || !admitted || !ladderCompleted || !returned || controller.Error is not null || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) & 8) == 0 || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The actual native ladder prerequisite did not establish its retained field destination.");
        }
    }
}
