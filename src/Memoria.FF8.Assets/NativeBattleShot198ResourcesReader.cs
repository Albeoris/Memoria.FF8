using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot198ResourcesReader
{
    public static NativeBattleShot198Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xCF3A78;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Hyper Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xCF5E44, 0xCF6054, 0xCF6200 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xCF6394, 0xCF8D2C, 0xCF9834, 0xCFA52C, 0xCFAE64 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xCFB328u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xCFB308, 12);
        byte[] secondaryAxis = memory.Read(0xCFB318, 12);
        return new(new("FF8_EN:CF3A78", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8))), new(unchecked((short)(int)U32(secondaryAxis, 0)), unchecked((short)(int)U32(secondaryAxis, 4)), unchecked((short)(int)U32(secondaryAxis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xCFB2FC, 4), 0));
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot198Resources? nativeShot198Resources;
    public NativeBattleShot198Resources ReadNativeShot198Resources() => nativeShot198Resources ??= NativeBattleShot198ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
