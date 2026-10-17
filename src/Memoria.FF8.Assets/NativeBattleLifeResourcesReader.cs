using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleLifeResourcesReader
{
    public static NativeBattleLifeResources Read(byte[] executable, ushort effect = 35)
    {
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(effect switch
        {
            35 => 0x8649C0,
            5 => 0x8D4450,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), "Native Life-family resources require effect5 or35.")
        });
        NativeBattleEffectHeapAllocation? animation = null;
        if (profile.RegistersAnimation)
        {
            const uint address = 0x15E69FC;
            ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, address, 8);
            ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
            if (length != 0x630 || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2 || address + length != 0x15E702C)
            {
                throw new InvalidDataException("Native Life animation must end at its original initializer filename.");
            }
            animation = new("FF8_EN:15E69FC", NativeBattleEffectStaticResourcesReader.Read(executable, address, length));
        }
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x15E638C, 0x15E6538, 0x15E66E4, 0x15E6890, 0x15E68EC }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, checked(address + profile.StaticDelta))).ToImmutableArray();
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0x15E6388u + profile.StaticDelta), 4).AsSpan());
        return new(animation, sprites, sound);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleLifeResources? nativeLifeResources;
    private NativeBattleLifeResources? nativePhoenixDownResources;

    public NativeBattleLifeResources ReadNativeLifeResources(ushort effect = 35)
    {
        return effect switch
        {
            35 => nativeLifeResources ??= NativeBattleLifeResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), effect),
            5 => nativePhoenixDownResources ??= NativeBattleLifeResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), effect),
            _ => throw new ArgumentOutOfRangeException(nameof(effect), "Native Life-family resources require effect5 or35.")
        };
    }
}
