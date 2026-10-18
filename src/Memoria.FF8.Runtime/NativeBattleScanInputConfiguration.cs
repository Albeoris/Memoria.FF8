namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanConfiguredInputStep(NativeBattleScanInputPublicationState State, NativeBattleScanByteOwner Mouse16);

public static class NativeBattleScanInputConfiguration
{
    public static NativeBattleScanConfiguredInputStep Configure4B9460(NativeBattleScanInputPublicationState state, NativeBattleScanByteOwner mouse16)
    {
        state.Validate();
        mouse16.RequireLength(16);
        state = state with { Settings = state.Settings.WriteByte(0, 16).WriteByte(1, 5).WriteByte(3, 0) };
        mouse16 = mouse16.WriteWord(8, 0).WriteWord(0x0A, 0).WriteWord(0x0C, 319).WriteWord(0x0E, 223);
        for (int physical = 0; physical < 2; physical++)
        {
            NativeBattleScanByteOwner device = state.Input.Devices[physical];
            for (int group = 0; group < 4; group++)
            {
                device = device.WriteByte(0x0C + group, state.Settings.ReadByte(0));
            }
            SetDevice();
            state = NativeBattleScanInputPublication.Reset49F390(state, physical, 0);
            device = state.Input.Devices[physical];
            device = device.WriteByte(0x19, 1).WriteWord(0x10, 0x0FFF).WriteWord(0x12, 0x5000).WriteWord(0x14, 0xA000).WriteWord(0x16, 0x0900).WriteWord(6, 0).WriteByte(0, 0x40).WriteByte(6, 0).WriteDword(0xBC, uint.MaxValue).WriteWord(0xC0, ushort.MaxValue).WriteByte(0x1B, 0).WriteByte(0x0B, 0).WriteByte(0xC3, 0x31);
            mouse16 = mouse16.WriteWord(4 * physical, 0).WriteWord(4 * physical + 2, 0);
            for (int group = 0; group < 4; group++)
            {
                device = device.WriteByte(0x0C + group, 0);
            }
            device = device.WriteByte(0xC2, (byte)physical);
            SetDevice();

            void SetDevice() => state = state with { Input = state.Input with { Devices = state.Input.Devices.SetItem(physical, device) } };
        }
        return new(state, mouse16);
    }
}
