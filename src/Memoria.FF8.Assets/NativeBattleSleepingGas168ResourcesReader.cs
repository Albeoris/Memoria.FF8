using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSleepingGas168ResourcesReader
{
    public static NativeBattleSleepingGas168Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> model = NativeBattleEffectStaticResourcesReader.Read(memory, 0xD6EDE4, 0x988);
        if (BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan()) != 0x3D8 || BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(4)) != 122 || BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(0x3E4)) != 60)
        {
            throw new InvalidDataException("Native Sleeping Gas requires its complete original122-vertex60-quad cloud source.");
        }
        byte[] axis = memory.Read(0xD6F788, 12);
        return new(model, NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD6BDE8), new("FF8_EN:D6BC70", NativeBattleEffectStaticResourcesReader.Read(memory, 0xD6BC70, 0x178)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xD6F77C, 4)), BinaryPrimitives.ReadInt32LittleEndian(axis), BinaryPrimitives.ReadInt32LittleEndian(axis.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(axis.AsSpan(8)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSleepingGas168Resources? nativeSleepingGas168Resources;
    public NativeBattleSleepingGas168Resources ReadNativeSleepingGas168Resources() => nativeSleepingGas168Resources ??= NativeBattleSleepingGas168ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
