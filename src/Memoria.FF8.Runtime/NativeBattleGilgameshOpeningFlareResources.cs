using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshOpeningFlareRow(short Frame, short Size, short Weight, short Padding);

public sealed record NativeBattleGilgameshOpeningFlareResources(NativeBattleModuleResource Sprite, ImmutableArray<NativeBattleGilgameshOpeningFlareRow> Rows)
{
    public void Validate()
    {
        if (Sprite is null || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0xA8 || Sprite.Kind != 0 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 6 || Rows.IsDefault || Rows.Length != 8)
        {
            throw new InvalidDataException("Native Gilgamesh opening flare requires its six-frame sprite and eight original placement rows.");
        }
        short[] frames = [0, 1, 2, 5, 4, 3, 1, 2];
        short[] weights = [2560, 1792, 1280, 768, 512, -512, -1280, -1792];
        for (int index = 0; index < Rows.Length; index++)
        {
            NativeBattleGilgameshOpeningFlareRow row = Rows[index];
            if (row is null || row.Frame != frames[index] || row.Size != 352 || row.Weight != weights[index] || row.Padding != 0)
            {
                throw new InvalidDataException("Native Gilgamesh opening-flare placement row contradicts its actual frame, size or signed weight.");
            }
            _ = Sprite.ProjectedFrame571C80(row.Frame);
        }
    }
}
