using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The command widget's BYTE26, advanced by4BB9E0 and consumed by4BC770/4BCE10.</summary>
public sealed record NativeBattleLimitCommandRow([property: JsonRequired] byte Offset26)
{
    public static NativeBattleLimitCommandRow Reset4BB840() => new(0);

    public NativeBattleLimitCommandRow Advance4BB9E0(ushort heldButtons, byte rowFlags)
    {
        Validate();
        bool opening = (heldButtons & 0x5000) == 0 && (heldButtons & 0x2000) != 0 && (rowFlags & 4) != 0;
        return this with { Offset26 = checked((byte)Math.Clamp(Offset26 + (opening ? 8 : -8), 0, 64)) };
    }

    public bool SelectsLimit(byte rowFlags)
    {
        Validate();
        return Offset26 != 0 && (rowFlags & 4) != 0;
    }

    public NativeBattleLimitCommandRow Confirm4BC770(byte rowFlags) => this with { Offset26 = SelectsLimit(rowFlags) ? (byte)64 : (byte)0 };

    public int OrdinaryClipWidth4BCE10(byte commandWidth)
    {
        Validate();
        //49ACD0 reverses its integer decay table: entry0 is64, entry1 is4096/64.
        //Entry64 is the untouched zero-filled byte immediately after the copied64 entries.
        int curve = 64;
        if (Offset26 == 64)
        {
            curve = 0;
        }
        else if (Offset26 != 0)
        {
            int value = 4096;
            for (int index = 1; index < Offset26; index++)
            {
                value = 9 * value / 10;
            }
            curve = value / 64;
        }
        return commandWidth * curve / 64;
    }

    public void Validate()
    {
        if (Offset26 > 64)
        {
            throw new InvalidDataException("Native command widget BYTE26 exceeds its original0..64 range.");
        }
    }
}
