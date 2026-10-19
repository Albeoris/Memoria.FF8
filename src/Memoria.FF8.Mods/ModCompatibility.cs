using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public static class ModCompatibility
{
    public static bool IsCompatible(GameDataEdition edition, ModFormat format) => format switch
    {
        ModFormat.Demaster => true,
        ModFormat.Tonberry => edition == GameDataEdition.Classic,
        ModFormat.Junction => edition == GameDataEdition.Classic,
        ModFormat.Memoria => edition == GameDataEdition.Classic,
        _ => false
    };
}
