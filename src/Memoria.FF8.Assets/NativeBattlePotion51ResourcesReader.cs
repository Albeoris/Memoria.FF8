using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePotion51ResourcesReader
{
    public static NativeBattleCuraResources Read(byte[] executable)
    {
        return NativeBattleCuraResourcesReader.Read(executable, 51);
    }

    public static (short ContextF4, int SceneFade) ReadColdState(IGameNativeMemorySource memory)
    {
        NativeBattleCuraFamilyProfile profile = NativeBattleCuraFamilyProfile.ForEffect(51);
        return (BinaryPrimitives.ReadInt16LittleEndian(memory.ReadLoader(profile.ContextF4Address, 2)), BinaryPrimitives.ReadInt32LittleEndian(memory.ReadLoader(profile.SceneFadeAddress, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCuraResources? nativePotion51Resources;

    public NativeBattleCuraResources ReadNativePotion51Resources()
    {
        return nativePotion51Resources ??= NativeBattlePotion51ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
