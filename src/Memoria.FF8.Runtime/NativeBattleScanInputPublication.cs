using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanInputPublicationState([property: JsonRequired] NativeBattleScanInputBank Input, [property: JsonRequired] ImmutableArray<NativeBattleScanByteOwner> RawSamples, [property: JsonRequired] NativeBattleScanByteOwner Settings, [property: JsonRequired] NativeBattleScanByteOwner PollPointers)
{
    public void Validate()
    {
        if (Input is null || Input.Devices.IsDefault || Input.Devices.Length != 2 || Input.Devices.Any(device => device is null) || RawSamples.IsDefault || RawSamples.Length != 2 || RawSamples.Any(sample => sample is null) || Settings is null || PollPointers is null)
        {
            throw new InvalidDataException("Native Scan publication requires both current physical devices, raw samples, settings and poll pointers.");
        }
        foreach (NativeBattleScanByteOwner device in Input.Devices)
        {
            device.RequireLength(196);
        }
        foreach (NativeBattleScanByteOwner sample in RawSamples)
        {
            sample.RequireLength(36);
        }
        Settings.RequireLength(4);
        PollPointers.RequireLength(12);
    }
}

public sealed record NativeBattleScanRepeatStep(NativeBattleScanByteOwner Device, ushort Value);

public static class NativeBattleScanInputPublication
{
    public static NativeBattleScanInputPublicationState Initialize49F540(NativeBattleScanInputPublicationState state, Func<int, uint> readHeld468500)
    {
        state.Validate();
        NativeBattleScanByteOwner pointers = state.PollPointers.WriteDword(0, 0x1D2B29B).WriteDword(4, 0x1D2B29A).WriteDword(8, 0);
        state = state with { PollPointers = pointers, Settings = state.Settings.WriteByte(2, 1) };
        state = Reset49F390(state, 0, 0);
        state = Reset49F390(state, 1, 0);
        return Poll56D990(state, readHeld468500);
    }

