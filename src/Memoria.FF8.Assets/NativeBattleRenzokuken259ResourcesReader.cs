using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken259ResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 259);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xCEDF40, 0xCEE210, 0xCEE0EC }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xCEE4C8, 0xCEEEB0, 0xCEF130, 0xE91524 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xCF066Cu + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 6).Select(index => unchecked((int)U32(0xCF06E0u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:E916BC", NativeBattleEffectStaticResourcesReader.Read(memory, 0xE916BC, 0xF90)), actor, sprites, colors, lead, meshes, U32(0xE91520), U32(0xB8B7D8)) { ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xCF06D0u + 4u * (uint)index))).ToImmutableArray(), StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE916ACu + 2u * (uint)index)).ToImmutableArray(), StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE9169Cu + 2u * (uint)index)).ToImmutableArray(), StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4))) };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken160Resources? nativeRenzokuken259Resources;
    public NativeBattleRenzokuken160Resources ReadNativeRenzokuken259Resources() => nativeRenzokuken259Resources ??= NativeBattleRenzokuken259ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
