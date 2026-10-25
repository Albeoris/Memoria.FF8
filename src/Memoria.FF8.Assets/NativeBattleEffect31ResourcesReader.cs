using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect31ResourcesReader
{
    public static NativeBattleEffect31Resources Read(byte[] executable, NativeBattleEffectHeapAllocation mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, InitializerTextureName.Length + 1)) != InitializerTextureName + "\0")
        {
            throw new InvalidDataException("Effect31 initializer texture name changed.");
        }
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = ProgrammeAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ProgrammeLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, ProgrammeBytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        Dictionary<uint, ImmutableArray<byte>> fronts = new();
        foreach ((uint address, int length) in StaticRanges)
        {
            fronts.Add(address, Bytes(address, length));
        }
        foreach (NativeBattleGfAnimationSource source in programmes.Values)
        {
            fronts.Add(source.Address, source.Data);
        }
        foreach (NativeBattleModuleResource sprite in sprites.Values)
        {
            fronts.Add(sprite.Address, sprite.Bytes);
        }
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        NativeBattleEffect31Resources result = new(mainTim, new("effect31-camera", fronts[CameraAddress]), programmes, meshes, sprites, fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();

        ImmutableArray<byte> ProgrammeBytes(uint address, int length)
        {
            foreach (NativeBattleGfAnimationSource source in programmes.Values)
            {
                if (address >= source.Address && (ulong)address + (uint)length <= (ulong)source.Address + (uint)source.Data.Length)
                {
                    return source.Data.AsSpan(checked((int)(address - source.Address)), length).ToArray().ToImmutableArray();
                }
            }
            throw new InvalidDataException("Effect31 geometry leaves its programme owner.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect31Resources? nativeEffect31Resources;

    public NativeBattleEffect31Resources ReadNativeEffect31Resources()
    {
        if (nativeEffect31Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect31Resources = NativeBattleEffect31ResourcesReader.Read(ReadNativeExecutable(), new(InitializerTextureName, Read(magic, InitializerTextureName).ToImmutableArray()));
        }
        return nativeEffect31Resources;
    }
}
