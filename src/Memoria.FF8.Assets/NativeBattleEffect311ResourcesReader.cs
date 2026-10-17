using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect311ResourcesReader
{
    public static NativeBattleEffect311Resources Read(byte[] executable)
    {
        var bank = NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleEffect311Resources.Address, NativeBattleEffect311Resources.Length);
        var animation = new NativeBattleEffectHeapAllocation("FF8_EN:1020D20", NativeBattleEffectStaticResourcesReader.Read(executable, 0x1020D20, 0x324));
        uint wrapper = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0xC7EFB4, 4).AsSpan());
        uint callback = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0xC7EFA0, 4).AsSpan());
        NativeBattleModuleResource sprite = new(0, 0x101AF28, wrapper, callback, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x101AF28));
        return new(bank, animation, sprite, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x1021044, 4).AsSpan()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect311Resources? nativeEffect311Resources;
    public NativeBattleEffect311Resources ReadNativeEffect311Resources()
    {
        return nativeEffect311Resources ??= NativeBattleEffect311ResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
