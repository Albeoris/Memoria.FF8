using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect3ResourcesReader
{
    public static NativeBattleEffect3Resources Read(byte[] executable)
    {
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, 0x14FA790, 12);
        int clutLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan().Slice(8));
        int imageLength = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0x14FA790u + 8u + (uint)clutLength), 4).AsSpan());
        int length = checked(8 + clutLength + imageLength);
        if (BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan()) != 16 || clutLength < 12 || imageLength < 12)
        {
            throw new InvalidDataException("Native effect3 embedded texture lacks its original indexed TIM blocks.");
        }
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x1522FB0, 0x1523204, 0x1523458, 0x1523578, 0x1523684, 0x15237A4, 0x1523928, 0x1523DE0 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address)).ToImmutableArray();
        return new(new("FF8_EN:14FA790", NativeBattleEffectStaticResourcesReader.Read(executable, 0x14FA790, length)), new("FF8_EN:1523F40", NativeBattleEffectStaticResourcesReader.Read(executable, 0x1523F40, 0x590)), sprites, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x15244D0, 4).AsSpan()), BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x15244D4, 4).AsSpan()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect3Resources? nativeEffect3Resources;

    public NativeBattleEffect3Resources ReadNativeEffect3Resources()
    {
        return nativeEffect3Resources ??= NativeBattleEffect3ResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
