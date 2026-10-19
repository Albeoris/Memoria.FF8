using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCarbuncle278ResourcesReader
{
    public static NativeBattleCarbuncle278Resources Read(byte[] executable) => Read(new NativeExecutableMemory(executable));

    public static NativeBattleCarbuncle278Resources Read(IGameNativeMemorySource memory)
    {
        ImmutableArray<byte> bytes = memory.Read(0x10B6F94, 0x5F04).ToImmutableArray();
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(bytes.AsSpan(), index)).ToArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 4 || !sections.SequenceEqual(new[] { 0x18, 0x5C8, 0x4B40, 0x5F04, 0x5F04 }))
        {
            throw new InvalidDataException("Native Carbuncle requires its complete original four-section creature model.");
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        NativeBattleEffectOwnedModelResource model = new(0x10B6F94, bytes, pose, NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount));
        ImmutableArray<byte> vibrationBytes = memory.Read(0x10C74DC, 0x140).ToImmutableArray();
        ImmutableArray<int> offsets = Enumerable.Range(0, 64).Select(index => BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeVibrationBank vibration = new(memory.SourceHash, offsets, vibrationBytes);
        vibration.Validate();
        NativeBattleCarbuncle278Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10C0D08u)) { CreatureModel = model, SparkleSprite = new(0, 0x10BD044, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x10BD044)), Vibration = vibration, CreatureBoundsMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10BD488u), Sounds = Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10C74CCu + (uint)index * 4, 4))).ToImmutableArray(), MirrorAlternateVertices = memory.Read(0x10C1940, 115 * 8).ToImmutableArray(), SecondaryMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10BFB08u) };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCarbuncle278Resources? nativeCarbuncle278Resources;

    public NativeBattleCarbuncle278Resources ReadNativeCarbuncle278Resources()
    {
        return nativeCarbuncle278Resources ??= NativeBattleCarbuncle278ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
