namespace Memoria.FF8.Runtime;

/// <summary>The supported native New Game copy, including a bounded prefix of the saved item inventory.</summary>
public static class NativeInitialData
{
    public const int InventoryOffset = 2804;
    public const int MaximumLength = InventoryOffset + NativeEconomy.ItemSlotCount * 2;

    public static void Validate(ReadOnlySpan<byte> content)
    {
        if (content.Length < InventoryOffset || content.Length > MaximumLength || (content.Length - InventoryOffset) % 2 != 0)
        {
            throw new InvalidDataException("Native initialization requires its complete saved header and an integral item inventory prefix within the supported saved owners.");
        }
    }

    public static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
