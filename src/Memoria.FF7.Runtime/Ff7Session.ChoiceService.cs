using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    internal FieldChoiceServiceProof ReadChoiceServiceProof(int entity, int programCounter, bool pendingSelection = false)
    {
        HashSet<FieldNavigationEntryPoint> entries = [];
        foreach (Ff7Actor actor in Host.Process.Actors.Where(actor => actor.EntityBound))
        {
            entries.Add(new(actor.Entity, 2));
            if (!actor.PlayerControlled && actor.TalkEnabled)
            {
                entries.Add(new(actor.Entity, 1));
            }
        }
        foreach (Ff7Line line in Host.Process.Lines)
        {
            for (int script = 1; script <= 6; script++)
            {
                entries.Add(new(line.Entity, script));
            }
        }
        return Machine.ProveChoiceService(entity, programCounter, entries.ToArray(), pendingSelection: pendingSelection);
    }
}
