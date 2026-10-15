using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    public NativeWorldEncounterDefinition? ReadRandomEncounterDefinition(int x, int rawZ, byte groundType, int disc)
    {
        int segment = 32 * ((rawZ + 294912) % 196608 / 8192) + (x + 393216) % 262144 / 8192;
        if ((uint)segment >= 768 || sections[2] - sections[1] < 768)
        {
            throw new InvalidDataException("Original random encounter region requires a valid wrapped world segment.");
        }
        byte region = wmset[sections[1] + segment];
        int end = sections[0] + BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(sections[0], 4));
        NativeWorldEncounterDefinition? result = null;
        for (int position = sections[0] + 4; position < end; position += 4)
        {
            if (wmset[position] != region || wmset[position + 1] != groundType)
            {
                continue;
            }
            if (result is not null)
            {
                throw new NotSupportedException("Multiple native encounter records match one region/ground; ordered multi-record callback needs explicit representation.");
            }
            int group = BinaryPrimitives.ReadInt16LittleEndian(wmset.AsSpan(position + 2, 2));
            int rateSection = disc == 4 && group >= 81 ? 4 : 2;
            int rateIndex = rateSection == 4 ? group - 81 : group;
            int formationSection = disc == 4 && group >= 80 ? 5 : 3;
            int formationIndex = formationSection == 5 ? group - 80 : group;
            if (rateIndex < 0 || sections[rateSection] + rateIndex >= sections[rateSection + 1] || formationIndex < 0 || sections[formationSection] + 16 * formationIndex + 16 > sections[formationSection + 1])
            {
                throw new InvalidDataException("Native encounter group lies outside its original rate/formation section.");
            }
            ImmutableArray<ushort>.Builder formations = ImmutableArray.CreateBuilder<ushort>(8);
            for (int slot = 0; slot < 8; slot++)
            {
                formations.Add(BinaryPrimitives.ReadUInt16LittleEndian(wmset.AsSpan(sections[formationSection] + 16 * formationIndex + 2 * slot, 2)));
            }
            result = new(wmset[sections[rateSection] + rateIndex], formations.MoveToImmutable());
        }
        return result;
    }
}
