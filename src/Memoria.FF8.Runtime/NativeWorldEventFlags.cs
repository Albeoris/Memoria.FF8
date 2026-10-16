using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved-world DWORD116/120: authenticated value bits, with unknown historical bits retained explicitly.</summary>
public sealed record NativeWorldEventFlags([property: JsonRequired] ulong Value, [property: JsonRequired] ulong KnownMask)
{
    public static NativeWorldEventFlags CreateNewGame() => new(0, ulong.MaxValue);

    public bool IsKnown(ushort operand) => (KnownMask & (1UL << Bit(operand))) != 0;

    public void Validate()
    {
        if ((Value & ~KnownMask) != 0)
        {
            throw new InvalidDataException("Saved-world event flags contain a value outside their authenticated mask.");
        }
    }

    public bool Matches(ushort operand)
    {
        Validate();
        //546100 selects DWORD120 for any unsigned low operand byte>=32; x86 shifts mask to five bits.
        int bit = Bit(operand);
        ulong mask = 1UL << bit;
        if ((KnownMask & mask) == 0)
        {
            throw new NotSupportedException($"World event FF27 requires authenticated saved-world flag{bit} at116/120.");
        }
        return ((Value & mask) != 0 ? 1 : 0) == operand >> 8;
    }

    private static int Bit(ushort operand) => ((operand & 255) >= 32 ? 32 : 0) + (operand & 31);
}
