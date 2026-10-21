using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePotion50ResourcesReader
{
    public static NativeBattleCureResources Read(byte[] executable)
    {
        return NativeBattleCureResourcesReader.Read(executable, 50);
    }

    public static short ReadColdContextF4(IGameNativeMemorySource memory)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(memory.ReadLoader(NativeBattleCureFamilyProfile.ForEffect(50).ContextF4Address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCureResources? nativePotion50Resources;

    public NativeBattleCureResources ReadNativePotion50Resources()
    {
        return nativePotion50Resources ??= NativeBattlePotion50ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
