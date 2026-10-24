using System.Collections.Immutable;
using System.Text;
using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect207ResourcesReader
{
    public static NativeBattleEffect207Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag206.tim\0")
        {
            throw new InvalidDataException("Effect207 initializer texture name changed.");
        }
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        NativeBattleGfAnimationSource programme = new(ProgrammeAddress, Bytes(ProgrammeAddress, ProgrammeLength));
        fronts.Add(ProgrammeAddress, programme.Data);
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        fronts.Add(SpriteAddress, Bytes(SpriteAddress, SpriteLength));
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> geometry = GeometryAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, GeometryLengths[index]))).ToImmutableDictionary(source => source.Address);
        NativeBattleEffect207Resources result = new(mainTim, new("effect207-camera", fronts[CameraAddress]), programme, geometry, new(0, SpriteAddress, 0, 0, fronts[SpriteAddress]), fronts[OriginalMeshAddress], fronts[SelectorsAddress], BinaryPrimitives.ReadUInt32LittleEndian(fronts[SoundAddress].AsSpan()), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect207Resources? nativeEffect207Resources;

    public NativeBattleEffect207Resources ReadNativeEffect207Resources()
    {
        if (nativeEffect207Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            nativeEffect207Resources = NativeBattleEffect207ResourcesReader.Read(ReadNativeExecutable(), new("mag206.tim", Read(magic, "mag206.tim").ToImmutableArray()));
        }
        return nativeEffect207Resources;
    }
}
