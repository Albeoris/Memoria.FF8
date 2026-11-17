using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private readonly Dictionary<ushort, int[]> autoPlayFieldConnections = [];

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
        FieldMachine inspection = new(FieldProgram.FromPcScripts(scripts));
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
