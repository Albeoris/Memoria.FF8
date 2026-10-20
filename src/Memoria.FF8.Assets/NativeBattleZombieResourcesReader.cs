using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleZombieResourcesReader
{
    public static NativeBattleZombieResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource layout = new(0x12BA30C, NativeBattleEffectStaticResourcesReader.Read(memory, 0x12BA30C, 0x35CC));
        _ = new NativeBattleGfAnimationWorkspace(new byte[0x6F4].ToImmutableArray()).Initialize7016B0(layout, 0x6F4);
        byte[] wave = memory.Read(0x12BE238, 8192);
        byte[] zero = memory.ReadLoader(0x2521860, 8);
        return new(new("FF8_EN:12BD8D8", NativeBattleEffectStaticResourcesReader.Read(memory, 0x12BD8D8, 0x95C)), layout, Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(index * 2))).ToImmutableArray(), new(BinaryPrimitives.ReadInt16LittleEndian(zero), BinaryPrimitives.ReadInt16LittleEndian(zero.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(zero.AsSpan(4))), BinaryPrimitives.ReadUInt32LittleEndian(memory.ReadLoader(0x2521868, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x12BE234, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleZombieResources? nativeZombieResources;

    public NativeBattleZombieResources ReadNativeZombieResources()
    {
        return nativeZombieResources ??= NativeBattleZombieResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
