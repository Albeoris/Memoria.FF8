using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldEntranceSource(int ChunkX, int ChunkZ, int Script, ushort Selector, ushort Destination)
{
    public string EventId => $"mesh:{ChunkX}:{ChunkZ}:script:{Script}";
}

public sealed partial class Ff7Session
{
    private readonly Dictionary<ushort, int[]> autoPlayFieldConnections = [];
    private readonly Dictionary<ushort, (string Identity, PcFieldGateway[] Gateways)> autoPlayGatewayDescriptors = [];
    private readonly Dictionary<int, Ff7AutoPlayWorldEntranceSource[]> autoPlayWorldEntrances = [];

    internal int[] ReadAutoPlayWorldConnections(int entrySelector)
    {
        return ReadAutoPlayWorldEntranceSources(Ff7WorldRuntime.ResolveWorldId(entrySelector)).Select(source => (int)source.Destination).Distinct().Order().ToArray();
    }

    internal Ff7AutoPlayWorldEntranceSource[] ReadAutoPlayWorldEntranceSources(int world)
    {
        if (autoPlayWorldEntrances.TryGetValue(world, out Ff7AutoPlayWorldEntranceSource[]? known))
        {
            return known;
        }
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation, world);
        PcWorldEvents events = resources.ReadEvents();
        PcWorldFieldTable table = resources.ReadFieldTable();
        WorldMachine inspection = new(new WorldProgram(events), Machine.Variables.CopyPersistent(), new AutoPlayDestinationWorldHost());
        HashSet<Ff7AutoPlayWorldEntranceSource> sources = [];
        Dictionary<ushort, WorldScriptAnalysis> entries = [];
        foreach (PcWorldFunction function in events.Functions.Where(function => function.Kind == 2 && function.Event <= 4))
        {
            if (!entries.TryGetValue(function.Offset, out WorldScriptAnalysis? analysis))
            {
                // Unknown source guards never authenticate current admission. Only
                // constant handoffs independent of foreign VM banks become advice.
                analysis = inspection.AnalyzeMeshDispatch(function.Mesh % 36, function.Mesh / 36, function.Event + 3, isolateTransitionDependencies: true);
                entries.Add(function.Offset, analysis);
            }
            foreach (WorldTransitionObservation transition in analysis.Transitions.Where(transition => transition.Operation == WorldOperation.FieldTransition && transition.Selector is not null))
            {
                ushort field = table.Select(transition.Selector!.Value).Field;
                if (IsKnownAutoPlayDestination(field))
                {
                    sources.Add(new(function.Mesh % 36, function.Mesh / 36, function.Event + 3, transition.Selector.Value, field));
                }
            }
        }
        Ff7AutoPlayWorldEntranceSource[] result = sources.OrderBy(source => source.ChunkZ).ThenBy(source => source.ChunkX).ThenBy(source => source.Script).ThenBy(source => source.Selector).ToArray();
        autoPlayWorldEntrances[world] = result;
        return result;
    }

    private sealed class AutoPlayDestinationWorldHost : IWorldHost
    {
        public WorldHostReply Execute(WorldHostRequest request)
        {
            throw new InvalidOperationException("Native destination advice cannot execute a world host operation.");
        }

        public int ReadSpecial(int selector, int activeModel)
        {
            throw new InvalidOperationException("Native destination advice cannot query a live world host.");
        }
    }

    internal bool MatchesAutoPlayGateway(ushort field, string visitId, string eventId, string condition, int destination)
    {
        string[] parts = eventId.Split(':');
        if (!IsKnownAutoPlayDestination(field) || parts.Length != 2 || parts[0] != "gateway" || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
        {
            return false;
        }
        if (!autoPlayGatewayDescriptors.TryGetValue(field, out var descriptor))
        {
            PcFieldFile file = PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find(mapList[field]).Single()));
            PcFieldScripts scripts = PcFieldScripts.Read(file.GetSection(PcFieldSectionKind.ScriptAndText).Span);
            PcFieldTriggers triggers = PcFieldTriggers.Read(file.GetSection(PcFieldSectionKind.TriggersAndGateways).Span);
            descriptor = (FieldProgram.FromPcScripts(scripts).Identity, triggers.Gateways.ToArray());
            autoPlayGatewayDescriptors[field] = descriptor;
        }
        if ((uint)index >= descriptor.Gateways.Length || visitId != $"ff7:{field}:{descriptor.Identity}" || descriptor.Gateways[index].DestinationField != destination)
        {
            return false;
        }
        string native = $"{descriptor.Identity}:gateway:{index}:{destination}:{descriptor.Gateways[index].DestinationTriangle}";
        return condition == native || condition == native + ":native-transfer:1";
    }

    internal int[] ReadAutoPlayFieldConnections(ushort field)
    {
        if (autoPlayFieldConnections.TryGetValue(field, out int[]? known))
        {
            return known;
        }
        if (!IsKnownAutoPlayDestination(field))
        {
            return [];
        }
        PcFieldFile file = PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find(mapList[field]).Single()));
        PcFieldScripts scripts = PcFieldScripts.Read(file.GetSection(PcFieldSectionKind.ScriptAndText).Span);
        PcFieldTriggers triggers = PcFieldTriggers.Read(file.GetSection(PcFieldSectionKind.TriggersAndGateways).Span);
        HashSet<int> destinations = triggers.Gateways.Select(gateway => (int)gateway.DestinationField).Where(destination => destination > 0 && destination < mapList.Count).ToHashSet();
        FieldProgram program = FieldProgram.FromPcScripts(scripts);
        autoPlayGatewayDescriptors[field] = (program.Identity, triggers.Gateways.ToArray());
        FieldMachine inspection = new(program);
        HashSet<ushort> entries = [];
        for (int entity = 0; entity < scripts.Entities.Count; entity++)
        {
            for (int script = 0; script < scripts.Entities[entity].ScriptOffsets.Count; script++)
            {
                if (!entries.Add(scripts.Entities[entity].ScriptOffsets[script]))
                {
                    continue;
                }
                // Ordinary analysis follows both conditional arms. No foreign map banks, actors or initialization are supplied as current facts.
                FieldScriptAnalysis analysis = inspection.AnalyzeScript(entity, script);
                if (analysis.Complete)
                {
                    destinations.UnionWith(analysis.DestinationFields.Where(destination => destination > 0 && destination < mapList.Count));
                }
            }
        }
        int[] result = destinations.Order().ToArray();
        autoPlayFieldConnections[field] = result;
        return result;
    }

    internal bool IsKnownAutoPlayDestination(ushort field)
    {
        return field > 64 && field < mapList.Count;
    }
}
