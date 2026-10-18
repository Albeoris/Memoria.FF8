namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanUiRepeatStep(NativeBattleScanByteOwner Globals1D2B524, ushort Value);

public static class NativeBattleScanUiRepeat
{
    public static NativeBattleScanUiRepeatStep Repeat49FDA0(NativeBattleScanInputPublicationState input, NativeBattleScanByteOwner globals1D2B524, ushort mappedHeld)
    {
        input.Validate();
        globals1D2B524.RequireLength(12);
        int selected = input.Input.Devices[0].ReadByte(0xC2);
        if (selected >= input.Input.Devices.Length)
        {
            throw new NotSupportedException("Native mapped UI repeat requires its actual selected physical device.");
        }
        ushort result = 0;
        for (int group = 0; group < 4; group++)
        {
            NativeBattleScanUiRepeatStep step = Repeat49FE20(input.Input.Devices[selected], input.Settings, globals1D2B524, mappedHeld, group);
            globals1D2B524 = step.Globals1D2B524;
            result |= step.Value;
        }
        return new(globals1D2B524, result);
    }

    public static NativeBattleScanUiRepeatStep Repeat49FE20(NativeBattleScanByteOwner selectedDevice, NativeBattleScanByteOwner settings, NativeBattleScanByteOwner globals1D2B524, ushort mappedHeld, int group)
    {
        selectedDevice.RequireLength(196);
        settings.RequireLength(4);
        globals1D2B524.RequireLength(12);
        if ((uint)group >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(group));
        }
        int mask = selectedDevice.ReadWord(0x10 + group * 2);
        int oldHeld = globals1D2B524.ReadWord(4 + group * 2) & mask;
        int current = mappedHeld & mask;
        globals1D2B524 = globals1D2B524.WriteWord(4 + group * 2, mappedHeld);
        int counter = unchecked((sbyte)globals1D2B524.ReadByte(group));
        ushort timing = settings.ReadWord(0);
        int delay = timing & 0xFF;
        int interval = timing >> 8;
        int value;
        if ((oldHeld & current) != 0)
        {
            if (oldHeld != current)
            {
                counter = delay;
            }
            counter--;
            if (counter < 0)
            {
                counter = interval;
                value = current;
            }
            else
            {
                value = 0;
            }
        }
        else
        {
            counter = delay;
            value = current;
        }
        return new(globals1D2B524.WriteByte(group, unchecked((byte)counter)), unchecked((ushort)value));
    }
}
