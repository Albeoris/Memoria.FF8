using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect303ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { CompletionFileId });

    public static NativeBattleEffect303Resources Read(byte[] executable, NativeBattleEffectFileAsset completionFile)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag302.tim\0")
        {
            throw new InvalidDataException("Effect303 requires its selected initializer texture identity.");
        }
        NativeBattleEffectFileAsset expected = ReadFileNames(executable).Single();
        if (completionFile is null || completionFile.FileId != expected.FileId || !string.Equals(completionFile.Name, expected.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Effect303 completion file disagrees with its executable directory.");
        }
        var vibration = Bytes(VibrationAddress, VibrationLength);
        NativeBattleEffect303Resources resources = new(Source(MainProgrammeAddress, MainProgrammeLength), Source(ChildProgrammeAddress, ChildProgrammeLength), MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableArray(), new(0, ParticleSpriteAddress, 0, 0, Bytes(ParticleSpriteAddress, ParticleSpriteLength)), new(0, SpriteAddress, 0, 0, Bytes(SpriteAddress, SpriteLength)), new("FF8_EN:10340AC", Bytes(CameraAddress, CameraLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), new(VibrationAddress, new(memory.SourceHash, [BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan().Slice(4))], vibration)), completionFile, Bytes(ChildPointsAddress, 32), Bytes(MuzzlePointsAddress, 16));
        resources.Validate();
        return resources;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, Bytes(address, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect303Resources? nativeEffect303Resources;

    public NativeBattleEffect303Resources ReadNativeEffect303Resources()
    {
        if (nativeEffect303Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            NativeBattleEffectFileAsset file = NativeBattleEffect303ResourcesReader.ReadFileNames(executable).Single();
            nativeEffect303Resources = NativeBattleEffect303ResourcesReader.Read(executable, file with { Data = Read(battle, file.Name).ToImmutableArray() });
        }
        return nativeEffect303Resources;
    }
}
