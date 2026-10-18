using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCactuar199ResourcesReader
{
    public static NativeBattleCactuar199Resources Read(byte[] executable, NativeBattleEffectHeapAllocation initializerHeapTim)
    {
        if (initializerHeapTim.Identity != "mag198.tim" || initializerHeapTim.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Cactuar requires its complete original named heap TIM input.");
        }
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = Read(0xE94918, 0x21E8);
        int[] sections = [0x18, 0x238, 0x208C, 0x21E8, 0x21E8];
        if (U32(bytes, 0) != 4)
        {
            throw new InvalidDataException("Native Cactuar model requires its original four-section source.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Cactuar model requires its exact original extent and empty final section.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0xE94918, bytes, pose, geometry);
        NativeBattleEffectHeapAllocation seed = Allocation(0xE94918, 0x21EC);
        if (!seed.Data.AsSpan().Slice(0, bytes.Length).SequenceEqual(bytes.AsSpan()) || U32(seed.Data, 0x21E8) != 0x10)
        {
            throw new InvalidDataException("Native Cactuar entry copy requires its original adjacent TIM header DWORD.");
        }
        NativeBattleCactuar199SpriteResource sprite = new(0xCF0740, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xCF0740));
        if (sprite.Bytes.Length != 0x1AC || sprite.Bytes[3] != 7 || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 16)
        {
            throw new InvalidDataException("Native Cactuar projected sprite requires its original direct sixteen-frame source.");
        }
        NativeBattleStaticMeshResource first = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCF08ECu);
        NativeBattleStaticMeshResource second = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCF11BCu);
        NativeBattleStaticMeshResource third = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCF26F8u);
        if (first.Bytes.Length != 0x8D0 || second.Bytes.Length != 0x153C || third.Bytes.Length != 0xC20)
        {
            throw new InvalidDataException("Native Cactuar static mesh leaves its complete original resource extent.");
        }
        ImmutableArray<byte> vibrationBytes = Read(0xCF3318, 0x240);
        if (U32(vibrationBytes, 0) != 0 || U32(vibrationBytes, 4) != 0x100)
        {
            throw new InvalidDataException("Native Cactuar vibration requires its reached null and program1 directory entries.");
        }
        NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [0, 0x100], vibrationBytes);
        vibration.Validate();
        NativeBattleEffectHeapAllocation camera = Allocation(0xCF35BC, 0xAA);
        if (BinaryPrimitives.ReadUInt16LittleEndian(camera.Data.AsSpan().Slice(0xA8)) != 0x8000)
        {
            throw new InvalidDataException("Native Cactuar camera requires its original bounded terminal WORD.");
        }
        return new(initializerHeapTim, model, seed, Allocation(0xE96B00, 0x4220), sprite, first, second, third, new(0xCF3318, vibration), camera, Allocation(0xCF3668, 0x400), Allocation(0xCF3568, 0x10), Allocation(0xCF3578, 0x10), Allocation(0xCF3594, 0x10), Allocation(0xCF35A4, 0x18), Allocation(0xCF3558, 8), Allocation(0xE94910, 8));

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        NativeBattleEffectHeapAllocation Allocation(uint address, int length) => new($"FF8_EN:{address:X}", Read(address, length));
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleCactuar199Resources? nativeCactuar199Resources;
    public NativeBattleCactuar199Resources ReadNativeCactuar199Resources()
    {
        if (nativeCactuar199Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectHeapAllocation initializer = new("mag198.tim", Read(magic, "mag198.tim").ToImmutableArray());
            nativeCactuar199Resources = NativeBattleCactuar199ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), initializer);
        }
        return nativeCactuar199Resources;
    }
}
