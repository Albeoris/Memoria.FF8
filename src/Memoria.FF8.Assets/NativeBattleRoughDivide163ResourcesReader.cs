using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRoughDivide163ResourcesReader
{
    public static NativeBattleRoughDivide163Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleRoughDivide163ColdRegion> cold = new (uint Address, int Length)[] { (0x23CA120, 0x88), (0x23CA1D8, 0x2EE0), (0x23CD0D8, 0x270), (0x23CE168, 0x420) }.Select(range => new NativeBattleRoughDivide163ColdRegion(range.Address, memory.ReadLoader(range.Address, range.Length).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD9B2AC, 0xD9B458 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD9B5D0, 0xD9CB38, 0xD9CE04, 0xD9D314, 0xD9D594, 0xD9DD34, 0xD9E1DC, 0xD9E2D4, 0xD9EC8C, 0xD9FE54 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        NativeBattleRoughDivide163Resources result = new(new("FF8_EN:D99DC8", memory.Read(NativeBattleRoughDivide163Resources.StaticAddress, NativeBattleRoughDivide163Resources.StaticLength).ToImmutableArray()), cold, sprites, meshes);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRoughDivide163Resources? nativeRoughDivide163Resources;

    public NativeBattleRoughDivide163Resources ReadNativeRoughDivide163Resources()
    {
        return nativeRoughDivide163Resources ??= NativeBattleRoughDivide163ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
