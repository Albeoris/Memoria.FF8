using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCarbuncle278ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(2);
        for (short id = 717; id <= 718; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] name = memory.Read(pointer, 128);
            int length = name.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Carbuncle file directory requires its original terminated name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(name.AsSpan(0, length)), []));
        }
        return files.MoveToImmutable();
    }

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
        NativeBattleCarbuncle278Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10C0D08u)) { ReturnSprites = new uint[] { 0x10BD170, 0x10BD2FC }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray(), RecipientRingMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10BF480u), RecipientRayMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10BF3A8u), RecipientShardSprite = new(0, 0x10BCE98, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x10BCE98)), RecipientPolarTable = memory.Read(0x10C761C, 16384).ToImmutableArray(), CreatureModel = model, RecipientProgramme = new(0x10C1CD8, memory.Read(0x10C1CD8, 0x930).ToImmutableArray()), SecondCreatureProgramme = new(0x10C5C8C, memory.Read(0x10C5C8C, 0x1840).ToImmutableArray()), CreatureProgramme = new(0x10C2608, memory.Read(0x10C2608, 0x3684).ToImmutableArray()), SparkleSprite = new(0, 0x10BD044, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x10BD044)), Vibration = vibration, CreatureBoundsMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10BD488u), Sounds = Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10C74CCu + (uint)index * 4, 4))).ToImmutableArray(), MirrorAlternateVertices = memory.Read(0x10C1940, 115 * 8).ToImmutableArray(), SecondaryMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10BFB08u) };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCarbuncle278Resources? nativeCarbuncle278Resources;

    public NativeBattleCarbuncle278Resources ReadNativeCarbuncle278Resources()
    {
        if (nativeCarbuncle278Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleCarbuncle278ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeCarbuncle278Resources = NativeBattleCarbuncle278ResourcesReader.Read(executable) with { Files = files };
        }
        return nativeCarbuncle278Resources;
    }
}
