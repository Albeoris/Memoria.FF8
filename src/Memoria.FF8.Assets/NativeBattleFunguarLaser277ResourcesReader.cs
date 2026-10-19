using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleFunguarLaser277ResourcesReader
{
    public static NativeBattleFunguarLaser277Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
        NativeBattleModuleResource Sprite(uint address)
        {
            NativeBattleModuleResource resource = new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
            if (BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8)) != 16)
            {
                throw new InvalidDataException("Native Funguar Laser requires its original sixteen-frame projected sprite.");
            }
            for (int frame = 0; frame < 16; frame++)
            {
                _ = resource.ProjectedFrame571C80(frame);
            }
            return resource;
        }
        return new(Source(0x10CE318, 0x2BBC), Source(0x10D0ED4, 0x1260), Source(0x10CD0AC, 0x126C), Source(0x10D2134, 0xB98), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10CB9E4u).Bytes, Sprite(0x10CB838), Sprite(0x10CB628), new("FF8_EN:10D2CCC", NativeBattleEffectStaticResourcesReader.Read(memory, 0x10D2CCC, 0x1D0)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10D2E9C, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleFunguarLaser277Resources? nativeFunguarLaser277Resources;
    public NativeBattleFunguarLaser277Resources ReadNativeFunguarLaser277Resources() => nativeFunguarLaser277Resources ??= NativeBattleFunguarLaser277ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
