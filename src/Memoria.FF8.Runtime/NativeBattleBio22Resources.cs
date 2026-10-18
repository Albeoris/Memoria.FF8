using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated geometry sources; the four writable meshes have separate retained execution owners.</summary>
public sealed record NativeBattleBio22Resources([property: JsonRequired] NativeBattleEffectHeapAllocation Animation, [property: JsonRequired] NativeBattleStaticMeshResource MeshBaseline, [property: JsonRequired] NativeBattleStaticMeshResource SecondMeshBaseline, [property: JsonRequired] ImmutableArray<NativeBattleEffectHeapAllocation> MorphSources, [property: JsonRequired] NativeBattleEffectHeapAllocation MorphPointers, [property: JsonRequired] NativeBattleEffectHeapAllocation SoundDescriptor, [property: JsonRequired] NativeBattleStaticMeshResource ThirdMeshBaseline, [property: JsonRequired] NativeBattleStaticMeshResource FourthMeshBaseline, [property: JsonRequired] ImmutableArray<NativeBattleEffectHeapAllocation> SecondFamilyInitialSources, [property: JsonRequired] NativeBattleEffectHeapAllocation SecondFamilyMorphPointers, [property: JsonRequired] ImmutableArray<NativeBattleEffectHeapAllocation> SecondFamilyMorphSources, [property: JsonRequired] ImmutableArray<NativeBattleModuleResource> Sprites)
{
    [JsonRequired]
    public NativeBattleEffectHeapAllocation OverlayProgram { get; init; } = null!;

    [JsonRequired]
    public NativeBattleEffectHeapAllocation PlanarCounts { get; init; } = null!;

    [JsonRequired]
    public NativeBattleEffectHeapAllocation BillboardCounts { get; init; } = null!;

    [JsonIgnore]
    public NativeBattleEffectHeapAllocation MorphFirst => MorphSources[0];

    [JsonIgnore]
    public NativeBattleEffectHeapAllocation MorphSecond => MorphSources[1];

    [JsonIgnore]
    public NativeBattleEffectHeapAllocation FirstMorphPointers => new(MorphPointers.Identity, MorphPointers.Data.AsSpan().Slice(0, 8).ToArray().ToImmutableArray());

    [JsonIgnore]
    public uint Sound => BinaryPrimitives.ReadUInt32LittleEndian(SoundDescriptor.Data.AsSpan());

    public NativeBattleModuleResource Sprite(uint address)
    {
        if (Sprites.IsDefault)
        {
            throw new InvalidDataException("Native Bio sprite profile is missing.");
        }
        return Sprites.SingleOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Native Bio sprite pointer leaves its supplied original resources.");
    }

