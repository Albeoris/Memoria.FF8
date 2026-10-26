using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMadRush273ResourcesReader
{
    public static NativeBattleMadRush273Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x110E778, NativeBattleEffectStaticResourcesReader.Read(memory, 0x110E778, 10580)), new(0, 0x110E5DC, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x110E5DC)), new("FF8_EN:11110D0", NativeBattleEffectStaticResourcesReader.Read(memory, 0x11110D0, 336)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x11110CC, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMadRush273Resources? nativeMadRush273Resources;

    public NativeBattleMadRush273Resources ReadNativeMadRush273Resources()
    {
        return nativeMadRush273Resources ??= NativeBattleMadRush273ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
