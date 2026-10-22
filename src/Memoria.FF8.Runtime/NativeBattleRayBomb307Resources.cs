using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRayBomb307SoundResource(uint Address, uint Value);
public sealed record NativeBattleRayBomb307Resources(NativeBattleEffectHeapAllocation CasterDat, NativeBattleEffectHeapAllocation Camera, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<uint> MeshSequence, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<int> SpawnX, ImmutableArray<int> SpawnZ, ImmutableArray<NativeBattleModuleAnchor> BirthAnchors, ImmutableArray<NativeBattleModuleAnchor> BirthDirections, ImmutableArray<NativeBattleRayBomb307SoundResource> Sounds)
{
    public void Validate()
    {
        if (CasterDat.Identity != "FF8_EN:CDF86C" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Camera.Identity != "FF8_EN:CDF6D8" || Camera.Data.IsDefault || Camera.Data.Length != 404 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 404 || Meshes.IsDefault || Meshes.Length != 6 || MeshSequence.IsDefault || MeshSequence.Length != 3 || Sprites.IsDefault || Sprites.Length != 4 || SpawnX.IsDefault || SpawnX.Length != 10 || SpawnZ.IsDefault || SpawnZ.Length != 10 || BirthAnchors.IsDefault || BirthAnchors.Length != 2 || BirthDirections.IsDefault || BirthDirections.Length != 2 || Sounds.IsDefault || Sounds.Length != 2)
        {
            throw new InvalidDataException("Ray-Bomb requires its complete addressed resources.");
        }
        uint[] meshAddresses = [0xCE190C, 0xCE2CE4, 0xCE2F9C, 0xCE3254, 0xCE350C, 0xCE3BB4];
        int[] meshLengths = [5080, 696, 696, 696, 1704, 1304];
        for (int index = 0; index < meshAddresses.Length; index++)
        {
            if (Meshes[index].Address != meshAddresses[index] || Meshes[index].Bytes.IsDefault || Meshes[index].Bytes.Length != meshLengths[index])
            {
                throw new InvalidDataException("Ray-Bomb selected an incomplete original static shape.");
            }
        }
        for (int index = 0; index < 3; index++)
        {
            if (MeshSequence[index] != meshAddresses[index + 1])
            {
                throw new InvalidDataException("Ray-Bomb requires its original three-shape sequence.");
            }
        }
        uint[] spriteAddresses = [0xCE13E0, 0xCE158C, 0xCE15B4, 0xCE1760];
        int[] spriteLengths = [428, 40, 428, 428];
        for (int index = 0; index < spriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            int frames = index == 1 ? 1 : 16;
            if (sprite.Address != spriteAddresses[index] || sprite.Kind != 0 || sprite.Bytes.IsDefault || sprite.Bytes.Length != spriteLengths[index] || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
            {
                throw new InvalidDataException("Ray-Bomb selected an incomplete original projected sprite.");
            }
            for (int phase = 0; phase < frames; phase++)
            {
                _ = sprite.ProjectedFrame571C80(phase);
            }
        }
        int[] sections = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Ray-Bomb requires its original caster binding directory.");
            }
        }
        if (Sounds[0].Address != 0xCE40CC || Sounds[1].Address != 0xCE40D0)
        {
            throw new InvalidDataException("Ray-Bomb requires its original sound descriptor identities.");
        }
    }
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.SingleOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Ray-Bomb selected an unsupported static shape.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.SingleOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Ray-Bomb selected an unsupported projected sprite.");
}
