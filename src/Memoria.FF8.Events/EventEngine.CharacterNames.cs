using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static int NativeNamingSubject(int argument) => argument switch
    {
        0 or 1 or 2 => argument,
        20 => GameNames.GrieverSubject,
        _ when TryNativeGuardianForceName(argument, out _, out int id) => GameNames.GuardianForceSubject(id),
        _ => throw new NotSupportedException("Native naming argument has no supported persistent name subject.")
    };

    private string NativeCharacterName(int entity)
    {
        string original = entity switch
        {
            0 => "Squall",
            1 => "Rinoa",
            2 => "Angelo",
            GameNames.GrieverSubject => GameNames.DefaultName(GameNames.GrieverSubject),
            >= 16 and <= 31 => GameNames.DefaultName(entity),
            _ => throw new NotSupportedException("Native name prompt has no supported persistent subject.")
        };
        return Scene.Names.GetValueOrDefault(entity, original);
    }
}
