using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private const string NativeKeymanProgram = "E81D4EC5D4FE755D7E214DCDD225C4D86061A7DDDEC5A0827E7DCB2C48968945";
    private static readonly int[] KeymanLogicalDoors = [4, 5, 3, 0, 2, 7, 1, 6, 8];
    private static readonly int[] KeymanPhysicalExits = [3, 6, 8, 1, 5, 2, 7, 4, 0];
    private long keymanPlanGeneration = -1;
    private long keymanPlanTick = -1;
    private string? keymanPreferredEvent;

    private bool IsKeymanPreferredEntry(AutoPlayVisit visit, AutoPlayEvent target)
    {
        if (session.World is not null || session.MapId != 610 || session.Machine.ProgramIdentity != NativeKeymanProgram || ordinaryObservation is not { } observation || observation.VisitId != visit.Key || observation.ProgramIdentity != NativeKeymanProgram || State.DestinationField is not (null or 611 or 612))
        {
            return false;
        }
        if (keymanPlanGeneration != session.FieldGeneration || keymanPlanTick != session.Machine.Tick)
        {
            keymanPlanGeneration = session.FieldGeneration;
            keymanPlanTick = session.Machine.Tick;
            keymanPreferredEvent = null;
            bool caught = (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xE9) & 8) != 0;
            int npc = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x12);
            Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
            if ((caught || npc is >= 0 and < 9) && player is not null)
            {
                int available = 0;
                bool progressionAvailable = false;
                foreach (Ff7Line line in session.Host.Process.Lines.Where(line => line.Slot is >= 0 and <= 9 && line.Entity == line.Slot + 6))
                {
                    if (Ff7AutoplayDriver.DiscoverLineContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, line).Count > 0)
                    {
                        if (line.Slot == 9)
                        {
                            progressionAvailable = true;
                        }
                        else
                        {
                            available |= 1 << line.Slot;
                        }
                    }
                }
                int[] plan = caught ? ReadKeymanExitPlan(available, progressionAvailable) : ReadKeymanDoorPlan(npc, available);
                if (plan.Length > 0)
                {
                    int entry = plan[0];
                    keymanPreferredEvent = $"line:{entry}:entity:{entry + 6}:script:{(entry == 9 ? 2 : 5)}";
                }
            }
        }
        return target.Id == keymanPreferredEvent;
    }

    internal static int[] ReadKeymanExitPlan(int availableEntries, bool progressionAvailable)
    {
        if ((availableEntries & ~0x1FF) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableEntries));
        }
        if (progressionAvailable)
        {
            return [9];
        }
        Queue<(int Available, int[] Entries)> queue = new();
        HashSet<int> visited = [];
        queue.Enqueue((availableEntries, []));
        visited.Add(availableEntries);
        while (queue.TryDequeue(out var state))
        {
            for (int entry = 0; entry < 9; entry++)
            {
                if ((state.Available & (1 << entry)) == 0)
                {
                    continue;
                }
                int[] route = [.. state.Entries, entry];
                int exit = KeymanPhysicalExits[entry];
                if (exit == 0)
                {
                    return [.. route, 9];
                }
                int nextAvailable = exit <= 4 ? 0x1E : 0x1E0;
                if (visited.Add(nextAvailable))
                {
                    queue.Enqueue((nextAvailable, route));
                }
            }
        }
        return [];
    }

    internal static int[] ReadKeymanDoorPlan(int npcDoor, int availableEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(npcDoor);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(npcDoor, 9);
        if ((availableEntries & ~0x1FF) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableEntries));
        }
        Queue<(int Available, int Npc, int[] Entries)> queue = new();
        HashSet<(int Available, int Npc)> visited = [];
        queue.Enqueue((availableEntries, npcDoor, []));
        visited.Add((availableEntries, npcDoor));
        while (queue.TryDequeue(out var state))
        {
            for (int entry = 0; entry < 9; entry++)
            {
                if ((state.Available & (1 << entry)) == 0)
                {
                    continue;
                }
                int[] route = [.. state.Entries, entry];
                if (KeymanLogicalDoors[entry] == state.Npc)
                {
                    return route;
                }
                int exit = KeymanPhysicalExits[entry];
                int nextAvailable = exit == 0 ? 1 : exit <= 4 ? 0x1E : 0x1E0;
                int nextNpc = (state.Npc + 1) % 9;
                if (visited.Add((nextAvailable, nextNpc)))
                {
                    queue.Enqueue((nextAvailable, nextNpc, route));
                }
            }
        }
        return [];
    }
}
