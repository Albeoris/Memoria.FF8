using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanPresentationMemory([property: JsonRequired] NativeBattleScanByteOwner Camera269A168, [property: JsonRequired] NativeBattleScanByteOwner Selection269A1BC, [property: JsonRequired] NativeBattleScanByteOwner SavedFlags269A1D8, [property: JsonRequired] NativeBattleScanByteOwner SavedColors269CE34, [property: JsonRequired] NativeBattleScanByteOwner Name269AA78, [property: JsonRequired] ushort Highlight269AA90, [property: JsonRequired] ushort Result269D938, [property: JsonRequired] ushort Exit269CE2C, [property: JsonRequired] uint PrimaryModel269CE30)
{
    public static NativeBattleScanPresentationMemory FromColdProcess() => new(Known(0x50), Known(28), Known(28), Known(28), Known(24), 0, 0, 0, 0);

    public void Validate()
    {
        Camera269A168.RequireLength(0x50);
        Selection269A1BC.RequireLength(28);
        SavedFlags269A1D8.RequireLength(28);
        SavedColors269CE34.RequireLength(28);
        Name269AA78.RequireLength(24);
    }

    private static NativeBattleScanByteOwner Known(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
}
