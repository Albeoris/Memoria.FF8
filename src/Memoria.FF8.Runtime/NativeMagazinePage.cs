namespace Memoria.FF8.Runtime;

/// <summary>One original68-byte mmag page's text and saved-learning selectors.</summary>
public sealed record NativeMagazinePage(int ItemId, byte PageId, byte WeaponRecipe, byte DuelMove, byte PetPalsIssue, string[] Text)
{
    public (NativeWeaponRecipes? Recipes, NativeLimitProgress? Limits) View(NativeEconomy economy, NativeWeaponRecipes? recipes, NativeLimitProgress? limits)
    {
        economy.Validate();
        if (ItemId is < 1 or > 198 || Text is null || WeaponRecipe != 255 && WeaponRecipe >= 28 || DuelMove != 255 && DuelMove >= 16 || PetPalsIssue != 255 && PetPalsIssue >= 8)
        {
            throw new InvalidDataException("Magazine page lacks its original bounded learning selectors.");
        }
        if (!economy.Items.Any(item => item.ItemId == ItemId && item.Count != 0))
        {
            throw new InvalidOperationException("Magazine viewing requires an owned nonzero original item.");
        }
        if (WeaponRecipe != 255)
        {
            recipes = recipes ?? throw new NotSupportedException("Magazine weapon learning requires its actual saved recipe DWORD.");
            recipes.Validate();
            if (recipes.InitOutSha256 != economy.InitOutSha256)
            {
                throw new InvalidDataException("Magazine recipe owner changed its saved provenance.");
            }
            recipes = recipes with { Bits = recipes.Bits | 1u << WeaponRecipe };
        }
        if (DuelMove != 255 || PetPalsIssue != 255)
        {
            limits = limits ?? throw new NotSupportedException("Magazine learning requires its actual saved Limit progression.");
            limits.Validate();
            if (limits.InitOutSha256 != economy.InitOutSha256)
            {
                throw new InvalidDataException("Magazine Limit owner changed its saved provenance.");
            }
            if (DuelMove != 255)
            {
                limits = limits with { DuelFlags = (ushort)(limits.DuelFlags | 1 << DuelMove) };
            }
            if (PetPalsIssue != 255)
            {
                byte prior = limits.PetPalsFlags ?? throw new NotSupportedException("Pet Pals viewing requires saved BYTE1CFE773; explicitly migrate this save from actual evidence.");
                limits = limits with { PetPalsFlags = (byte)(prior | 1 << PetPalsIssue) };
            }
        }
        return (recipes, limits);
    }
}
