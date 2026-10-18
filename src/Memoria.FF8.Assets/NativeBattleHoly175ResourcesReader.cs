using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleHoly175ResourcesReader
{
    public static NativeBattleHoly175Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint[] spriteAddresses = [0xD522CC, 0xD52374, 0xD523E8, 0xD524C4];
        int[] spriteLengths = [0xA8, 0x74, 0xDC, 0x334];
        int[] spriteFrames = [6, 4, 8, 31];
        ImmutableArray<NativeBattleHoly175SpriteResource>.Builder sprites = ImmutableArray.CreateBuilder<NativeBattleHoly175SpriteResource>();
        for (int index = 0; index < spriteAddresses.Length; index++)
        {
            ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, spriteAddresses[index]);
            if (bytes.Length != spriteLengths[index] || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(8)) != spriteFrames[index])
            {
                throw new InvalidDataException("Native Holy projected sprite leaves its original complete frame resource.");
            }
            sprites.Add(new(spriteAddresses[index], bytes));
        }
        uint[] meshAddresses = [0xD527F8, 0xD52DD8, 0xD53688, 0xD53BA0, 0xD54588, 0xD56C58];
        int[] meshLengths = [0x5E0, 0x8B0, 0x518, 0x9E8, 0x26D0, 0x2528];
        ImmutableArray<NativeBattleStaticMeshResource>.Builder meshes = ImmutableArray.CreateBuilder<NativeBattleStaticMeshResource>();
        for (int index = 0; index < meshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, meshAddresses[index]);
            if (mesh.Bytes.Length != meshLengths[index])
            {
                throw new InvalidDataException("Native Holy mesh leaves its original complete resource extent.");
            }
            meshes.Add(mesh);
        }
        return new(Allocation(0xD45188, 0xD24), Allocation(0xD45EAC, 0xC420), sprites.ToImmutable(), meshes.ToImmutable(), Allocation(0xD59180, 4)) { WindowColors = Allocation(0xD59210, 64), DelayTable = Allocation(0xD59188, 48), RayRows = Allocation(0xD591D0, 64), CaptureDirectory = Allocation(0xD59250, 32), CaptureBlackPackets = Allocation(0xD592C0, 48) };

        NativeBattleEffectHeapAllocation Allocation(uint address, int length) => new($"FF8_EN:{address:X}", NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleHoly175Resources? nativeHoly175Resources;
    public NativeBattleHoly175Resources ReadNativeHoly175Resources() => nativeHoly175Resources ??= NativeBattleHoly175ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
