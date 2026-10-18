using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleScanVisualResources? nativeScanVisualResources;

    public NativeBattleScanVisualResources ReadNativeScanVisualResources()
    {
        return nativeScanVisualResources ??= NativeBattleScanVisualResourcesReader.Read(ReadNativeMemory());
    }
}
