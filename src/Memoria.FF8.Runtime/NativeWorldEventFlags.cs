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

    public NativeWorldEventFlags WithOperand(ushort operand)
    {
        Validate();
        //54D7E0/FF28 uses a signed low byte for bank selection, unlike546100/FF27.
        int selector = unchecked((sbyte)operand);
        int bit = (selector >= 32 ? 32 : 0) + (selector & 31);
        ulong mask = 1UL << bit;
        return new NativeWorldEventFlags(operand >> 8 != 0 ? Value | mask : Value & ~mask, KnownMask | mask);
    }

    private static int Bit(ushort operand) => ((operand & 255) >= 32 ? 32 : 0) + (operand & 31);
}
