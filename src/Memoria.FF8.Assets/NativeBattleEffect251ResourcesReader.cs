using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect251ResourcesReader
{
    public static string ReadFileName(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + StreamFileId * 4, 4));
        byte[] name = memory.Read(pointer, StreamFileName.Length + 1);
        if (name[^1] != 0 || Encoding.ASCII.GetString(name.AsSpan(0, name.Length - 1)).ToLowerInvariant() != StreamFileName)
        {
            throw new InvalidDataException("Effect251 streamed input requires its original file directory name.");
        }
        return Encoding.ASCII.GetString(name.AsSpan(0, name.Length - 1));
    }

    public static NativeBattleEffect251Resources Read(byte[] executable, NativeBattleEffectFileAsset file)
    {
        if (file.FileId != StreamFileId || !string.Equals(file.Name, ReadFileName(executable), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Effect251 streamed input disagrees with its original file directory.");
        }
        NativeExecutableMemory memory = new(executable);
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = ProgrammeAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ProgrammeLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleGfAnimationSource programme in programmes.Values)
        {
            fronts.Add(programme.Address, programme.Data);
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> geometry = GeometryAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, GeometryLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleModuleResource sprite in sprites.Values)
        {
            fronts.Add(sprite.Address, sprite.Bytes);
        }
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        NativeBattleEffect251Resources result = new(new("effect251-embedded-tim", Bytes(EmbeddedTimAddress, EmbeddedTimLength)), new("effect251-camera", fronts[CameraAddress]), programmes, geometry, sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty.Add(StreamFileId, file with { Name = StreamFileName }), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect251Resources? nativeEffect251Resources;

    public NativeBattleEffect251Resources ReadNativeEffect251Resources()
    {
        if (nativeEffect251Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive battle = OpenArchive("battle");
            string name = NativeBattleEffect251ResourcesReader.ReadFileName(executable);
            NativeBattleEffectFileAsset file = new(StreamFileId, name, Read(battle, name).ToImmutableArray());
            nativeEffect251Resources = NativeBattleEffect251ResourcesReader.Read(executable, file);
        }
        return nativeEffect251Resources;
    }
}
