using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSandStorm176ResourcesReader
{
    public static NativeBattleSandStorm176Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] matrix = memory.Read(0xD45158, 32);
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(matrix.AsSpan(index * 2))).ToImmutableArray();
        NativeBattleActionMatrix funnel = new(rotation, BinaryPrimitives.ReadInt32LittleEndian(matrix.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(matrix.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(matrix.AsSpan(28)), 4096);
        return new(new("FF8_EN:D44EB0", NativeBattleEffectStaticResourcesReader.Read(memory, 0xD44EB0, 0xF0)), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD44FA0), funnel, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD4514C, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD45150, 4)));
    }
}
public sealed partial class GameAssets
{
    private NativeBattleSandStorm176Resources? nativeSandStorm176Resources;

    public NativeBattleSandStorm176Resources ReadNativeSandStorm176Resources()
    {
        return nativeSandStorm176Resources ??= NativeBattleSandStorm176ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
