using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleProtectResourcesReader
{
    public static NativeBattleProtectResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource shield = new(0x15F3B24, NativeBattleEffectStaticResourcesReader.Read(memory, 0x15F3B24, 0x4754));
        _ = new NativeBattleGfAnimationWorkspace(new byte[0x470].ToImmutableArray()).Initialize7016B0(shield, 0x470);
        ImmutableArray<byte> flash = NativeBattleEffectStaticResourcesReader.Read(memory, 0x15F3B14, 12);
        return new(new("FF8_EN:15F8278", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15F8278, 0x9EC)), shield, NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15F8C64), NativeBattleEffectStaticResourcesReader.Read(memory, 0x15F8D94, 32), Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(flash.AsSpan().Slice(index * 4))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15F3B20, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleProtectResources? nativeProtectResources;

    public NativeBattleProtectResources ReadNativeProtectResources()
    {
        return nativeProtectResources ??= NativeBattleProtectResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
