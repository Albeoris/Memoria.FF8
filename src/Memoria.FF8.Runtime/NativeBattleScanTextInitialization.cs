namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanTextInitializationResult(NativeBattleScanTextWindows Windows, sbyte SelectedWindow1D2B51C, NativeBattleScanByteOwner Globals1D2B524, byte Status1D2B513);

public static class NativeBattleScanTextInitialization
{
    public static NativeBattleScanTextWindows InitializeBackendStores4B7170(NativeBattleScanTextWindows windows)
    {
        ValidateBanks(windows);
        byte[] rectangle = new NativeBattleScanRect(64, 64, 128, 128).ToBytes();
        for (byte index = 0; index < 8; index++)
        {
            NativeBattleScanByteOwner backend = windows.Backend(index).Write(8, rectangle).Write(0x10, rectangle).WriteByte(0x38, 6).WriteDword(0x30, 0x38000000).WriteDword(0, 0).WriteDword(4, 0).WriteByte(0x34, 0).WriteByte(0x36, 0).WriteByte(0x37, 3).WriteWord(0x3A, 0).WriteByte(0x3B, index).WriteWord(0x3C, 4096);
            windows = windows with { BackendRows = windows.BackendRows.SetItem(index, backend) };
        }
        return windows;
    }

    public static NativeBattleScanTextInitializationResult InitializeTextStores4A0880(NativeBattleScanTextWindows windows, NativeBattleScanByteOwner globals1D2B524)
    {
        ValidateBanks(windows);
        globals1D2B524.RequireLength(12);
        for (byte index = 0; index < 8; index++)
        {
            NativeBattleScanByteOwner backend = windows.Backend(index).WriteByte(0x34, 1).WriteDword(4, 0x4A09A0).WriteDword(0, 0x4A0C00).WriteByte(0x35, 0).WriteByte(0x3A, index);
            NativeBattleScanByteOwner row = windows.Row(index).WriteByte(0x18, index).WriteWord(0x14, 0).WriteByte(0x19, 0).WriteByte(0x2F, 0);
            windows = windows with { TextRows = windows.TextRows.SetItem(index, row), BackendRows = windows.BackendRows.SetItem(index, backend) };
            windows = windows.SetText4A0410(index, 0);
            row = windows.Row(index).WriteWord(0x10, 4096).WriteWord(0x14, 0).WriteByte(0x16, 0);
            backend = windows.Backend(index).WriteByte(0x37, 6);
            windows = windows with { TextRows = windows.TextRows.SetItem(index, row), BackendRows = windows.BackendRows.SetItem(index, backend) };
            row = windows.Row(index).WriteWord(0x1C, 0).WriteWord(0x1E, 0);
            backend = windows.Backend(index).WriteByte(0x38, 0x0E).WriteDword(0x30, 0x38000000);
            windows = windows with { TextRows = windows.TextRows.SetItem(index, row), BackendRows = windows.BackendRows.SetItem(index, backend) };
            windows = windows.SetRect4A07A0(index, new(64, 64, 128, 128));
            row = windows.Row(index).WriteWord(0x1A, 4096);
            backend = windows.Backend(index).WriteWord(0x3C, 4096);
            row = row.WriteDword(0x24, 0).WriteWord(0x30, 0).WriteByte(0x32, 0).WriteByte(0x2E, (byte)(row.ReadByte(0x2E) & 0x7F)).WriteDword(0x34, 0).WriteDword(0x38, 0);
            windows = windows with { TextRows = windows.TextRows.SetItem(index, row), BackendRows = windows.BackendRows.SetItem(index, backend) };
        }
        globals1D2B524 = globals1D2B524.WriteWord(4, 0).WriteWord(6, 0).WriteByte(0, 0).WriteByte(1, 0);
        return new(windows, -1, globals1D2B524, 0);
    }

    private static void ValidateBanks(NativeBattleScanTextWindows windows)
    {
        if (windows is null || windows.TextRows.IsDefault || windows.TextRows.Length != 8 || windows.BackendRows is null || windows.BackendRows.Count != 8)
        {
            throw new InvalidDataException("Native text startup stores require their actual eight text and backend rows.");
        }
        for (byte index = 0; index < 8; index++)
        {
            windows.Row(index).RequireLength(60);
            windows.Backend(index).RequireLength(64);
        }
    }
}
