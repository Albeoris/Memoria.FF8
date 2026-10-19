using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleTonberry90ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(2);
        for (short id = 416; id <= 417; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Tonberry file directory requires its original terminated name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleTonberry90Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 2)
        {
            throw new InvalidDataException("Native Tonberry requires both original archive files.");
        }
        for (int index = 0; index < files.Length; index++)
        {
            if (files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native Tonberry requires the complete original archive file selected by its directory.");
            }
        }
        ImmutableArray<byte> vibrationBytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x16D7CD0, 0x11C);
        if (BinaryPrimitives.ReadUInt32LittleEndian(vibrationBytes.AsSpan()) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(vibrationBytes.AsSpan().Slice(4)) != 0x100)
        {
            throw new InvalidDataException("Native Tonberry vibration requires its original null and program1 directory entries.");
        }
        NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [0, 0x100], vibrationBytes);
        ImmutableArray<byte> modelBytes = NativeBattleEffectStaticResourcesReader.Read(memory, 0x1547514, 0x73FC);
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(modelBytes.AsSpan(), index)).ToArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(modelBytes.AsSpan()) != 4 || !sections.SequenceEqual(new[] { 0x18, 0x508, 0x524C, 0x73EC, 0x73FC }))
        {
            throw new InvalidDataException("Native Tonberry requires its complete original four-section creature model.");
        }
        NativeBattlePoseSource pose = new(modelBytes.Slice(sections[0], sections[1] - sections[0]), modelBytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        NativeBattleEffectOwnedModelResource model = new(0x1547514, modelBytes, pose, NativeBattleRawGeometryReader.Read(modelBytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount));
        NativeBattleTonberry90Resources resources = new(0x1547170, new("FF8_EN:1547170", NativeBattleEffectStaticResourcesReader.Read(memory, 0x1547170, 0x7B00)), new(0x16D7CD0, vibration), files) { PhaseOneSource = new(0x16D7DEC, NativeBattleEffectStaticResourcesReader.Read(memory, 0x16D7DEC, 0x19154)), CreatureModel = model };
        resources = resources with { CreatureEntrySound = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x154716C, 4)), CreatureParticleSchedules = new("FF8_EN:154EC70", NativeBattleEffectStaticResourcesReader.Read(memory, 0x154EC70, 64)), CreatureProgramSource = new(0x16F0F40, NativeBattleEffectStaticResourcesReader.Read(memory, 0x16F0F40, 0x104C)) };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleTonberry90Resources? nativeTonberry90Resources;

    public NativeBattleTonberry90Resources ReadNativeTonberry90Resources()
    {
        if (nativeTonberry90Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleTonberry90ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            ImmutableArray<NativeBattleEffectInitializerAsset> tims = new[] { "mag089_0.tim", "mag089_1.tim", "mag089_2.tim" }.Select(name => new NativeBattleEffectInitializerAsset(name, Read(magic, name).ToImmutableArray())).ToImmutableArray();
            nativeTonberry90Resources = NativeBattleTonberry90ResourcesReader.Read(executable, files) with { InitializerTims = tims };
        }
        return nativeTonberry90Resources;
    }
}
