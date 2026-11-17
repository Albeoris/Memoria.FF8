using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    internal string ReadSpatialAdviceFingerprint()
    {
        Ff7Actor player = Host.Process.Actors.Single(actor => actor.PlayerControlled);
        byte[] facts = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Host.Process.Lines, Actors = Host.Process.Actors.Select(actor => new { actor.Entity, actor.EntityBound }), player.CollisionRadius, AdviceAvailable = IsSpatialAdviceHostAvailable(), CanScope = CanScopeNavigationControlEntries(Host.Process.Actors, Host.HasScriptedMotion) });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(facts));
    }

    private bool IsSpatialAdviceHostAvailable()
    {
        return World is null && Battle is null && !HasPendingTick && Host.Process.GlobalCommand == Ff7GlobalCommand.None && Host.Process.Menu is not { Completed: false };
    }

    internal FieldSpatialAdviceContext? ReadSpatialAdviceContext(IReadOnlyList<FieldSpatialSample>? samples = null)
    {
        if (!IsSpatialAdviceHostAvailable())
        {
            return null;
        }
        HashSet<FieldNavigationEntryPoint> entries = [];
        foreach (Ff7Actor actor in Host.Process.Actors.Where(actor => actor.EntityBound && actor.Entity >= 0))
        {
            entries.Add(new(actor.Entity, 2));
        }
        foreach (Ff7Line line in Host.Process.Lines)
        {
            for (int script = 2; script <= 6; script++)
            {
                entries.Add(new(line.Entity, script));
            }
            if (line.Talk)
            {
                entries.Add(new(line.Entity, 1));
            }
        }
        if (samples is null || !CanScopeNavigationControlEntries(Host.Process.Actors, Host.HasScriptedMotion))
        {
            return new(navigationExternalWrites, entries.ToArray());
        }
        Ff7Actor player = Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Dictionary<int, FieldNavigationEntryPoint[]> sampledEntries = samples.ToDictionary(sample => sample.Id, sample => ReadNavigationControlEntries(player with { X4096 = sample.Position.X << 12, Y4096 = sample.Position.Y << 12, Z4096 = sample.Position.Z << 12, Triangle = sample.Position.Triangle }, Host.Process.Actors, Host.Process.Lines, entries));
        return new(navigationExternalWrites, entries.ToArray()) { SampleEntries = sampledEntries, FixedPlayerEntity = player.Entity };
    }

    // Ordinary field publications outside the VM: clocks, party/materia flags,
    // encounter counters and the player-help toggle. These are clobbers, not
    // assumptions about which banks contain stable progress.
    private static readonly FieldVariableRead[] navigationExternalWrites = Enumerable.Range(16, 8).Select(address => new FieldVariableRead(FieldBank.Persistent0Byte, (ushort)address, false)).Concat(new FieldVariableRead[]
    {
        new(FieldBank.Persistent0Byte, 75, false),
        new(FieldBank.Persistent0Byte, 122, false),
        new(FieldBank.Persistent3Byte, 6, false),
        new(FieldBank.Persistent3Byte, 7, false),
        new(FieldBank.Persistent3Byte, 30, false)
    }).ToArray();

    internal FieldNavigationEntryProof ReadNavigationEntryProof(int entity, int script, FieldInputState input, bool talkContext = false)
    {
        Ff7FieldProcess process = Host.Process;
        if (World is not null || Battle is not null || HasPendingTick || Machine.CycleCursor is not null || !Machine.HasCompletedInitialCycle || process.GlobalCommand != Ff7GlobalCommand.None || process.PlayerLocked || process.Join is not null || process.Transition is not null || process.Dialogues.Any(dialogue => !dialogue.Completed) || process.Actors.Any(actor => actor.PlayerControlled && (actor.Ladder is not null || actor.Jump is not null || Host.HasScriptedMotion(actor))))
        {
            return new(false, false, "Read-only entry admission requires an ordinary controllable field boundary.", []);
        }
        HashSet<FieldNavigationEntryPoint> possibleEntries = [];
        foreach (Ff7Actor actor in process.Actors.Where(actor => actor.EntityBound && actor.Entity >= 0))
        {
            possibleEntries.Add(new(actor.Entity, 2));
        }
        foreach (Ff7Line line in process.Lines)
        {
            for (int candidate = 2; candidate <= 6; candidate++)
            {
                possibleEntries.Add(new(line.Entity, candidate));
            }
            if (line.Talk)
            {
                possibleEntries.Add(new(line.Entity, 1));
            }
        }
        if (talkContext)
        {
            Ff7Actor player = process.Actors.Single(actor => actor.PlayerControlled);
            possibleEntries.UnionWith(ReadNavigationTalkEntries(player, GetTalkTarget(), process.Lines));
        }
        FieldNavigationControlState? controls = null;
        if (CanScopeNavigationControlEntries(process.Actors, Host.HasScriptedMotion))
        {
            Ff7Actor player = process.Actors.Single(actor => actor.PlayerControlled);
            HashSet<FieldNavigationEntryPoint> controlEntries = ReadNavigationControlEntries(player, process.Actors, process.Lines, possibleEntries).ToHashSet();
            if (talkContext)
            {
                controlEntries.UnionWith(ReadNavigationTalkEntries(player, GetTalkTarget(), process.Lines));
            }
            controls = new(process.PlayerLocked, process.MenuDisabled, player.Entity, player.AnimationStatus, controlEntries.ToArray());
        }
        return Machine.ProveNavigationEntryHasNoEffect(entity, script, input, navigationExternalWrites, possibleEntries.ToArray(), controls: controls);
    }

    internal static bool CanScopeNavigationControlEntries(IReadOnlyList<Ff7Actor> actors, Func<Ff7Actor, bool> hasScriptedMotion)
    {
        return !actors.Any(actor => actor.Ladder is not null || actor.Jump is not null || hasScriptedMotion(actor));
    }

    internal static FieldNavigationEntryPoint[] ReadNavigationControlEntries(Ff7Actor player, IReadOnlyList<Ff7Actor> actors, IReadOnlyList<Ff7Line> lines, IReadOnlySet<FieldNavigationEntryPoint> possibleEntries)
    {
        HashSet<FieldNavigationEntryPoint> entries = possibleEntries.Where(entry => actors.Any(actor => actor.EntityBound && actor.Entity == entry.Entity)).ToHashSet();
        System.Numerics.Vector3 position = new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12);
        foreach (Ff7Line line in lines)
        {
            int distance = DistanceToSegment(new(line.X1, line.Y1, line.Z1), new(line.X2, line.Y2, line.Z2), position, out _);
            // Native tail contacts test the pre-move player position. Dispatch
            // also retains requests independently of the LINE enable flag.
            if (line.Touch || line.Talk || line.Push || line.Across || line.Enter || line.Leave || line.Enabled == 1 && distance >= 0 && distance < unchecked(player.CollisionRadius * player.CollisionRadius))
            {
                entries.UnionWith(possibleEntries.Where(entry => entry.Entity == line.Entity));
            }
        }
        return entries.ToArray();
    }

    internal static FieldNavigationEntryPoint[] ReadNavigationTalkEntries(Ff7Actor player, Ff7Actor? selectedActor, IReadOnlyList<Ff7Line> lines)
    {
        HashSet<FieldNavigationEntryPoint> entries = [];
        if (selectedActor is not null)
        {
            entries.Add(new(selectedActor.Entity, 1));
        }
        foreach (Ff7Line line in lines.Where(line => Ff7FieldHost.CanRequestLineTalk(line, player)))
        {
            entries.Add(new(line.Entity, 1));
        }
        return entries.ToArray();
    }

}