    public void Validate()
    {
        Allocation(OverlayProgram, "FF8_EN:162693C", 0x10);
        Allocation(PlanarCounts, "FF8_EN:162694C", 0x15);
        Allocation(BillboardCounts, "FF8_EN:162696C", 0x15);
        Allocation(Animation, "FF8_EN:1626014", 0x918);
        Allocation(MorphPointers, "FF8_EN:16189CC", 0x1C);
        Allocation(SoundDescriptor, "FF8_EN:16146E4", 4);
        if (BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != Animation.Data.Length || MorphSources.IsDefault || MorphSources.Length != 7)
        {
            throw new InvalidDataException("Native Bio requires its complete animation and seven original morph sources.");
        }
        uint[] addresses = [0x1615A04, 0x1617164, 0x1617444, 0x1617724, 0x1617A04, 0x1617CE4, 0x1618874];
        for (int index = 0; index < addresses.Length; index++)
        {
            int count = index < 5 ? 91 : 42;
            Allocation(MorphSources[index], $"FF8_EN:{addresses[index]:X}", 8 + count * 8);
            NativeBattleEnhancementMorph.Validate(MorphSources[index].Data);
            if (BinaryPrimitives.ReadInt32LittleEndian(MorphSources[index].Data.AsSpan().Slice(4)) != count || BinaryPrimitives.ReadUInt32LittleEndian(MorphPointers.Data.AsSpan().Slice(4 * index)) != addresses[index])
            {
                throw new InvalidDataException("Native Bio morph source counts or pointer directory contradict their complete resource owners.");
            }
        }
        if (Sprites.IsDefault || Sprites.Length != 5)
        {
            throw new InvalidDataException("Native Bio requires its five complete original particle sprites.");
        }
        (uint Address, int Length, int Frames)[] spriteProfiles = [(0x1614F74, 0x1AC, 16), (0x1615120, 0x1AC, 16), (0x16152CC, 0x174, 13), (0x1615440, 0x174, 13), (0x16156F8, 0x160, 13)];
        for (int index = 0; index < spriteProfiles.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            if (sprite is null || sprite.Address != spriteProfiles[index].Address || sprite.Bytes.IsDefault || sprite.Bytes.Length != spriteProfiles[index].Length || sprite.Kind != 0 || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != spriteProfiles[index].Frames)
            {
                throw new InvalidDataException("Native Bio sprite address, extent or frame count contradicts its complete profile.");
            }
            sprite.Validate();
        }
        Mesh(MeshBaseline, 0x1615CE4, 0x1480, 91, 160);
        Mesh(SecondMeshBaseline, 0x1617E3C, 0xA38, 42, 80);
        Mesh(ThirdMeshBaseline, 0x1619888, 0x5028, 467, 598);
        Mesh(FourthMeshBaseline, 0x161F750, 0x4038, 258, 512);
        if (SecondFamilyInitialSources.IsDefault || SecondFamilyInitialSources.Length != 2 || SecondFamilyMorphSources.IsDefault || SecondFamilyMorphSources.Length != 5)
        {
            throw new InvalidDataException("Native Bio requires its second family's two initial and five directory-selected vector banks.");
        }
        Source(SecondFamilyInitialSources[0], 0x16189E8, 467);
        Source(SecondFamilyInitialSources[1], 0x161E8B0, 467);
        Allocation(SecondFamilyMorphPointers, "FF8_EN:1626000", 0x14);
        uint[] secondAddresses = [0x1623FA0, 0x16247B8, 0x1624FD0, 0x16257E8, 0x1623788];
        for (int index = 0; index < secondAddresses.Length; index++)
        {
            Source(SecondFamilyMorphSources[index], secondAddresses[index], 258);
            if (BinaryPrimitives.ReadUInt32LittleEndian(SecondFamilyMorphPointers.Data.AsSpan().Slice(4 * index)) != secondAddresses[index])
            {
                throw new InvalidDataException("Native Bio second-family source directory order contradicts its actual resource banks.");
            }
        }
    }

    private static void Source(NativeBattleEffectHeapAllocation source, uint address, int count)
    {
        Allocation(source, $"FF8_EN:{address:X}", 8 + count * 8);
        NativeBattleEnhancementMorph.Validate(source.Data);
        if (BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4)) != count)
        {
            throw new InvalidDataException("Native Bio source bank vertex count contradicts its complete extent.");
        }
    }

    private static void Mesh(NativeBattleStaticMeshResource mesh, uint address, int length, int count, int faces)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length)
        {
            throw new InvalidDataException("Native Bio requires each complete original mesh baseline at its distinct address.");
        }
        NativeBattleEnhancementMorph.Validate(mesh.Bytes);
        if (BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != count)
        {
            throw new InvalidDataException("Native Bio mesh baseline vertex count contradicts its physical allocation.");
        }
        int directory = 8 + count * 8;
        for (int group = 0; group < 6; group++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(directory + 4 * group)) != 0)
            {
                throw new InvalidDataException("Native Bio mesh baseline has an unexpected primitive group.");
            }
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(directory + 24)) != faces || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(directory + 28 + faces * 28)) != 0)
        {
            throw new InvalidDataException("Native Bio mesh baseline requires its complete GT3-only primitive group.");
        }
    }

    private static void Allocation(NativeBattleEffectHeapAllocation allocation, string identity, int length)
    {
        if (allocation is null || allocation.Identity != identity || allocation.Data.IsDefault || allocation.Data.Length != length)
        {
            throw new InvalidDataException("Native Bio requires the exact externally supplied resource identities and extents.");
        }
    }
}
