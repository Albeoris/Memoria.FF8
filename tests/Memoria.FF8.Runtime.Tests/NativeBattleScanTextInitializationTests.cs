using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTextInitializationTests
{
    [Fact]
    public void Backend_stores_initialize_all_eight_owners_but_leave_visibility_clipped_rects_and_unknown_tail_untouched()
    {
        NativeBattleScanTextWindows windows = Windows();
        NativeBattleScanTextWindows initialized = NativeBattleScanTextInitialization.InitializeBackendStores4B7170(windows);
        for (byte index = 0; index < 8; index++)
        {
            NativeBattleScanByteOwner backend = initialized.Backend(index);
            byte[] rectangle = new NativeBattleScanRect(64, 64, 128, 128).ToBytes();
            Assert.Equal(rectangle, backend.Bytes.AsSpan(8, 8).ToArray());
            Assert.Equal(rectangle, backend.Bytes.AsSpan(0x10, 8).ToArray());
            Assert.Equal(0u, backend.ReadDword(0));
            Assert.Equal(0u, backend.ReadDword(4));
            Assert.Equal((byte)6, backend.ReadByte(0x38));
            Assert.Equal(0x38000000u, backend.ReadDword(0x30));
            Assert.Equal((byte)0, backend.ReadByte(0x34));
            Assert.Equal((byte)0, backend.ReadByte(0x36));
            Assert.Equal((byte)3, backend.ReadByte(0x37));
            Assert.Equal((byte)0, backend.ReadByte(0x3A));
            Assert.Equal(index, backend.ReadByte(0x3B));
            Assert.Equal((ushort)4096, backend.ReadWord(0x3C));
            Assert.False(backend.Known[0x35]);
            Assert.All(backend.Known.Skip(0x18).Take(24), known => Assert.False(known));
            Assert.False(backend.Known[0x3E]);
            Assert.Same(windows.Row(index), initialized.Row(index));
        }
    }

    [Fact]
    public void Text_stores_publish_exact_callbacks_rectangles_priorities_and_globals_without_claiming_numeric_font_initialization()
    {
        NativeBattleScanTextWindows windows = NativeBattleScanTextInitialization.InitializeBackendStores4B7170(Windows());
        NativeBattleScanTextInitializationResult initialized = NativeBattleScanTextInitialization.InitializeTextStores4A0880(windows, Unknown(12));
        Assert.Equal((sbyte)-1, initialized.SelectedWindow1D2B51C);
        Assert.Equal((byte)0, initialized.Status1D2B513);
        Assert.Equal((ushort)0, initialized.Globals1D2B524.ReadWord(0));
        Assert.Equal(0u, initialized.Globals1D2B524.ReadDword(4));
        Assert.False(initialized.Globals1D2B524.Known[2]);
        Assert.False(initialized.Globals1D2B524.Known[3]);
        Assert.All(initialized.Globals1D2B524.Known.Skip(8), known => Assert.False(known));
        for (byte index = 0; index < 8; index++)
        {
            NativeBattleScanByteOwner row = initialized.Windows.Row(index);
            NativeBattleScanByteOwner backend = initialized.Windows.Backend(index);
            Assert.Equal(index, row.ReadByte(0x18));
            Assert.Equal((byte)0, row.ReadByte(0x19));
            Assert.Equal((ushort)4096, row.ReadWord(0x10));
            Assert.Equal((ushort)4096, row.ReadWord(0x1A));
            Assert.Equal(0u, row.ReadDword(8));
            Assert.Equal(0u, row.ReadDword(12));
            Assert.Equal((byte)0x77, row.ReadByte(0x17));
            Assert.Equal((byte)0, row.ReadByte(0x16));
            Assert.Equal((ushort)0, row.ReadWord(0x1C));
            Assert.Equal((ushort)0, row.ReadWord(0x1E));
            Assert.Equal((byte)7, row.ReadByte(0x23));
            Assert.Equal((byte)(0x60 + index), row.ReadByte(0x2E));
            Assert.Equal((byte)255, row.ReadByte(0x29));
            Assert.False(row.Known[0x2B]);
            Assert.False(row.Known[0x2C]);
            Assert.False(row.Known[0x2D]);
            Assert.False(row.Known[0x33]);
            Assert.Equal(new NativeBattleScanRect(64, 64, 128, 128).ToBytes(), row.Bytes.AsSpan(0, 8).ToArray());
            Assert.Equal(new NativeBattleScanRect(128, 128, 0, 0).ToBytes(), backend.Bytes.AsSpan(8, 8).ToArray());
            Assert.Equal(new NativeBattleScanRect(134, 134, 2, 1).ToBytes(), backend.Bytes.AsSpan(0x10, 8).ToArray());
            Assert.Equal(0x4A0C00u, backend.ReadDword(0));
            Assert.Equal(0x4A09A0u, backend.ReadDword(4));
            Assert.Equal((byte)1, backend.ReadByte(0x34));
            Assert.Equal((byte)0, backend.ReadByte(0x35));
            Assert.Equal((byte)6, backend.ReadByte(0x37));
            Assert.Equal((byte)0x0E, backend.ReadByte(0x38));
            Assert.Equal(index, backend.ReadByte(0x3A));
            Assert.Equal(index, backend.ReadByte(0x3B));
            Assert.Equal((ushort)4096, backend.ReadWord(0x3C));
            Assert.All(backend.Known.Skip(0x18).Take(24), known => Assert.False(known));
        }
    }

    [Fact]
    public void Text_only_initialization_preserves_current_backend_index_and_fails_missing_retained_flag_byte()
    {
        NativeBattleScanTextWindows windows = Windows();
        windows = windows with { BackendRows = windows.BackendRows.SetItem(3, windows.Backend(3).WriteByte(0x3B, 93)) };
        NativeBattleScanTextInitializationResult result = NativeBattleScanTextInitialization.InitializeTextStores4A0880(windows, Unknown(12));
        Assert.Equal((byte)93, result.Windows.Backend(3).ReadByte(0x3B));
        Assert.False(result.Windows.Backend(3).Known[0x36]);
        windows = windows with { TextRows = windows.TextRows.SetItem(3, Unknown(60)) };
        Assert.Throws<NotSupportedException>(() => NativeBattleScanTextInitialization.InitializeTextStores4A0880(windows, Unknown(12)));
    }

    [Fact]
    public void Warm_startup_store_results_survive_independent_serialization()
    {
        NativeBattleScanTextWindows windows = Windows();
        NativeBattleScanTextWindows restored = JsonSerializer.Deserialize<NativeBattleScanTextWindows>(JsonSerializer.Serialize(windows))!;
        NativeBattleScanTextInitializationResult first = NativeBattleScanTextInitialization.InitializeTextStores4A0880(NativeBattleScanTextInitialization.InitializeBackendStores4B7170(windows), Unknown(12));
        NativeBattleScanTextInitializationResult second = NativeBattleScanTextInitialization.InitializeTextStores4A0880(NativeBattleScanTextInitialization.InitializeBackendStores4B7170(restored), Unknown(12));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    private static NativeBattleScanTextWindows Windows()
    {
        return new(Enumerable.Range(0, 8).Select(index => Unknown(60).WriteByte(0x2E, (byte)(0xE0 + index))).ToImmutableArray(), Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, _ => Unknown(64)));
    }

    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), new bool[length].ToImmutableArray());
}
