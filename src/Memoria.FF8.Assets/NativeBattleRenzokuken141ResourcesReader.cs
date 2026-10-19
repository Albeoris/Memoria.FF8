using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken141ResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 141);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xDDF4A0, 0xDDF770, 0xDDF64C }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xDDFA28, 0xDE0410, 0xDE0690, 0xEA7E78 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xDE1684u + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 5).Select(index => unchecked((int)U32(0xDE16F8u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:EA8010", NativeBattleEffectStaticResourcesReader.Read(memory, 0xEA8010, 0xAA4)), actor, sprites, colors, lead, meshes, U32(0xEA7E74), U32(0xB8B7D8)) { ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xDE16E8u + 4u * (uint)index))).ToImmutableArray(), StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xEA8000u + 2u * (uint)index)).ToImmutableArray(), StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xEA7FF0u + 2u * (uint)index)).ToImmutableArray(), StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4))) };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken160Resources? nativeRenzokuken141Resources;
    public NativeBattleRenzokuken160Resources ReadNativeRenzokuken141Resources() => nativeRenzokuken141Resources ??= NativeBattleRenzokuken141ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
