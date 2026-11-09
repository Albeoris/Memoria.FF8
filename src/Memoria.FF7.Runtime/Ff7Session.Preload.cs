using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void RecordPreloadedMap(ushort map)
    {
        Process = Process with { PreloadedMap = map };
    }
}

public sealed partial class Ff7Session
{
    private ushort? preloadedMap;
    private Ff7FieldResources? preloadedField;
    private Ff7FieldModelResources[]? preloadedModels;

    private FieldHostReply Preload(FieldPreloadCommand command)
    {
        ushort? requested = command.Wait ? Host.Process.PreloadedMap : command.Map;
        if (requested is not ushort map)
        {
            return new(false);
        }
        if (map >= mapList.Count)
        {
            throw new InvalidDataException("FFVII preload map is outside the original maplist.");
        }
        if (preloadedMap != map)
        {
            Ff7FieldResources field = Ff7FieldResources.Load(fields, mapList[map]);
            Ff7FieldModelResources[] loaded = field.Models.Models.Select(model => Ff7FieldModelResources.Load(characters, model)).ToArray();
            preloadedField = field;
            preloadedModels = loaded;
            preloadedMap = map;
        }
        Host.RecordPreloadedMap(map);
        return new(true);
    }
}
