using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

/// <summary>Projects the actual4EA770 recipe list and explicit selection/remodel requests.</summary>
public static class NativeWeaponShopMenuProjection
{
    public static MenuData Build(NativeWeaponShopTransaction shop, NativeCharacterVitals? vitals = null)
    {
        ArgumentNullException.ThrowIfNull(shop);
        shop.Validate();
        MenuData data = new() { Location = "Junk Shop", Currency = checked((int)shop.Gil), SourceDescription = "Native weapon shop" };
        for (int character = 0; character < 6; character++)
        {
            if ((shop.EligibleMask & 1 << character) != 0)
            {
                string id = $"character-{character}";
                data.Characters.Add(new MenuCharacter { Id = id, NativeId = character, Name = CharacterName(character), InParty = character == shop.SelectedCharacter });
                data.SupportedActions.Add($"weapon-character:{id}");
            }
        }
        foreach (byte offer in shop.AvailableRecipes)
        {
            int id = offer & 0x3F;
            NativeWeaponRecipe recipe = shop.Definitions.Recipes[id];
            string materials = string.Join(", ", recipe.Materials.Where(material => material.ItemId != 0).Select(material => $"Item {material.ItemId}: {shop.Counts[material.ItemId]}/{material.Count}"));
            string availability = (offer & 0x80) != 0 ? "Materials and gil available" : "Materials or gil missing";
            string itemId = $"weapon-{id}";
            data.Items.Add(new MenuItem { Id = itemId, Name = recipe.Name, Price = checked((int)shop.Price(id)), Description = $"{availability}. {materials}." });
            if ((offer & 0x80) != 0 && vitals is not null && vitals.Records[shop.SelectedCharacter].Weapon != id)
            {
                data.SupportedActions.Add($"weapon-remodel:{itemId}");
            }
        }
        return data;
    }

    public static PlayerAction PlayerAction(MenuAction action)
    {
        string prefix = action.Kind == "weapon-character" ? "character-" : action.Kind == "weapon-remodel" ? "weapon-" : throw new ArgumentException("Unsupported weapon shop action.", nameof(action));
        if (!action.SubjectId.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(action.SubjectId.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id))
        {
            throw new ArgumentException("Weapon shop action requires an exact native row.", nameof(action));
        }
        return new PlayerAction(action.Kind == "weapon-character" ? PlayerActionKind.SelectWeaponShopCharacter : PlayerActionKind.RemodelWeapon, id);
    }

    private static string CharacterName(int character) => character switch { 0 => "Squall", 1 => "Zell", 2 => "Irvine", 3 => "Quistis", 4 => "Rinoa", 5 => "Selphie", _ => throw new ArgumentOutOfRangeException(nameof(character)) };
}
