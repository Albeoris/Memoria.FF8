using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanTextFrame([property: JsonRequired] NativeBattleScanTextWindows Windows, [property: JsonRequired] NativeBattleScanByteOwner ExpandedContext128, [property: JsonRequired] byte Formatting1D7660C, [property: JsonRequired] sbyte SelectedWindow1D2B51C, [property: JsonRequired] NativeBattleScanByteOwner Globals1D2B524);
public sealed record NativeBattleScanTextServices(Func<NativeBattleScanTextFrame, uint, NativeBattleScanTextFrame> Expand4B8B30, Func<uint, byte> ReadOriginalByte, Func<NativeBattleScanTextFrame, int, NativeBattleScanTextFrame> Sound4B92A0);

public static class NativeBattleScanTextUpdate
{
    public static NativeBattleScanTextFrame Advance49FEB0(NativeBattleScanTextFrame frame, int index, uint packedInput, uint secondaryInput, NativeBattleScanTextServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.Expand4B8B30);
        ArgumentNullException.ThrowIfNull(services.ReadOriginalByte);
        ArgumentNullException.ThrowIfNull(services.Sound4B92A0);
        frame.ExpandedContext128.RequireLength(128);
        frame.Globals1D2B524.RequireLength(12);
        NativeBattleScanByteOwner row = frame.Windows.Row(index);
        int progress = Math.Clamp(unchecked((short)row.ReadWord(0x1C)) + unchecked((short)row.ReadWord(0x1E)), 0, 4096);
        Store(row.WriteWord(0x1C, (ushort)progress));
        byte handle = Row().ReadByte(0x18);
        NativeBattleScanByteOwner backend = frame.Windows.Backend(handle).WriteByte(0x35, progress != 0 ? (byte)1 : (byte)0);
        frame = frame with { Windows = frame.Windows with { BackendRows = frame.Windows.BackendRows.SetItem(handle, backend) } };
        if (progress == 0)
        {
            Store(Row().WriteByte(0x16, 0));
        }
        else
        {
            row = Row();
            NativeBattleScanRect rectangle = NativeBattleScanTextWindows.Scale4A03A0(new(unchecked((short)row.ReadWord(0)), unchecked((short)row.ReadWord(2)), unchecked((short)row.ReadWord(4)), unchecked((short)row.ReadWord(6))), (short)progress);
            NativeBattleScanRect inner = new(unchecked((short)(rectangle.X + 6)), unchecked((short)(rectangle.Y + 6)), unchecked((short)(rectangle.Width - 12)), unchecked((short)(rectangle.Height - 12)));
            if (inner.Height <= 0)
            {
                inner = inner with { Height = 1 };
            }
            if (inner.Width <= 1)
            {
                inner = inner with { Width = 2 };
            }
            backend = backend.Write(8, rectangle.ToBytes()).Write(0x10, inner.ToBytes());
            frame = frame with { Windows = frame.Windows with { BackendRows = frame.Windows.BackendRows.SetItem(handle, backend) } };
        }
        if (Row().ReadDword(8) == 0 || progress < 4096)
        {
            return frame;
        }
        uint held = frame.SelectedWindow1D2B51C == index ? packedInput & 0xFFFF : 0;
        uint edge = frame.SelectedWindow1D2B51C == index ? packedInput >> 16 : 0;
        uint repeat = frame.SelectedWindow1D2B51C == index ? secondaryInput : 0;
        uint currentToken = held;
        uint state = Row().ReadDword(0x24);
        while (true)
        {
            row = Row();
            switch (state)
            {
                case 0:
                    frame = frame with { Globals1D2B524 = frame.Globals1D2B524.WriteWord(4, 0).WriteWord(6, 0).WriteByte(0, 0).WriteByte(1, 0) };
                    Persist(1);
                    state = 1;
                    break;
                case 1:
                    Store(row.WriteWord(0x14, unchecked((ushort)(row.ReadWord(0x14) + row.ReadWord(0x10)))));
                    state = 2;
                    break;
                case 2:
                    if (row.ReadWord(0x10) != 0 && unchecked((short)row.ReadWord(0x14)) < 4096)
                    {
                        return frame;
                    }
                    Store(row.WriteWord(0x14, unchecked((ushort)(row.ReadWord(0x14) - 4096))));
                    Expand();
                    state = 3;
                    break;
                case 3:
                    currentToken = frame.ExpandedContext128.ReadByte(row.ReadByte(0x20));
                    if (currentToken is 1 or 7)
                    {
                        state = 8;
                        break;
                    }
                    int consumed = Consume();
                    currentToken = unchecked((byte)consumed);
                    int command = consumed >> 8;
                    if (currentToken == 2)
                    {
                        state = 4;
                    }
                    else if (currentToken == 0)
                    {
                        state = 6;
                    }
                    else if (command == 8)
                    {
                        int operand = (int)currentToken - 0x20;
                        int speed = operand == 0 ? 4096 : operand == 1 ? 0 : 4096 / (operand - 1);
                        Store(Row().WriteWord(0x10, unchecked((ushort)speed)));
                        state = 3;
                    }
                    else if (command == 9)
                    {
                        Store(Row().WriteByte(0x33, unchecked((byte)(currentToken - 0x20))));
                        state = 14;
                    }
                    else if (command == 6)
                    {
                        Store(Row().WriteByte(0x17, (byte)((Row().ReadByte(0x17) & 0xF0u) | (currentToken & 0xFu))));
                        state = 3;
                    }
                    else
                    {
                        state = Row().ReadWord(0x10) != 0 ? 2u : 3u;
                    }
                    break;
                case 4:
                    if (row.ReadByte(0x21) < row.ReadByte(0x23))
                    {
                        state = 3;
                        break;
                    }
                    Store(row.WriteWord(0x14, unchecked((ushort)(row.ReadWord(0x14) + 4096))).WriteByte(0x21, unchecked((byte)(row.ReadByte(0x21) - 1))));
                    Persist(5);
                    state = 5;
                    break;
                case 5:
                    short scroll = unchecked((short)(row.ReadWord(0x12) + 1));
                    Store(row.WriteWord(0x12, unchecked((ushort)scroll)));
                    if (scroll % 16 == 0)
                    {
                        Persist(1);
                    }
                    return frame;
                case 6:
                    Store(row.WriteByte(0x28, 1));
                    if (row.ReadByte(0x29) == 0xFF)
                    {
                        Persist(7);
                        state = 7;
                    }
                    else
                    {
                        state = 11;
                    }
                    break;
                case 7:
                    return frame;
                case 8:
                    Store(row.WriteByte(0x2E, currentToken == 7 ? (byte)0x80 : (byte)(row.ReadByte(0x2E) & 0x7F)));
                    Persist(9);
                    return frame;
                case 9:
                    IncrementIndicator();
                    if ((held & 0xFFFF0FFF) == 0)
                    {
                        Persist(10);
                    }
                    return frame;
                case 10:
                    IncrementIndicator();
                    if ((held & 0xC0) == 0)
                    {
                        return frame;
                    }
                    row = Row();
                    byte old = row.ReadByte(0x17);
                    Store(row.WriteByte(0x2E, (byte)(row.ReadByte(0x2E) & 0x7F)).WriteByte(0x17, unchecked((byte)((old & 0xF) | (old << 4)))));
                    Expand();
                    Consume();
                    Expand();
                    Persist(1);
                    return frame;
                case 11:
                    Persist(12);
                    state = 12;
                    break;
                case 12:
                    if (held == 0)
                    {
                        Persist(13);
                    }
                    return frame;
                case 13:
                    if ((edge & 0xC0) != 0)
                    {
                        Store(row.WriteByte(0x19, 1));
                        Sound(2);
                        Persist(7);
                        return frame;
                    }
                    if ((edge & 0x10) != 0)
                    {
                        int choice = unchecked((sbyte)row.ReadByte(0x2C));
                        if (choice >= 0)
                        {
                            Sound(3);
                        }
                        else
                        {
                            choice = (int)held;
                        }
                        int lower = Row().ReadByte(0x29);
                        int upper = Row().ReadByte(0x2A);
                        choice = choice < lower ? lower : choice > upper ? upper : choice;
                        Store(Row().WriteByte(0x2B, unchecked((byte)choice)));
                    }
                    else if ((repeat & 0x1000) != 0 || (repeat & 0x4000) != 0)
                    {
                        int original = row.ReadByte(0x2B);
                        int choice;
                        if ((repeat & 0x1000) != 0)
                        {
                            choice = original - 1;
                            if (choice < row.ReadByte(0x29))
                            {
                                choice = row.ReadByte(0x2A);
                            }
                        }
                        else
                        {
                            choice = original + 1;
                            if (choice > row.ReadByte(0x2A))
                            {
                                choice = row.ReadByte(0x29);
                            }
                        }
                        if (choice != original)
                        {
                            Sound(1);
                        }
                        Store(Row().WriteByte(0x2B, unchecked((byte)choice)));
                    }
                    return frame;
                case 14:
                    Persist(15);
                    state = 15;
                    break;
                case 15:
                    if (row.ReadByte(0x33) != 0)
                    {
                        Store(row.WriteByte(0x33, unchecked((byte)(row.ReadByte(0x33) - 1))));
                        return frame;
                    }
                    Persist(1);
                    state = 2;
                    break;
                case 16:
                    Persist(17);
                    state = 17;
                    break;
                case 17:
                    if ((held & 0xF000) == 0)
                    {
                        Persist(11);
                    }
                    return frame;
                default:
                    return frame;
            }
        }

        NativeBattleScanByteOwner Row() => frame.Windows.Row(index);
        void Store(NativeBattleScanByteOwner value) => frame = frame with { Windows = frame.Windows with { TextRows = frame.Windows.TextRows.SetItem(index, value) } };
        void Persist(uint value) => Store(Row().WriteDword(0x24, value));
        void Expand() => frame = services.Expand4B8B30(frame, Row().ReadDword(0x0C));
        int Consume()
        {
            NativeBattleScanTextConsumeStep step = NativeBattleScanTextConsumption.Consume4B9170(frame, index, services);
            frame = step.Frame;
            return step.Token;
        }
        void Sound(int value) => frame = services.Sound4B92A0(frame, value);
        void IncrementIndicator()
        {
            byte value = Row().ReadByte(0x2E);
            Store(Row().WriteByte(0x2E, (byte)((value & 0x80) | ((value + 1) & 0x7F))));
        }
    }
}
