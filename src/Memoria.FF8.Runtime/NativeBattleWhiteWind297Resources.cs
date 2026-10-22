using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWhiteWind297Resources(NativeBattleGfAnimationSource Caster, NativeBattleGfAnimationSource Sweep, NativeBattleGfAnimationSource Target, NativeBattleEffectHeapAllocation Camera, uint Sound, NativeBattleEffectHeapAllocation CasterDat)
{
    public void Validate()
    {
        Source(Caster, 0x105AEFC, 0xA30, 1, 0x638, 4, 22);
        Source(Sweep, 0x105A494, 0xA68, 1, 0x638, 3, 26);
        Source(Target, 0x10583FC, 0x2098, 5, 0x1970, 7, 26);
        if (Camera is null || Camera.Identity != "FF8_EN:105B92C" || Camera.Data.IsDefault || Camera.Data.Length != 0x168 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x168 || Sound != 0x5578A)
        {
            throw new InvalidDataException("Blue Magic White Wind requires its original camera and sound.");
        }
        if (CasterDat is null || CasterDat.Identity != "FF8_EN:1056888" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || BinaryPrimitives.ReadUInt32LittleEndian(CasterDat.Data.AsSpan()) != 3)
        {
            throw new InvalidDataException("Blue Magic White Wind requires its complete original caster binding source.");
        }
        uint[] sections = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < sections.Length; index++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(CasterDat.Data.AsSpan().Slice(4 + index * 4)) != sections[index])
            {
                throw new InvalidDataException("Blue Magic White Wind caster binding has an invalid section directory.");
            }
        }
    }

    private static void Source(NativeBattleGfAnimationSource source, uint address, int length, int shapes, int animation, int nodes, int duration)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()) != shapes || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4)) != animation || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(animation)) != nodes || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(animation + 4)) != duration)
        {
            throw new InvalidDataException("Blue Magic White Wind ACT source disagrees with its original identity and bounded programme.");
        }
    }
}
