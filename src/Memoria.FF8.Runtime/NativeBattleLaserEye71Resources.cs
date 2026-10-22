using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLaserEye71EmissionRow(short Minimum, short Range, short Count, short Reserved);
public sealed record NativeBattleLaserEye71Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleCuragaExecutableSource Executable, NativeBattleEffectHeapAllocation CasterDat, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleModuleResource ParticleSprite, ImmutableArray<NativeBattleLaserEye71EmissionRow> EmissionRows, uint Sound)
{
    public void Validate()
    {
        if (Camera.Identity != "FF8_EN:158F534" || Camera.Data.IsDefault || Camera.Data.Length != 340 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 340 || Executable.ResourceAddress != NativeBattleLaserEye71Profile.SourceAddress || Executable.Data.IsDefault || Executable.Data.Length != NativeBattleLaserEye71Profile.SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0 || CasterDat.Identity != "FF8_EN:1590024" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Meshes.IsDefault || Meshes.Length != 3 || EmissionRows.IsDefault || EmissionRows.Length != 19 || ParticleSprite.Address != 0x158FF60 || ParticleSprite.Bytes.IsDefault || ParticleSprite.Bytes.Length != 196 || BinaryPrimitives.ReadUInt16LittleEndian(ParticleSprite.Bytes.AsSpan().Slice(8)) != 7)
        {
            throw new InvalidDataException("Laser Eye requires its complete addressed immutable resources.");
        }
        uint[] addresses = [0x158F688, 0x158FDA0, 0x158FE48];
        int[] lengths = [1816, 168, 280];
        for (int index = 0; index < Meshes.Length; index++)
        {
            if (Meshes[index].Address != addresses[index] || Meshes[index].Bytes.IsDefault || Meshes[index].Bytes.Length != lengths[index])
            {
                throw new InvalidDataException("Laser Eye static shape has an incomplete source boundary.");
            }
        }
        int[] sections = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Laser Eye caster binding has an incomplete section directory.");
            }
        }
        for (int phase = 0; phase < 7; phase++)
        {
            _ = ParticleSprite.ProjectedFrame571C80(phase);
        }
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.FirstOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Laser Eye selected an unsupported static shape.");
}
