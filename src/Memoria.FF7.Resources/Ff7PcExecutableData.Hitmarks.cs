using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<Ff7BattleHitmarkDefinition> LoadBattleHitmarks(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        int[] frames = new int[9];
        for (int index = 0; index < 8; index++)
        {
            frames[index] = Ff7BattleHitmarkResources.ReadSprite(magic, index).Count;
        }
        frames[8] = frames[5];
        byte[] table = ReadVirtual(stream, pe, 0x7C3098, 72 * 4);
        byte[] subroutines = ReadVirtual(stream, pe, 0x7C2CD0, 17 * 4);
        Ff7BattleHitmarkDefinition[] definitions = new Ff7BattleHitmarkDefinition[72];
        for (int index = 0; index < definitions.Length; index++)
        {
            List<Ff7BattleHitmarkCue> cues = [];
            ReadScript(BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(index * 4)), 0, true, cues);
            definitions[index] = new(index, cues.AsReadOnly());
        }
        return Array.AsReadOnly(definitions);

        void ReadScript(uint address, int startFrame, bool allowSubroutines, List<Ff7BattleHitmarkCue> cues)
        {
            for (int count = 0; count < 256; count++)
            {
                byte frame = ReadVirtual(stream, pe, address++, 1)[0];
                if (frame == 255)
                {
                    return;
                }
                byte opcode = ReadVirtual(stream, pe, address, 1)[0];
                if (frame > 127)
                {
                    throw new InvalidDataException("FFVII hitmark cue has a negative native frame.");
                }
                if (opcode == 254 && allowSubroutines)
                {
                    byte subroutine = ReadVirtual(stream, pe, address + 1, 1)[0];
                    if (subroutine >= 17)
                    {
                        throw new InvalidDataException("FFVII hitmark references an absent native subroutine.");
                    }
                    ReadScript(BinaryPrimitives.ReadUInt32LittleEndian(subroutines.AsSpan(subroutine * 4)), startFrame + frame, false, cues);
                    address += 2;
                    continue;
                }
                byte[] operands = ReadVirtual(stream, pe, address, 5);
                int sprite = operands[0] - 1;
                int scale = (sbyte)operands[3] << 8;
                if ((uint)sprite >= frames.Length || scale <= 0)
                {
                    throw new InvalidDataException("FFVII hitmark cue references unsupported native sprite data.");
                }
                cues.Add(new(startFrame + frame, sprite, (sbyte)operands[1] << 3, (sbyte)operands[2] << 3, scale, (sbyte)operands[4], frames[sprite]));
                address += 5;
            }
            throw new InvalidDataException("FFVII hitmark script exceeds its native termination budget.");
        }
    }
}
