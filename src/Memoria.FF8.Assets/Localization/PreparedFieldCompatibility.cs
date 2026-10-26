namespace Memoria.FF8.Assets.Localization;

/// <summary>An owned source snapshot and its immutable mechanical inventory, reusable across language comparisons.</summary>
public sealed class PreparedFieldCompatibility
{
    internal PreparedFieldCompatibility(FieldCompatibilityInput input)
    {
        Input = input with
        {
            Jsm = input.Jsm?.ToArray(),
            Bank = input.Bank?.Select(row => row with { Tokens = row.Tokens?.ToArray() }).ToArray(),
            Errors = input.Errors?.ToArray()
        };
        Inventory = FieldCompatibilityAudit.Inspect(Input);
        Projection = FieldCompatibilityProjection.Create(Inventory);
    }

    internal FieldCompatibilityInput Input { get; }
    internal FieldCompatibilityProjection Projection { get; }
    public FieldCompatibilityInventory Inventory { get; }
}
