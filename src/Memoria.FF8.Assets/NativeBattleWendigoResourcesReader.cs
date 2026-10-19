using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWendigoResourcesReader
{
    public static NativeBattleWendigoResources Read(byte[] executable, ushort effect)
    {
        NativeExecutableMemory memory = new(executable);
        if (effect == 41)
        {
            return new(41, 0x15CA5A8, 0x15CD8B0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x15CA5A8, 0x340C), new("FF8_EN:15CD9B4", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15CD9B4, 0x2CC)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15CA5B0), Sound(0x15CA5A8), Sound(0x15CA5AC)) { Radii = NativeBattleEffectStaticResourcesReader.Read(memory, 0x15CDC80, 57 * 6), ConstantChannels = memory.ReadLoader(0x2698AB8, 0x16A8).ToImmutableArray() };
        }
        if (effect == 42)
        {
            return new(42, 0x15C62B8, 0x15C9DE0, NativeBattleEffectStaticResourcesReader.Read(memory, 0x15C62B8, 0x3C2C), new("FF8_EN:15C9EE4", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15C9EE4, 0x234)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15C62C0), Sound(0x15C62B8), Sound(0x15C62BC))
            {
                KickScript = NativeBattleEffectStaticResourcesReader.Read(memory, 0x15CA118, 77 * 14),
                PassScript = NativeBattleEffectStaticResourcesReader.Read(memory, 0x15CA550, 7 * 10),
                ConstantChannels = memory.ReadLoader(0x267B024, 0x834).ToImmutableArray(),
                SmallBurst = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15C65E4),
                LargeBurst = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15C646C),
                BallPrim = new(0x15C67B8, NativeBattleEffectStaticResourcesReader.Read(memory, 0x15C67B8, 0x1890)),
                KickPrim = new(0x15C8048, NativeBattleEffectStaticResourcesReader.Read(memory, 0x15C8048, 0x1D98))
            };
        }
        throw new ArgumentOutOfRangeException(nameof(effect));

        uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }
}
public sealed partial class GameAssets
{
    private NativeBattleWendigoResources? nativeDribble41Resources;
    private NativeBattleWendigoResources? nativeShoot42Resources;
    public NativeBattleWendigoResources ReadNativeDribble41Resources() => nativeDribble41Resources ??= NativeBattleWendigoResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), 41);
    public NativeBattleWendigoResources ReadNativeShoot42Resources() => nativeShoot42Resources ??= NativeBattleWendigoResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), 42);
}
