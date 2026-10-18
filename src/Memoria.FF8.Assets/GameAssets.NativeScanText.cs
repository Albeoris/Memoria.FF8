using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleScanTextResources? nativeScanTextResources;

    public NativeBattleScanTextResources ReadNativeScanTextResources()
    {
        if (nativeScanTextResources is not null)
        {
            return nativeScanTextResources;
        }
        return nativeScanTextResources = NativeBattleScanTextResourcesReader.Read(ReadNativeMemory());
    }
}
