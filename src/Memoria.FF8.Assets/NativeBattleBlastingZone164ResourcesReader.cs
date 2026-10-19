using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlastingZone164ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadTextureNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new short[] { 538, 539 }.Select(id =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCCu + (uint)id * 4, 4));
            byte[] name = memory.Read(pointer, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException("Native Blasting Zone texture filename is not terminated.");
            }
            return new NativeBattleEffectFileAsset(id, Encoding.ASCII.GetString(name.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleBlastingZone164Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleBlastingZone164ColdRegion> cold = new (uint Address, int Length)[] { (0x23C6950, 8), (0x23C6978, 0x2580), (0x23C8F18, 0x28), (0x23C9D60, 0x3BC) }.Select(range => new NativeBattleBlastingZone164ColdRegion(range.Address, memory.ReadLoader(range.Address, range.Length).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD9961C, 0xD99748, 0xD999AC }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD8A048, 0xD8B180, 0xD8B610, 0xD8B880, 0xD8C130, 0xD8D918, 0xD8EAA8, 0xD8F8B8, 0xD909F0, 0xD90F00, 0xD91AD0, 0xD92154, 0xD9339C, 0xD948C4, 0xD94FAC, 0xD95904, 0xD95B7C, 0xD95DEC, 0xD97D6C }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        NativeBattleBlastingZone164Resources result = new(new("FF8_EN:D88AA0", memory.Read(NativeBattleBlastingZone164Resources.StaticAddress, NativeBattleBlastingZone164Resources.StaticLength).ToImmutableArray()), cold, files, sprites, meshes);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlastingZone164Resources? nativeBlastingZone164Resources;

    public NativeBattleBlastingZone164Resources ReadNativeBlastingZone164Resources()
    {
        if (nativeBlastingZone164Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            ImmutableArray<NativeBattleEffectFileAsset> names = NativeBattleBlastingZone164ResourcesReader.ReadTextureNames(executable);
            using IFlArchive battle = OpenArchive("battle");
            nativeBlastingZone164Resources = NativeBattleBlastingZone164ResourcesReader.Read(executable, names.Select(name => name with { Data = Read(battle, name.Name).ToImmutableArray() }).ToImmutableArray());
        }
        return nativeBlastingZone164Resources;
    }
}
