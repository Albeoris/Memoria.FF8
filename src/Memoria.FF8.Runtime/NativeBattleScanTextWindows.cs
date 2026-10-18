using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanRect(short X, short Y, short Width, short Height)
{
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), Width);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), Height);
        return bytes;
    }
}

public sealed record NativeBattleScanTextWindows([property: JsonRequired] ImmutableArray<NativeBattleScanByteOwner> TextRows, [property: JsonRequired] ImmutableDictionary<byte, NativeBattleScanByteOwner> BackendRows)
{
    public NativeBattleScanTextWindows SetText4A0410(int index, uint textPointer)
    {
        NativeBattleScanByteOwner row = Row(index).WriteByte(0x28, 0).WriteDword(8, textPointer).WriteDword(0x0C, textPointer).WriteWord(0x12, 0).WriteByte(0x17, 0x77).WriteDword(0x24, 0).WriteByte(0x20, 0).WriteByte(0x21, 0).WriteByte(0x22, 0).WriteByte(0x29, 0xFF).WriteByte(0x2A, 0xFF).WriteByte(0x19, 0);
        return WithRow(index, row);
    }

    public NativeBattleScanTextWindows SetValue49FBC0(int index, ushort value) => WithRow(index, Row(index).WriteWord(0x10, value).WriteWord(0x14, 0));

    public NativeBattleScanTextWindows Open4A0640(int index) => WithRow(index, Row(index).WriteByte(0x16, 1).WriteWord(0x1E, 4096).WriteByte(0x2D, 0));

    public NativeBattleScanTextWindows Close4A0680(int index) => WithRow(index, Row(index).WriteWord(0x1E, 0xF000));

    public NativeBattleScanTextWindows SetFlags4A0700(int index, byte flags)
    {
        byte handle = Row(index).ReadByte(0x18);
        NativeBattleScanByteOwner backend = Backend(handle);
        byte current = (byte)(flags | 8);
        backend = backend.WriteByte(0x38, current).WriteDword(0x30, (current & 1) != 0 ? 0x3A000000u : 0x38000000u);
        return this with { BackendRows = BackendRows.SetItem(handle, backend) };
    }

    public NativeBattleScanTextWindows SetRect4A07A0(int index, NativeBattleScanRect rectangle)
    {
        NativeBattleScanByteOwner row = Row(index);
        byte visibleLines = unchecked((byte)(unchecked((short)(rectangle.Height - 12)) / 16));
        row = row.WriteByte(0x23, visibleLines == 0 ? (byte)1 : visibleLines).Write(0, rectangle.ToBytes());
        NativeBattleScanRect working = Scale4A03A0(rectangle, unchecked((short)row.ReadWord(0x1C)));
        byte handle = row.ReadByte(0x18);
        NativeBattleScanByteOwner backend = Backend(handle).Write(8, working.ToBytes());
        NativeBattleScanRect inner = new(unchecked((short)(working.X + 6)), unchecked((short)(working.Y + 6)), unchecked((short)(working.Width - 12)), unchecked((short)(working.Height - 12)));
        if (inner.Height <= 0)
        {
            inner = inner with { Height = 1 };
        }
        if (inner.Width <= 1)
        {
            inner = inner with { Width = 2 };
        }
        backend = backend.Write(0x10, inner.ToBytes());
        return WithRow(index, row) with { BackendRows = BackendRows.SetItem(handle, backend) };
    }

    public static NativeBattleScanRect Scale4A03A0(NativeBattleScanRect rectangle, short scale)
    {
        if (scale == 4096)
        {
            return rectangle;
        }
        uint centerX = unchecked((uint)rectangle.X + ((uint)(int)rectangle.Width >> 1));
        uint centerY = unchecked((uint)rectangle.Y + ((uint)(int)rectangle.Height >> 1));
        uint width = unchecked((uint)(rectangle.Width * scale)) >> 12;
        uint height = unchecked((uint)(rectangle.Height * scale)) >> 12;
        return new(unchecked((short)(centerX - (width >> 1))), unchecked((short)(centerY - (height >> 1))), unchecked((short)width), unchecked((short)height));
    }

    public NativeBattleScanByteOwner Row(int index)
    {
        if (TextRows.IsDefault || TextRows.Length != 8 || (uint)index >= 8 || TextRows[index] is null)
        {
            throw new InvalidDataException("Native Scan text windows require their eight actual sixty-byte rows.");
        }
        TextRows[index].RequireLength(60);
        return TextRows[index];
    }

    public NativeBattleScanByteOwner Backend(byte handle)
    {
        if (BackendRows is null || !BackendRows.TryGetValue(handle, out NativeBattleScanByteOwner? backend) || backend is null)
        {
            throw new NotSupportedException("Native Scan text window requires its actual current backend handle owner.");
        }
        backend.RequireLength(64);
        return backend;
    }

    private NativeBattleScanTextWindows WithRow(int index, NativeBattleScanByteOwner row) => this with { TextRows = TextRows.SetItem(index, row) };
}
