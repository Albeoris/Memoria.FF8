namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanTextExpansionServices(Func<uint, byte> ReadOriginalByte, Func<byte, uint> ResolveNamePointer4B9010)
{
    public Func<byte, System.Collections.Immutable.ImmutableArray<byte>>? ReadNameBytes { get; init; }
}
public sealed record NativeBattleScanTextLineStep(NativeBattleScanTextFrame Frame, uint SourcePointer);

public static class NativeBattleScanTextExpansion
{
    public static NativeBattleScanTextFrame Expand4B8B30(NativeBattleScanTextFrame frame, uint sourcePointer, int maximumLength, NativeBattleScanTextExpansionServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.ReadOriginalByte);
        ArgumentNullException.ThrowIfNull(services.ResolveNamePointer4B9010);
        frame.ExpandedContext128.RequireLength(128);
        NativeBattleScanByteOwner output = frame.ExpandedContext128;
        if (sourcePointer == 0)
        {
            return frame with { ExpandedContext128 = output.WriteByte(0, 0) };
        }
        int end = maximumLength < 0 ? 128 : maximumLength;
        int cursor = 0;
        while (true)
        {
            byte token = Read();
            if (cursor >= end)
            {
                return frame with { ExpandedContext128 = output.WriteByte(end, 0) };
            }
            output = output.WriteByte(cursor++, token);
            if (token is 0 or 1 or 2 or 7)
            {
                return frame with { ExpandedContext128 = output };
            }
            if (token >= 0x19 && token <= 0xE7)
            {
                continue;
            }
            if (token == 3)
            {
                byte operand = Read();
                if (services.ReadNameBytes is { } readName)
                {
                    System.Collections.Immutable.ImmutableArray<byte> bytes = readName(operand);
                    int terminator = bytes.AsSpan().IndexOf((byte)0);
                    if (terminator < 0)
                    {
                        throw new InvalidDataException("Native Scan replacement name has no original terminator.");
                    }
                    cursor--;
                    output = output.Write(cursor, bytes.AsSpan()[..(terminator + 1)]);
                    cursor += terminator;
                    continue;
                }
                uint name = services.ResolveNamePointer4B9010(operand);
                cursor--;
                while (true)
                {
                    byte value = services.ReadOriginalByte(name);
                    name = unchecked(name + 1);
                    output = output.WriteByte(cursor, value);
                    if (value == 0)
                    {
                        break;
                    }
                    cursor++;
                }
            }
            else if (token == 6)
            {
                output = output.WriteByte(cursor++, Read());
            }
            else
            {
                throw new NotSupportedException($"Native Scan expansion opcode{token:X2} requires its complete original service.");
            }
        }

        byte Read()
        {
            byte value = services.ReadOriginalByte(sourcePointer);
            sourcePointer = unchecked(sourcePointer + 1);
            return value;
        }
    }

    public static NativeBattleScanTextLineStep AdvanceLine4B8AC0(NativeBattleScanTextFrame frame, uint sourcePointer, Func<uint, byte> readOriginalByte)
    {
        ArgumentNullException.ThrowIfNull(readOriginalByte);
        if (sourcePointer == 0)
        {
            return new(frame, 0);
        }
        while (true)
        {
            byte token = Read();
            if (token == 0)
            {
                return new(frame, 0);
            }
            if (token is 1 or 2 or 7)
            {
                return new(frame, sourcePointer);
            }
            if (token == 6)
            {
                frame = frame with { Formatting1D7660C = Read() };
            }
        }

        byte Read()
        {
            byte value = readOriginalByte(sourcePointer);
            sourcePointer = unchecked(sourcePointer + 1);
            return value;
        }
    }
}
