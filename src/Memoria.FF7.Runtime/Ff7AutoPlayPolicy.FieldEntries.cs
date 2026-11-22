using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static (Ff7AutoPlayTarget? Target, bool Limited) FindFieldEntryDestinationAdvice(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, ushort destination, Ff7Actor player, byte[] blocked, Func<ushort, Ff7AutoPlayFieldTopology?> readTopology, Func<Ff7AutoPlayTarget, bool>? canSelect)
    {
        return FindFieldEntryDestinationAdvice(state, observation, destination, player, blocked, readTopology, canSelect, out _);
    }

    private static (Ff7AutoPlayTarget? Target, bool Limited) FindFieldEntryDestinationAdvice(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, ushort destination, Ff7Actor player, byte[] blocked, Func<ushort, Ff7AutoPlayFieldTopology?> readTopology, Func<Ff7AutoPlayTarget, bool>? canSelect, out int cost)
    {
        cost = int.MaxValue;
        int currentField = ReadVisitField(observation.VisitId);
        if (currentField <= 64 || currentField >= short.MaxValue || readTopology((ushort)currentField) is not { } origin)
        {
            return (null, true);
        }
        Queue<(FieldTransitionCommand Entry, Ff7AutoPlayTarget First, int Cost)> pending = new();
        HashSet<(ushort Field, ushort Triangle)> visited = [((ushort)currentField, checked((ushort)player.Triangle))];
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && (target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField) && CanRequestFieldTravel(state, observation, target) && canSelect?.Invoke(target) != false))
        {
            if (target.GatewayIndex is int index && (uint)index < origin.Gateways.Count)
            {
                pending.Enqueue((GatewayEntry(origin.Gateways[index]), target, 1));
            }
            else
            {
                foreach (Ff7AutoPlayLineTransition line in origin.Lines.Where(line => line.Line.Entity == target.Entity && line.Script == target.Script))
                {
                    pending.Enqueue((line.Transition, target, 1));
                }
            }
        }
        bool limited = false;
        while (pending.TryDequeue(out var step))
        {
            FieldTransitionCommand entry = step.Entry;
            if (entry.Map <= 64 || entry.Map >= short.MaxValue)
            {
                continue;
            }
            Ff7AutoPlayFieldTopology? topology = readTopology(entry.Map);
            if (topology is null)
            {
                limited = true;
                continue;
            }
            if ((uint)entry.Triangle >= topology.Walkmesh.Count || !Ff7AutoplayDriver.Contains(topology.Walkmesh[entry.Triangle], new(entry.X, entry.Y, 0), out float height))
            {
                continue;
            }
            Ff7Actor actor = player with { X4096 = entry.X << 12, Y4096 = entry.Y << 12, Z4096 = (int)(height * 4096), Triangle = entry.Triangle };
            byte[] mask = entry.Map == currentField ? blocked : new byte[(topology.Walkmesh.Count + 7) / 8];
            if (actor.Triangle >> 3 >= mask.Length || (mask[actor.Triangle >> 3] & 1 << (actor.Triangle & 7)) != 0)
            {
                continue;
            }
            if (!visited.Add((entry.Map, entry.Triangle)))
            {
                continue;
            }
            if (entry.Map == destination)
            {
                cost = step.Cost;
                return (step.First, false);
            }
            foreach (PcFieldGateway gateway in topology.Gateways.Where(gateway => gateway.DestinationField > 64 && gateway.DestinationField < short.MaxValue && Ff7AutoplayDriver.HasGatewayTopologyPath(topology.Walkmesh, mask, actor, gateway, out _)))
            {
                pending.Enqueue((GatewayEntry(gateway), step.First, step.Cost + 1));
            }
            foreach (Ff7AutoPlayLineTransition line in topology.Lines.Where(line => line.Line.Enabled == 1 && Ff7AutoplayDriver.DiscoverLineContacts(topology.Walkmesh, mask, actor, line.Line).Count != 0))
            {
                pending.Enqueue((line.Transition, step.First, step.Cost + 1));
            }
        }
        return (null, limited);
    }

    private static FieldTransitionCommand GatewayEntry(PcFieldGateway gateway)
    {
        return new(unchecked((ushort)gateway.DestinationField), gateway.DestinationX, gateway.DestinationY, gateway.DestinationTriangle, unchecked((byte)gateway.Direction));
    }
}
