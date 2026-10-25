using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect308ResourcesReader
{
    public static string ReadFileName(byte[] executable, int fileId)
    {
        if (fileId is not 360 and not 361)
        {
            throw new NotSupportedException("Effect308 file selector has no admitted original input.");
        }
        NativeExecutableMemory memory = new(executable);
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCC + (uint)(fileId * 4), 4));
        byte[] bytes = memory.Read(address, 16);
        int end = bytes.AsSpan().IndexOf((byte)0);
        return end > 0 ? Encoding.ASCII.GetString(bytes.AsSpan(0, end)) : throw new InvalidDataException("Effect308 file input has no bounded original name.");
    }

    public static NativeBattleEffect308Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != TextureName + "\0")
        {
            throw new InvalidDataException("Effect308 initializer texture name changed.");
        }
        foreach (NativeBattleEffectFileAsset file in files)
        {
            if (file.Name != ReadFileName(executable, file.FileId))
            {
                throw new InvalidDataException("Effect308 file input disagrees with its original directory.");
            }
        }
        Dictionary<uint, ImmutableArray<byte>> fronts = StaticRanges.ToDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        ImmutableDictionary<uint, ImmutableArray<byte>> scripts = ScriptRanges.ToImmutableDictionary(range => range.Address, range => Bytes(range.Address, range.Length));
        foreach ((uint address, ImmutableArray<byte> bytes) in scripts)
        {
            fronts.Add(address, bytes);
        }
        ImmutableDictionary<int, NativeBattleGfAnimationSource> programmes = Enumerable.Range(0, 4).ToImmutableDictionary(selector => selector, selector => new NativeBattleGfAnimationSource(ProgrammeAddresses[selector], Bytes(ProgrammeAddresses[selector], ProgrammeLengths[selector])));
        foreach (NativeBattleGfAnimationSource programme in programmes.Values)
        {
            fronts.Add(programme.Address, programme.Data);
        }
        fronts.Add(CameraAddress, Bytes(CameraAddress, CameraLength));
        fronts.Add(VibrationAddress, Bytes(VibrationAddress, VibrationLength));
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> geometry = GeometryAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, GeometryLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleModuleResource> sprites = SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableDictionary(source => source.Address);
        foreach (NativeBattleModuleResource sprite in sprites.Values)
        {
            fronts.Add(sprite.Address, sprite.Bytes);
        }
        NativeBattleModuleResource smoke = new(0, 0x1026038, 0, 0, Bytes(0x1026038, 328));
        fronts.Add(smoke.Address, smoke.Bytes);
        ImmutableDictionary<uint, NativeBattleModuleResource> projectedQuads = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(smoke.Address, smoke).Add(0x1025D20, sprites[0x1025D20]);
        ImmutableDictionary<byte, uint> spritePointers = SpriteSelectors.Select((selector, index) => (Selector: selector, Address: SpriteAddresses[index])).ToImmutableDictionary(row => row.Selector, row => row.Address);
        NativeBattleEffect308Resources result = new(mainTim, new("effect308-camera", fronts[CameraAddress]), new("effect308-vibration", fronts[VibrationAddress]), scripts, programmes, Enumerable.Range(0, 4).ToImmutableDictionary(selector => selector, selector => ProgrammeWorkspaceLengths[selector]), geometry, sprites, spritePointers, projectedQuads, BinaryPrimitives.ReadUInt32LittleEndian(fronts[SoundAddress].AsSpan()), files.ToImmutableDictionary(file => (int)file.FileId), fronts.ToImmutableDictionary());
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect308Resources? nativeEffect308Resources;

    public NativeBattleEffect308Resources ReadNativeEffect308Resources()
    {
        if (nativeEffect308Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            using IFlArchive magic = OpenArchive("magic");
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = new[] { 360, 361 }.Select(fileId =>
            {
                string name = NativeBattleEffect308ResourcesReader.ReadFileName(executable, fileId);
                return new NativeBattleEffectFileAsset(checked((short)fileId), name, Read(battle, name).ToImmutableArray());
            }).ToImmutableArray();
            nativeEffect308Resources = NativeBattleEffect308ResourcesReader.Read(executable, new(TextureName, Read(magic, TextureName).ToImmutableArray()), files);
        }
        return nativeEffect308Resources;
    }
}
