using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken160ResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 160);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xDAA718, 0xDAA9E8, 0xDAA8C4 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xDAACB8, 0xDAB6A0, 0xDAB970, 0xEA0FD0 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xDAC20Cu + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 4).Select(index => unchecked((int)U32(0xDAC280u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:EA1168", NativeBattleEffectStaticResourcesReader.Read(memory, 0xEA1168, 0xA38)), actor, sprites, colors, lead, meshes, U32(0xEA0FCC), U32(0xB8B7D8)) { ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xDAC270u + 4u * (uint)index))).ToImmutableArray(), StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xEA1158u + 2u * (uint)index)).ToImmutableArray(), StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xEA1148u + 2u * (uint)index)).ToImmutableArray(), StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4))) };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken160Resources? nativeRenzokuken160Resources;
    public NativeBattleRenzokuken160Resources ReadNativeRenzokuken160Resources() => nativeRenzokuken160Resources ??= NativeBattleRenzokuken160ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
