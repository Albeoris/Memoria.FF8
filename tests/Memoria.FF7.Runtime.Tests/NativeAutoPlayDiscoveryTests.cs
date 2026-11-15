using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayDiscoveryTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DISCOVERY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native generic discovery: configure original resources and a standalone field fixture.");
            return;
        }
        using FileStream stream = File.OpenRead(path);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("A field discovery fixture requires an FFVII session.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayDiscovery discovery = new(session);
        Ff7AutoPlayObservation observed = discovery.Observe();
        if (!session.Machine.HasCompletedInitialCycle)
        {
            Check(session.Machine.Initialized && !observed.CanAcceptInput, "Completed initialization exposed a frontier before the first normal native callback.");
        }
        Check(before == JsonSerializer.Serialize(session.Capture()), "Discovery changed the native VM, process, inputs, progress or RNG.");
        Check(JsonSerializer.Serialize(observed) == JsonSerializer.Serialize(discovery.Observe()), "Read-only discovery was not deterministic.");
        Check(observed.Events.Select(target => target.Id).Distinct(StringComparer.Ordinal).Count() == observed.Events.Length, "Native targets have duplicate identities.");
        Check(observed.Events.All(target => discovery.FindTarget(target.Id) == target), "A discovered target could not be resolved by stable identity.");
        foreach (Ff7AutoPlayTarget target in observed.Events)
        {
            if (target.Kind == Ff7AutoPlayTargetKind.Gateway)
            {
                Check(!session.Host.Process.MapJumpDisabled && target.GatewayIndex is int index && session.Field.Triggers.Gateways[index].DestinationField == target.DestinationField, "A gateway ignored native admission or lost its directory index.");
            }
            else
            {
                int entry = session.Field.Scripts.Entities[target.Entity].ScriptOffsets[target.Script];
                Check(session.Field.Scripts.Data.Span[entry] != 0, "An immediate-return script was advertised as an interaction.");
                if (target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave)
                {
                    Check(target.SegmentStart is not null && target.SegmentEnd is not null && session.Host.Process.Lines.Any(line => line.Entity == target.Entity && line.Enabled == 1), "A LINE target lost its geometry or enabled owner.");
                }
            }
        }
        if (!session.Machine.HasCompletedInitialCycle)
        {
            byte[] source = File.ReadAllBytes(path);
            using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A pending initial native cycle cannot create a choice save."));
            Ff7AutoPlayActiveEvent? active = policy.State.Active;
            Ff7AutoplayInput input = policy.ChooseInput(new(session));
            Check(input.Horizontal == 0 && input.Vertical == 0 && input.Held == 0 && !input.Talk && policy.State.Active == active, "Planning ran before the first native main/control callback.");
            session.SetInput(0, 0);
            session.MovePlayer(0, 0, true);
            session.AdvanceTick(advanceClock: false);
            Check(session.Machine.HasCompletedInitialCycle, "A complete ordinary native callback did not advance the derived readiness phase.");
            Ff7AutoPlayObservation after = discovery.Observe();
            Check(!session.Host.Process.PlayerLocked || !after.CanAcceptInput, "A main-owned native control lock was ignored after the initial cycle.");
            Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The detached initial-cycle readiness check changed its standalone source.");
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
