using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect298ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { CompletionFileId });

    public static NativeBattleEffect298Resources Read(byte[] executable, NativeBattleEffectFileAsset completionFile)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag297.tim\0")
        {
            throw new InvalidDataException("Effect298 requires its selected initializer texture identity.");
        }
        NativeBattleEffectFileAsset expected = ReadFileNames(executable).Single();
        if (completionFile is null || completionFile.FileId != expected.FileId || !string.Equals(completionFile.Name, expected.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Effect298 completion file disagrees with its executable directory.");
        }
        var vibration = Bytes(VibrationAddress, VibrationLength);
        NativeBattleEffect298Resources resources = new(Source(ProgrammeAAddress, ProgrammeALength), Source(ProgrammeBAddress, ProgrammeBLength), Source(ProgrammeCAddress, ProgrammeCLength), Source(ProgrammeDAddress, ProgrammeDLength), MeshAddresses.Select(address => new NativeBattleStaticMeshResource(0, address, Bytes(address, 248))).ToImmutableArray(), SpriteAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, SpriteLengths[index]))).ToImmutableArray(), new("FF8_EN:1052298", Bytes(CameraAddress, CameraLength)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), new(VibrationAddress, new(memory.SourceHash, [BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan().Slice(4))], vibration)), completionFile, Bytes(ScriptAddress, ScriptLength), Bytes(ProgrammeTableAddress, 32), Bytes(SpriteTableAddress, 16), Bytes(TrigAddress, TrigLength), Bytes(NormalizationAddress, NormalizationLength));
        resources.Validate();
        return resources;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, Bytes(address, length));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect298Resources? nativeEffect298Resources;

    public NativeBattleEffect298Resources ReadNativeEffect298Resources()
    {
        if (nativeEffect298Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            NativeBattleEffectFileAsset file = NativeBattleEffect298ResourcesReader.ReadFileNames(executable).Single();
            nativeEffect298Resources = NativeBattleEffect298ResourcesReader.Read(executable, file with { Data = Read(battle, file.Name).ToImmutableArray() });
        }
        return nativeEffect298Resources;
    }
}
