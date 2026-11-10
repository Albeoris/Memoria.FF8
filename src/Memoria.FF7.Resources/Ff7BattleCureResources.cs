using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7BattleCureResources
{
    public static PcTexture Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return PcTexture.Read(File.ReadAllBytes(Path.Combine(installation.DataDirectory, "battle", "seffect1.tex")));
    }
}
