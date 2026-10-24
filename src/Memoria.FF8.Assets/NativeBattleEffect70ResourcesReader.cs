using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect70ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(360, 2).Select(id =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1 || Encoding.ASCII.GetString(bytes.AsSpan(0, end)) != $"MA8DEF_{id - 360}.TIM")
            {
                throw new InvalidDataException("Effect70 requires its original native streamed-file directory.");
            }
            return new NativeBattleEffectFileAsset(checked((short)id), Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleEffect70Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 15)) != "mag069-259.tim\0")
        {
            throw new InvalidDataException("Effect70 requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 2 || files.Where((file, index) => file.FileId != names[index].FileId || file.Name != names[index].Name).Any())
        {
            throw new InvalidDataException("Effect70 streamed inputs disagree with their native directory.");
        }
        ImmutableArray<byte> vibration = Bytes(VibrationAddress, VibrationLength);
        NativeBattleEffect70Resources result = new(mainTim, ActAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ActLengths[index]))).ToImmutableArray(), PuffAddresses.Select((address, index) => new NativeBattleModuleResource(0, address, 0, 0, Bytes(address, PuffLengths[index]))).ToImmutableArray(), new(0, ParticleMeshAddress, Bytes(ParticleMeshAddress, ParticleMeshLength)), Bytes(DefaultDispatchAddress, 16), Bytes(MainDispatchAddress, 15), new(memory.SourceHash, [BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibration.AsSpan().Slice(4))], vibration), SoundAddresses.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4))), files.ToImmutableDictionary(file => file.FileId));
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect70Resources? nativeEffect70Resources;

    public NativeBattleEffect70Resources ReadNativeEffect70Resources()
    {
        if (nativeEffect70Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffect70ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset tim = new("mag069-259.tim", Read(magic, "mag069-259.tim").ToImmutableArray());
            nativeEffect70Resources = NativeBattleEffect70ResourcesReader.Read(executable, tim, files);
        }
        return nativeEffect70Resources;
    }
}
