using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Read-only initial4EA770 recipe list; remodeling is an unsupported action.</summary>
public static class NativeWeaponShopMenuProjection
{
    public static MenuData Build(NativeWeaponShopTransaction shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        shop.Validate();
        MenuData data = new() { Location = "Junk Shop", Currency = checked((int)shop.Gil), SourceDescription = "Native weapon shop", UnavailableReason = "Weapon remodeling is unsupported." };
        foreach (byte offer in shop.AvailableRecipes)
        {
            int id = offer & 0x3F;
            NativeWeaponRecipe recipe = shop.Definitions.Recipes[id];
            string materials = string.Join(", ", recipe.Materials.Where(material => material.ItemId != 0).Select(material => $"Item {material.ItemId}: {shop.Counts[material.ItemId]}/{material.Count}"));
            string availability = (offer & 0x80) != 0 ? "Materials and gil available" : "Materials or gil missing";
            data.Items.Add(new MenuItem { Id = $"weapon-{id}", Name = recipe.Name, Price = checked((int)shop.Price(id)), Description = $"{availability}. {materials}. Remodeling is unsupported." });
        }
        return data;
    }
}