    public static NativeBattleScanInputPublicationState Reset49F390(NativeBattleScanInputPublicationState state, int logicalPort, ushort held)
    {
        state.Validate();
        if ((uint)logicalPort >= 2)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalPort));
        }
        int selected = state.Input.Devices[logicalPort].ReadByte(0xC2);
        if (selected >= 2)
        {
            throw new NotSupportedException("Native input initialization requires its actual selected physical device.");
        }
        NativeBattleScanByteOwner device = state.Input.Devices[selected].WriteByte(0x18, 0).WriteByte(0x0A, 0);
        byte delay = state.Settings.ReadByte(0);
        for (int group = 0; group < 4; group++)
        {
            device = device.WriteByte(0x0C + group, delay);
        }
        for (int record = 0; record < 8; record++)
        {
            byte[] bytes = new byte[20];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), held);
            device = device.Write(0x1C + record * 20, bytes);
        }
        return SetDevice(state, selected, device);
    }

    public static NativeBattleScanInputPublicationState Poll56D990(NativeBattleScanInputPublicationState state, Func<int, uint> readHeld468500)
    {
        state.Validate();
        ArgumentNullException.ThrowIfNull(readHeld468500);
        uint held = readHeld468500(0);
        state = WriteRawPointer(state, state.PollPointers.ReadDword(4), unchecked((byte)~(held >> 8)));
        return WriteRawPointer(state, state.PollPointers.ReadDword(0), unchecked((byte)~held));
    }

    public static NativeBattleScanInputPublicationState Publish49E9C0(NativeBattleScanInputPublicationState state, Func<int, uint> readHeld468500)
    {
        state.Validate();
        for (int physical = 0; physical < 2; physical++)
        {
            state = Poll56D990(state, readHeld468500);
            NativeBattleScanByteOwner device = PublishStatus6(state.Input.Devices[physical].WriteByte(0x1A, 6));
            state = SetDevice(state, physical, device);
        }
        for (int physical = 0; physical < 2; physical++)
        {
            NativeBattleScanByteOwner device = state.Input.Devices[physical];
            NativeBattleScanByteOwner raw = state.RawSamples[physical];
            int oldCursor = device.ReadByte(0x18);
            if (oldCursor >= 8)
            {
                throw new NotSupportedException("Native input ring cursor leaves its eight current history records.");
            }
            int oldHeld = device.ReadWord(0x1C + 20 * oldCursor + 2);
            int selector = physical;
            int type = raw.ReadByte(1) >> 4;
            int newHeld;
            if (device.ReadByte(0x1A) == 0)
            {
                newHeld = 0;
            }
            else
            {
                if (raw.ReadByte(0) != 0)
                {
                    newHeld = oldHeld;
                }
                else
                {
                    newHeld = ((raw.ReadByte(2) << 8) | raw.ReadByte(3)) ^ 0xFFFF;
                    if ((newHeld & 0xA000) == 0xA000)
                    {
                        newHeld &= ~0xA000;
                    }
                    if ((newHeld & 0x5000) == 0x5000)
                    {
                        newHeld &= ~0x5000;
                    }
                    if (state.Settings.ReadByte(3) != 0 && physical == 0 && newHeld == 0 && (type is not (5 or 7) || Centered(raw, 4) && Centered(raw, 6)))
                    {
                        selector = 1;
                    }
                    if (type == 1)
                    {
                        throw new NotSupportedException("Native PC mouse input requires its actual mouse position, bounds and4B8710 service.");
                    }
                }
                if (state.Settings.ReadByte(2) == 0)
                {
                    oldHeld = 0;
                    newHeld = 0;
                }
            }
            int cursor = (oldCursor + 1) & 7;
            int record = 0x1C + cursor * 20;
            byte[] prefix = new byte[8];
            for (int index = 0; index < prefix.Length; index++)
            {
                prefix[index] = raw.ReadByte(index);
            }
            device = device.Write(record, prefix).WriteWord(record + 2, (ushort)newHeld).WriteWord(record + 0x10, (ushort)(newHeld & (newHeld ^ oldHeld))).WriteWord(record + 0x12, unchecked((ushort)((newHeld ^ oldHeld) & ~newHeld)));
            for (int group = 0; group < 4; group++)
            {
                NativeBattleScanRepeatStep repeat = Repeat49EC90(device, state.Settings, (ushort)oldHeld, (ushort)newHeld, group);
                device = repeat.Device.WriteWord(record + 8 + group * 2, repeat.Value);
            }
            device = device.WriteByte(0x18, (byte)cursor).WriteByte(0xC2, (byte)selector);
            state = SetDevice(state, physical, device);
        }
        return state;
    }

    public static NativeBattleScanRepeatStep Repeat49EC90(NativeBattleScanByteOwner device, NativeBattleScanByteOwner settings, ushort oldHeld, ushort newHeld, int group)
    {
        device.RequireLength(196);
        settings.RequireLength(4);
        if ((uint)group >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(group));
        }
        int mask = device.ReadWord(0x10 + group * 2);
        int previous = oldHeld & mask;
        int current = newHeld & mask;
        int counter = unchecked((sbyte)device.ReadByte(0x0C + group));
        ushort repeatTiming = settings.ReadWord(0);
        int delay = repeatTiming & 0xFF;
        int interval = repeatTiming >> 8;
        int result;
        if ((previous & current) != 0)
        {
            if (current != previous)
            {
                counter = delay;
            }
            counter--;
            if (counter < 0)
            {
                counter = interval;
                result = current;
            }
            else
            {
                result = 0;
            }
        }
        else
        {
            counter = delay;
            result = current;
        }
        return new(device.WriteByte(0x0C + group, unchecked((byte)counter)), (ushort)result);
    }

    public static NativeBattleScanByteOwner PublishStatus6(NativeBattleScanByteOwner device)
    {
        device.RequireLength(196);
        byte configuring = device.ReadByte(0x19);
        int mask = device.ReadByte(0x1B);
        if (configuring == 1)
        {
            return device.WriteByte(0x0B, 0).WriteDword(0xBC, uint.MaxValue).WriteWord(0xC0, ushort.MaxValue).WriteByte(0x0A, 1).WriteByte(0x19, 0).WriteByte(1, 0).WriteByte(2, 0);
        }
        int second = unchecked((sbyte)device.ReadByte(7)) & mask;
        int first = unchecked((sbyte)device.ReadByte(6)) & mask;
        second >>= device.ReadByte(9) & 31;
        first >>= device.ReadByte(8) & 31;
        return device.WriteByte(0x0A, 1).WriteByte(2, unchecked((byte)second)).WriteByte(1, unchecked((byte)first));
    }

    private static bool Centered(NativeBattleScanByteOwner raw, int offset)
    {
        int x = raw.ReadByte(offset) - 128;
        int y = raw.ReadByte(offset + 1) - 128;
        return x * x + y * y < 1600;
    }

    private static NativeBattleScanInputPublicationState SetDevice(NativeBattleScanInputPublicationState state, int index, NativeBattleScanByteOwner device) => state with { Input = state.Input with { Devices = state.Input.Devices.SetItem(index, device) } };

    private static NativeBattleScanInputPublicationState WriteRawPointer(NativeBattleScanInputPublicationState state, uint address, byte value)
    {
        for (int index = 0; index < 2; index++)
        {
            uint start = checked(0x1D2B298u + (uint)(index * 36));
            if (address >= start && address < start + 36)
            {
                return state with { RawSamples = state.RawSamples.SetItem(index, state.RawSamples[index].WriteByte((int)(address - start), value)) };
            }
        }
        throw new NotSupportedException("Native PC polling pointer leaves its actual retained raw sample owners.");
    }
}
