using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private readonly Dictionary<ushort, int[]> autoPlayFieldConnections = [];
    private readonly Dictionary<ushort, (string Identity, PcFieldGateway[] Gateways)> autoPlayGatewayDescriptors = [];

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
        HashSet<int> destinations = triggers.Gateways.Select(gateway => (int)gateway.DestinationField).Where(destination => destination > 64 && destination < mapList.Count).ToHashSet();
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
                    destinations.UnionWith(analysis.DestinationFields.Where(destination => destination > 64 && destination < mapList.Count));
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
