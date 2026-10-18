using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanByteOwner([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    public void RequireLength(int length)
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != length || Known.Length != length)
        {
            throw new InvalidDataException("Native Scan byte owner requires its complete physical extent and knownness mask.");
        }
    }

    public byte ReadByte(int offset)
    {
        RequireRange(offset, 1);
        return Bytes[offset];
    }

    public ushort ReadWord(int offset)
    {
        RequireRange(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(offset, 2));
    }

    public uint ReadDword(int offset)
    {
        RequireRange(offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(offset, 4));
    }

    public NativeBattleScanByteOwner WriteByte(int offset, byte value) => Write(offset, [value]);

    public NativeBattleScanByteOwner WriteWord(int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Write(offset, bytes);
    }

    public NativeBattleScanByteOwner WriteDword(int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Write(offset, bytes);
    }

    public NativeBattleScanByteOwner Write(int offset, ReadOnlySpan<byte> values)
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != Known.Length || offset < 0 || offset > Bytes.Length - values.Length)
        {
            throw new InvalidDataException("Native Scan write leaves its current physical byte owner.");
        }
        ImmutableArray<byte>.Builder bytes = Bytes.ToBuilder();
        ImmutableArray<bool>.Builder known = Known.ToBuilder();
        for (int index = 0; index < values.Length; index++)
        {
            bytes[offset + index] = values[index];
            known[offset + index] = true;
        }
        return new(bytes.ToImmutable(), known.ToImmutable());
    }

    private void RequireRange(int offset, int length)
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != Known.Length || offset < 0 || offset > Bytes.Length - length)
        {
            throw new InvalidDataException("Native Scan read leaves its current physical byte owner.");
        }
        for (int index = offset; index < offset + length; index++)
        {
            if (!Known[index])
            {
                throw new NotSupportedException("Native Scan requires an uninitialized current input or window byte.");
            }
        }
    }
}

public sealed record NativeBattleScanInputBank([property: JsonRequired] ImmutableArray<NativeBattleScanByteOwner> Devices)
{
    public ushort Read49ED30(int port, int historyOffset)
    {
        (NativeBattleScanByteOwner device, int record) = Select(port, historyOffset);
        return device.ReadWord(record + 2);
    }

    public ushort Read49EDE0(int port, int historyOffset)
    {
        (NativeBattleScanByteOwner device, int record) = Select(port, historyOffset);
        return device.ReadWord(record + 0x10);
    }

    public ushort Read49ED80(int port, int historyOffset)
    {
        (NativeBattleScanByteOwner device, int record) = Select(port, historyOffset);
        return (ushort)(device.ReadWord(record + 8) | device.ReadWord(record + 0x0A) | device.ReadWord(record + 0x0C) | device.ReadWord(record + 0x0E));
    }

    public int Read49F0A0(int port, int axis, int historyOffset)
    {
        (NativeBattleScanByteOwner device, int record) = Select(port, historyOffset);
        if (device.ReadByte(record) != 0)
        {
            return -1;
        }
        if ((device.ReadByte(0xC3) & 0x80) != 0)
        {
            axis ^= 2;
        }
        axis = Math.Clamp(axis, 0, 3);
        if ((device.ReadByte(record + 1) >> 4) is not (5 or 7))
        {
            return -1;
        }
        return device.ReadByte(record + 4 + axis);
    }

    private (NativeBattleScanByteOwner Device, int Record) Select(int port, int historyOffset)
    {
        if (Devices.IsDefault || Devices.Length < 2 || Devices.Any(device => device is null))
        {
            throw new InvalidDataException("Native Scan input requires its actual logical selectors and physical devices.");
        }
        NativeBattleScanByteOwner selector = Devices[port & 1];
        selector.RequireLength(196);
        int selected = selector.ReadByte(0xC2);
        if (selected >= Devices.Length)
        {
            throw new NotSupportedException("Native Scan selected input device is absent from the retained physical bank.");
        }
        NativeBattleScanByteOwner device = Devices[selected];
        device.RequireLength(196);
        int record = 0x1C + 20 * ((unchecked(device.ReadByte(0x18) - historyOffset)) & 7);
        return (device, record);
    }
}

public static class NativeBattleScanInput
{
    public static NativeBattleScanByteOwner Sample8501F0(NativeBattleScanByteOwner state, NativeBattleScanInputBank input, uint options, IReadOnlyList<byte> buttonMap)
    {
        state.RequireLength(48);
        for (int index = 0; index < 18; index++)
        {
            state = state.WriteWord(12 + index * 2, 0);
        }
        ushort mask = NativeInputConfiguration.Project(input.Read49ED30(0, 0), options, buttonMap);
        state = state.WriteDword(0, mask);
        if ((mask & 0xF000) != 0)
        {
            int horizontal = (mask & 3) != 0 ? 0xD4 : (mask & 12) != 0 ? 0xC6 : 0xCC;
            int opposite = (mask & 3) != 0 ? 0xD6 : (mask & 12) != 0 ? 0xC4 : 0xCE;
            int vertical = (mask & 15) != 0 ? 0xD0 : 0xC8;
            int verticalOpposite = (mask & 15) != 0 ? 0xD2 : 0xCA;
            if ((mask & 0x1000) != 0)
            {
                Store(horizontal, 8);
            }
            else if ((mask & 0x4000) != 0)
            {
                Store(opposite, 8);
            }
            if ((mask & 0x8000) != 0)
            {
                Store(vertical, 8);
            }
            else if ((mask & 0x2000) != 0)
            {
                Store(verticalOpposite, 8);
            }
            return state;
        }
        int first = input.Read49F0A0(0, 2, 0);
        int[] axes = first < 0 ? [128, 128, 128, 128] : [first, input.Read49F0A0(0, 3, 0), input.Read49F0A0(0, 0, 0), input.Read49F0A0(0, 1, 0)];
        for (int index = 0; index < 4; index++)
        {
            short value = unchecked((short)axes[index]);
            state = state.WriteWord(4 + index * 2, unchecked((ushort)value));
            int lower = value <= 96 ? (unchecked((short)(96 - value)) << 4) / 96 : 0;
            int upper = value >= 159 ? (unchecked((short)(value - 159)) << 4) / 96 : 0;
            state = state.WriteWord(12 + index * 4, unchecked((ushort)lower)).WriteWord(14 + index * 4, unchecked((ushort)upper));
        }
        if ((mask & 3) != 0)
        {
            Copy(0xD0, 0xB4);
            Copy(0xD2, 0xB6);
            Copy(0xD4, 0xB8);
            Copy(0xD6, 0xBA);
        }
        else if ((mask & 12) != 0)
        {
            Copy(0xC6, 0xB8);
            Copy(0xC4, 0xBA);
            Copy(0xD0, 0xB4);
            Copy(0xD2, 0xB6);
        }
        else
        {
            Copy(0xC8, 0xB4);
            Copy(0xCA, 0xB6);
            Copy(0xCC, 0xB8);
            Copy(0xCE, 0xBA);
        }
        return state;

        void Store(int address, int value) => state = state.WriteWord(address - 0xA8, unchecked((ushort)value));
        void Copy(int destination, int source) => state = state.WriteWord(destination - 0xA8, state.ReadWord(source - 0xA8));
    }
}
