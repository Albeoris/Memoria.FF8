using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWaves184Resources(NativeBattleStaticMeshResource Ring, NativeBattleEffectHeapAllocation CasterDat, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<uint> WarpColors, ImmutableArray<uint> RingColors, ImmutableArray<NativeBattleModuleAnchor> EmitterVectors, ImmutableArray<int> StageDraw, ImmutableArray<byte> CapturePackets)
{
    public void Validate()
    {
        if (Ring.Address != 0xD34450 || Ring.Bytes.IsDefault || Ring.Bytes.Length != 624 || CasterDat.Identity != "FF8_EN:D328B4" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Camera.Identity != "FF8_EN:D32750" || Camera.Data.IsDefault || Camera.Data.Length != 0x164 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x164 || WarpColors.IsDefault || WarpColors.Length != 35 || RingColors.IsDefault || RingColors.Length != 16 || EmitterVectors.IsDefault || EmitterVectors.Length != 8 || StageDraw.IsDefault || StageDraw.Length != 8 || CapturePackets.IsDefault || CapturePackets.Length != 48)
        {
            throw new InvalidDataException("Ultra Waves requires its complete addressed resources.");
        }
        int[] sections = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Ultra Waves requires its original caster animation binding.");
            }
        }
    }
}
