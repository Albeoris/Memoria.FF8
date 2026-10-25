using System.Collections.Immutable;
using System.Text;
using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect320ResourcesReader
{
    public static NativeBattleEffect320Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != TextureName + "\0")
        {
            throw new InvalidDataException("Effect320 initializer texture name changed.");
        }
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = ProgrammeAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ProgrammeLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleGfAnimationSource source in programmes.Values)
        {
            fronts.Add(source.Address, source.Data);
        }
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        fronts.Add(SpriteAddress, Bytes(SpriteAddress, SpriteLength));
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> geometry = GeometryAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, GeometryLengths[index]))).ToImmutableDictionary(source => source.Address);
        NativeBattleEffect320Resources result = new(mainTim, new("effect320-camera", fronts[CameraAddress]), programmes, geometry, new(0, SpriteAddress, 0, 0, fronts[SpriteAddress]), BinaryPrimitives.ReadUInt32LittleEndian(fronts[SoundAddress].AsSpan()), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect320Resources? nativeEffect320Resources;

    public NativeBattleEffect320Resources ReadNativeEffect320Resources()
    {
        if (nativeEffect320Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect320Resources = NativeBattleEffect320ResourcesReader.Read(ReadNativeExecutable(), new(TextureName, Read(magic, TextureName).ToImmutableArray()));
        }
        return nativeEffect320Resources;
    }
}
