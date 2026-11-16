using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
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

    internal FieldNavigationEntryProof ReadNavigationEntryProof(int entity, int script, FieldInputState input)
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
        return Machine.ProveNavigationEntryHasNoEffect(entity, script, input, navigationExternalWrites, possibleEntries.ToArray());
    }
}
