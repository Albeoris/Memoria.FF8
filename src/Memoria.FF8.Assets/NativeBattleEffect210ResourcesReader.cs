using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect210ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new[] { 704, 360, 361, 362 }.Select(id =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + checked((uint)id * 4), 4));
            byte[] name = memory.Read(pointer, 128);
            int end = name.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Effect210 file input has no original directory name.");
            }
            return new NativeBattleEffectFileAsset(checked((short)id), Encoding.ASCII.GetString(name.AsSpan(0, end)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleEffect210Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length || files.Where((file, index) => file.FileId != names[index].FileId || file.Name != names[index].Name).Any())
        {
            throw new InvalidDataException("Effect210 streamed inputs disagree with their original directory.");
        }
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        fronts.Add(TimAddress, Bytes(TimAddress, TimLength));
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = ActAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ActLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleGfAnimationSource source in programmes.Values)
        {
            fronts.Add(source.Address, source.Data);
        }
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> geometry = GeometryAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, GeometryLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleStaticMeshResource source in meshes.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleModuleResource source in sprites.Values)
        {
            fronts.Add(source.Address, source.Bytes);
        }
        NativeBattleEffect210Resources result = new(new("effect210-tim", fronts[TimAddress]), new("effect210-camera", fronts[CameraAddress]), programmes, geometry, meshes, sprites, files.ToImmutableDictionary(file => (int)file.FileId), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect210Resources? nativeEffect210Resources;

    public NativeBattleEffect210Resources ReadNativeEffect210Resources()
    {
        if (nativeEffect210Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffect210ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeEffect210Resources = NativeBattleEffect210ResourcesReader.Read(executable, files);
        }
        return nativeEffect210Resources;
    }
}
