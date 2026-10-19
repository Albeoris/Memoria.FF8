using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot188ResourcesReader
{
    public static NativeBattleShot188Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xD1B4B8;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Normal Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD1CA28, 0xD1CB00, 0xD1CB74, 0xD1CF44 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD1D0F0, 0xD1D638 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xD1DA40u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xD1DA30, 12);
        return new(new("FF8_EN:D1B4B8", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xD1DA28, 4), 0));
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot188Resources? nativeShot188Resources;
    public NativeBattleShot188Resources ReadNativeShot188Resources() => nativeShot188Resources ??= NativeBattleShot188ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
