using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The expanded text bytes around the two bytes canonically owned by UiServices.Flags942.</summary>
public sealed record NativeBattleScanExpandedStorage([property: JsonRequired] NativeBattleScanByteOwner Prefix, [property: JsonRequired] NativeBattleScanByteOwner Suffix)
{
    public NativeBattleScanByteOwner Materialize(ushort flags942)
    {
        Prefix.RequireLength(0x2E);
        Suffix.RequireLength(0x50);
        return new(Prefix.Bytes.Add(unchecked((byte)flags942)).Add((byte)(flags942 >> 8)).AddRange(Suffix.Bytes), Prefix.Known.Add(true).Add(true).AddRange(Suffix.Known));
    }

    public static NativeBattleScanExpandedStorage Capture(NativeBattleScanByteOwner context, out ushort flags942)
    {
        context.RequireLength(128);
        flags942 = context.ReadWord(0x2E);
        return new(new(context.Bytes.Slice(0, 0x2E), context.Known.Slice(0, 0x2E)), new(context.Bytes.Slice(0x30, 0x50), context.Known.Slice(0x30, 0x50)));
    }
}

public sealed record NativeBattleScanTextProcess([property: JsonRequired] NativeBattleScanTextWindows Windows, [property: JsonRequired] NativeBattleScanExpandedStorage Expanded, [property: JsonRequired] byte Formatting1D7660C, [property: JsonRequired] sbyte SelectedWindow1D2B51C, [property: JsonRequired] NativeBattleScanByteOwner Globals1D2B524, [property: JsonRequired] uint CurrentContext1D76608, [property: JsonRequired] bool Initialized)
{
    public static NativeBattleScanTextProcess FromColdProcess() => new(new(Enumerable.Range(0, 8).Select(_ => Known(60)).ToImmutableArray(), Enumerable.Range(0, 8).ToImmutableDictionary(index => (byte)index, _ => Known(64))), new(Known(0x2E), Known(0x50)), 0, 0, Known(12), 0, false);

    public NativeBattleScanTextFrame ReadFrame(ushort flags942) => new(Windows, Expanded.Materialize(flags942), Formatting1D7660C, SelectedWindow1D2B51C, Globals1D2B524);

    public NativeBattleScanTextProcess CaptureFrame(NativeBattleScanTextFrame frame, out ushort flags942)
    {
        NativeBattleScanExpandedStorage expanded = NativeBattleScanExpandedStorage.Capture(frame.ExpandedContext128, out flags942);
        return this with { Windows = frame.Windows, Expanded = expanded, Formatting1D7660C = frame.Formatting1D7660C, SelectedWindow1D2B51C = frame.SelectedWindow1D2B51C, Globals1D2B524 = frame.Globals1D2B524 };
    }

    public void Validate()
    {
        if (Windows is null || Windows.TextRows.IsDefault || Windows.TextRows.Length != 8 || Windows.BackendRows is null || Windows.BackendRows.Count != 8 || Windows.BackendRows.Keys.Any(handle => handle >= 8))
        {
            throw new InvalidDataException("Native Scan text requires its eight physical text rows and backend handles.");
        }
        foreach (NativeBattleScanByteOwner row in Windows.TextRows)
        {
            row.RequireLength(60);
        }
        foreach (NativeBattleScanByteOwner row in Windows.BackendRows.Values)
        {
            row.RequireLength(64);
        }
        Expanded.Prefix.RequireLength(0x2E);
        Expanded.Suffix.RequireLength(0x50);
        Globals1D2B524.RequireLength(12);
    }

    private static NativeBattleScanByteOwner Known(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
}
