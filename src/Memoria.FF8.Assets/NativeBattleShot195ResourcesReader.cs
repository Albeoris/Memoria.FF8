using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot195ResourcesReader
{
    public static NativeBattleShot195Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xD07400;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Canister Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD097CC, 0xD099F0, 0xD09AB4, 0xD09C60, 0xD09E0C }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD09FB8, 0xD0A978, 0xD0AD68, 0xD0AFE0, 0xD0B488 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xD0BD58u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xD0BD38, 12);
        byte[] secondaryAxis = memory.Read(0xD0BD48, 12);
        return new(new("FF8_EN:D07400", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8))), new(unchecked((short)(int)U32(secondaryAxis, 0)), unchecked((short)(int)U32(secondaryAxis, 4)), unchecked((short)(int)U32(secondaryAxis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xD0BD30, 4), 0));
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot195Resources? nativeShot195Resources;
    public NativeBattleShot195Resources ReadNativeShot195Resources() => nativeShot195Resources ??= NativeBattleShot195ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
