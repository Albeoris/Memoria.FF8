using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCureResourcesReader
{
    public static NativeBattleCureResources Read(byte[] executable)
    {
        const uint animationAddress = 0x16417C8;
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, 8);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
        if (length != 0x1641F78 - animationAddress || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2)
        {
            throw new InvalidDataException("Native8D6A00 animation directory does not end at its original first sprite descriptor.");
        }
        NativeBattleEffectHeapAllocation animation = new("FF8_EN:16417C8", NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, length));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x1641F78, 0x1641FCC, 0x164205C, 0x1642238, 0x1642504 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address)).ToImmutableArray();
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x16417C4, 4).AsSpan());
        return new(animation, sprites, sound);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCureResources? nativeCureResources;

    public NativeBattleCureResources ReadNativeCureResources()
    {
        return nativeCureResources ??= NativeBattleCureResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
