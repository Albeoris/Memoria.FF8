using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private NativeWorldPathPosition? type2CactuarSeed;

    public NativeWorldPathPosition ReadFootType2CactuarLoadPosition()
    {
        return type2CactuarSeed ??= NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
    }
}
