using System.Buffers.Binary;

namespace Albeoris.Games.FF8.Jsm;

/// <summary>
/// A source-backed JSM bytecode operation.
/// </summary>
public sealed class JsmOperation
{
    public const Int32 MinimumParameter = -0x80_0000;
    public const Int32 MaximumParameter = 0x7F_FFFF;

    private const Int32 OpcodeMask = unchecked((Int32)0xFF00_0000);
    private const Int32 ParameterMask = 0x00FF_FFFF;

    private readonly Int32 _opcodeBits;
    private Int32 _parameter;

    internal JsmOperation(Int32 index, Int32 offset, Int32 encodedValue)
    {
        Index = index;
        Offset = offset;
        _opcodeBits = encodedValue & OpcodeMask;
        HasParameter = _opcodeBits != 0;
        Opcode = HasParameter
            ? (Jsm.Opcode)(encodedValue >> 24)
            : (Jsm.Opcode)encodedValue;
        _parameter = DecodeParameter(encodedValue);
    }

    public Int32 Index { get; }

    public Int32 Offset { get; }

    public Jsm.Opcode Opcode { get; }

    public Boolean HasParameter { get; }

    public Int32 Parameter
    {
        get => _parameter;
        set
        {
            if (!HasParameter)
                throw new InvalidOperationException($"Operation {Opcode} does not have an encoded parameter.");

            ValidateParameter(value);
            _parameter = value;
        }
    }

    internal void WriteTo(Span<Byte> destination)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, Encode());
    }

    internal static void ValidateParameter(Int32 value)
    {
        if (value is < MinimumParameter or > MaximumParameter)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"A JSM operation parameter must be in the range [{MinimumParameter}, {MaximumParameter}].");
        }
    }

    private Int32 Encode()
    {
        return HasParameter
            ? _opcodeBits | (_parameter & ParameterMask)
            : (Int32)Opcode;
    }

    private static Int32 DecodeParameter(Int32 encodedValue)
    {
        return (encodedValue & 0x0080_0000) == 0
            ? encodedValue & ParameterMask
            : encodedValue | OpcodeMask;
    }
}
