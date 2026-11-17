using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed partial class BattleSequenceSet
{
    /// <summary>Reads an unconditional A8/6 service request in the original death sequence prefix.</summary>
    public bool DeathSequenceRequestsAllocationRelease()
    {
        if (!Contains(3))
        {
            return false;
        }
        int start = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6));
        int end = bytes.Length;
        for (int index = 1; index <= BinaryPrimitives.ReadUInt16LittleEndian(bytes); index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2));
            if (offset > start)
            {
                end = Math.Min(end, offset);
            }
        }
        for (int cursor = start; cursor < end;)
        {
            byte opcode = bytes[cursor++];
            switch (opcode)
            {
                case < 0x80:
                case 0x9D:
                case 0xA1:
                    break;
                case 0xA0 when cursor < end:
                    cursor++;
                    break;
                case 0x97 or 0x98 or 0xB5 or 0xB6 or 0xB8 when cursor + 2 <= end:
                    cursor += (bytes[cursor + 1] & 2) != 0 ? 3 : 2;
                    break;
                case 0xA8 when cursor < end:
                    return bytes[cursor] == 6;
                default:
                    // A control branch or any unrepresented service terminates this unconditional proof.
                    return false;
            }
        }
        return false;
    }
}
