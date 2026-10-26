using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect314ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 0x168, 0x169 });

    public static NativeBattleEffect314Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> completionFiles)
    {
        ImmutableArray<NativeBattleEffectFileAsset> expectedFiles = ReadFileNames(executable);
        if (completionFiles.IsDefault || completionFiles.Length != expectedFiles.Length || completionFiles.Where((file, index) => file.FileId != expectedFiles[index].FileId || !string.Equals(file.Name, expectedFiles[index].Name, StringComparison.OrdinalIgnoreCase)).Any())
        {
            throw new InvalidDataException("Effect314 completion files disagree with their original file directory.");
        }
        NativeExecutableMemory memory = new(executable);
        var programs = ImmutableDictionary.CreateBuilder<uint, ImmutableArray<byte>>();
        foreach (uint address in ProgramAddresses)
        {
            int length = 2;
            while (true)
            {
                ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(checked(address + (uint)length), 2));
                length = checked(length + (opcode switch
                {
                    0x1002 or 0x1003 or 0x1012 => 8,
                    0x1004 or 0x1014 or 0x10FF => 2,
                    < 0x1000 => 2,
                    _ => throw new NotSupportedException("Effect314 original particle program reaches an unsupported command.")
                }));
                if (opcode == 0x10FF)
                {
                    break;
                }
            }
            programs.Add(address, Bytes(address, length));
        }
        ImmutableArray<NativeBattleModuleResource> quads = Enumerable.Range(0, QuadAddresses.Length).Select(index => new NativeBattleModuleResource(checked((byte)index), QuadAddresses[index], 0, 0, Bytes(QuadAddresses[index], QuadLengths[index]))).ToImmutableArray();
        NativeBattleEffect314Resources result = new(mainTim, new("effect314-camera", Bytes(CameraAddress, 268)), new("effect314-vibration", Bytes(VibrationAddress, 294)), quads, programs.ToImmutable()) { Sound = Bytes(SoundAddress, 4), CompletionFiles = completionFiles };
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect314Resources? nativeEffect314Resources;

    public NativeBattleEffect314Resources ReadNativeEffect314Resources()
    {
        if (nativeEffect314Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            byte[] executable = ReadNativeExecutable();
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffect314ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeEffect314Resources = NativeBattleEffect314ResourcesReader.Read(executable, new("mag313.tim", Read(magic, "mag313.tim").ToImmutableArray()), files);
        }
        return nativeEffect314Resources;
    }
}
