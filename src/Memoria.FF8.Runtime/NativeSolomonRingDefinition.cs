using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original mthomas.bin ingredient pairs checked by4F8B16 and consumed by4FA3FB.</summary>
public sealed record NativeSolomonRingDefinition(string ExecutableHash, string ResourceHash, byte ItemId, ImmutableArray<NativeItemStock> Ingredients)
{
    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || ResourceHash != "CD2C7261EE28D2D6D13DB5922D447FA49D337B07A706BC042D3803E562F62BDD" || ItemId == 0 || Ingredients.IsDefault || Ingredients.Length != 3 || Ingredients.Any(stock => stock is null || stock.ItemId == 0 || stock.Count == 0) || Ingredients.Select(stock => stock.ItemId).Distinct().Count() != Ingredients.Length)
        {
            throw new InvalidDataException("Solomon Ring requires the original mthomas ingredient table and selected native item identity.");
        }
    }

    public (NativeCharacterVitals Vitals, NativeEconomy Economy, bool Changed) Use(NativeCharacterVitals vitals, NativeEconomy economy, int itemId)
    {
        Validate();
        vitals.Validate();
        economy.Validate();
        if (itemId != ItemId || economy.InitOutSha256 != vitals.InitOutSha256)
        {
            throw new InvalidDataException("Solomon Ring input disagrees with its original item or saved owner provenance.");
        }
        int ringSlot = Array.FindIndex(economy.Items, stock => stock.ItemId == ItemId && stock.Count > 0);
        if (ringSlot < 0)
        {
            throw new InvalidOperationException("The selected Solomon Ring stock is exhausted.");
        }
        //4F8B16 first refuses an already acquired Doomtrain; no ingredient or ring is spent.
        if ((vitals.GuardianForces[11].Flags & 1) != 0 || Ingredients.Any(ingredient => (economy.Items.FirstOrDefault(stock => stock.ItemId == ingredient.ItemId)?.Count ?? 0) < ingredient.Count))
        {
            return (vitals, economy, false);
        }
        NativeEconomy inventory = economy.DeepCopy();
        foreach (NativeItemStock ingredient in Ingredients.Append(new NativeItemStock(ItemId, 1)))
        {
            int slot = Array.FindIndex(inventory.Items, stock => stock.ItemId == ingredient.ItemId);
            int remaining = inventory.Items[slot].Count - ingredient.Count;
            inventory.Items[slot] = remaining == 0 ? new NativeItemStock(0, 0) : inventory.Items[slot] with { Count = checked((byte)remaining) };
        }
        //4FA5E6 uses the same47E480 acquisition contract as field GETGF.
        return (vitals.AcquireGuardianForce(11), inventory, true);
    }
}
