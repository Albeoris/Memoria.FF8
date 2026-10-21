using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePotion52ResourcesReader
{
    public static NativeBattleCuragaResources Read(byte[] executable)
    {
        return NativeBattleCuragaResourcesReader.Read(executable, 52);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCuragaResources? nativePotion52Resources;

    public NativeBattleCuragaResources ReadNativePotion52Resources()
    {
        return nativePotion52Resources ??= NativeBattlePotion52ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
