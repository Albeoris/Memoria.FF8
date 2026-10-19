using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot193ResourcesReader
{
    public static NativeBattleShot193Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xD12EC0;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Dark Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD14430, 0xD1450C, 0xD145E4, 0xD14658, 0xD149B0 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD14AA8, 0xD14D18, 0xD15108 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xD155C8u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xD155B8, 12);
        return new(new("FF8_EN:D12EC0", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xD155B0, 4), 0));
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot193Resources? nativeShot193Resources;
    public NativeBattleShot193Resources ReadNativeShot193Resources() => nativeShot193Resources ??= NativeBattleShot193ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
