using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDiablos325ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(727, 6).Select(id =>
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
            byte[] bytes = memory.Read(address, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Diablos file directory requires its original terminated name.");
            }
            return new NativeBattleEffectFileAsset(checked((short)id), Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleDiablos325Resources Read(byte[] executable) => Read(new NativeExecutableMemory(executable));

    public static NativeBattleDiablos325Resources Read(IGameNativeMemorySource memory)
    {
        ImmutableArray<byte> vibrationBytes = memory.Read(0xFD6658, 0x148).ToImmutableArray();
        ImmutableArray<int> offsets = Enumerable.Range(0, 64).Select(index => BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(index * 4, 4))).ToImmutableArray();
        NativeVibrationBank vibration = new(memory.SourceHash, offsets, vibrationBytes);
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, memory.Read(0xFD8584, 0x3890).ToImmutableArray()), vibration) { WellGeometry = memory.Read(0xFD6B6C, 6680).ToImmutableArray(), PuffSprite = memory.Read(0xFD67A0, 116).ToImmutableArray(), PrimitiveKinds = memory.Read(0xFE0FB8, 4).ToImmutableArray(), TextureKinds = memory.Read(0xFE0FC0, 4).ToImmutableArray(), ParticlePrimitiveKinds = memory.Read(0xFE0FC8, 1).ToImmutableArray(), ParticleTextureKinds = memory.Read(0xFE0FB4, 1).ToImmutableArray(), Sounds = Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xFD6640u + (uint)index * 4), 4))).ToImmutableArray() };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDiablos325Resources? nativeDiablos325Resources;

    public NativeBattleDiablos325Resources ReadNativeDiablos325Resources()
    {
        if (nativeDiablos325Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleDiablos325ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeDiablos325Resources = NativeBattleDiablos325ResourcesReader.Read(executable) with { Files = files };
        }
        return nativeDiablos325Resources;
    }
}
