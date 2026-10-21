using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private void ConfigureCommandColumns(UiList list)
    {
        if (CurrentScreen.Layout == MenuLayout.BattleHud)
        {
            return;
        }
        IReadOnlyList<MenuEntry> entries = Entries;
        if (ScreenId == "junction-stock" || entries.Any(entry => entry.Action?.Kind == "select-magic"))
        {
            list.Items = entries.Select(entry => CommandMagic(entry)?.Name ?? entry.Label).ToList();
            list.SecondaryItems = entries.Select(entry => CommandMagic(entry)?.StockPresentation ?? "").ToList();
            list.Images = [];
            list.TrailingImages = entries.Select(entry => Data.SharedMagicStock ? CommandMagic(entry)?.OwnerIcon : null).ToList();
            list.TrailingImagesBeforeSecondary = true;
        }
        else if (entries.Any(entry => entry.Action?.Kind == "select-slot"))
        {
            list.Items = entries.Select(entry => entry.Action?.Kind == "select-slot" ? entry.Action.SubjectId : entry.Label).ToList();
            list.SecondaryItems = entries.Select(entry => entry.Action?.Kind == "select-slot" ? Character?.Junctions.GetValueOrDefault(entry.Action.SubjectId, "--") ?? "--" : "").ToList();
        }
        else if (ScreenId is "item-target" or "magic-target" or "magic-exchange-target")
        {
            if (ScreenId == "item-target" && Item?.NativeFieldInventoryTarget == true)
            {
                return;
            }
            list.Items = entries.Select(entry => CommandTargetCharacter(entry)?.Name ?? CommandTargetGuardianForce(entry)?.Name ?? entry.Label).ToList();
            list.SecondaryItems = entries.Select(entry => CommandTargetCharacter(entry) is { } character ? "HP " + Hp(character) : CommandTargetGuardianForce(entry) is { } force ? $"HP {Number(force.CurrentHp)} / {Number(force.MaximumHp)}" : "").ToList();
        }
        else if (ScreenId == "junction-gf")
        {
            list.Items = entries.Select(entry => Data.GuardianForces.FirstOrDefault(force => force.Id == entry.Action?.SubjectId)?.Name ?? entry.Label).ToList();
            list.SecondaryItems = [];
            list.TrailingImages = entries.Select(entry => GuardianForceOwner(entry.Action?.SubjectId ?? "")?.NativeId is { } owner ? (UiImageSource?)new UiImageSource(UiImageKind.CharacterPortrait, owner) : null).ToList();
        }
        else if (ScreenId == "ability-card-mod")
        {
            list.Items = entries.Select(entry => Data.Cards.FirstOrDefault(card => card.Id == entry.Id)?.Name ?? entry.Label).ToList();
            list.SecondaryItems = entries.Select(entry => Data.Cards.FirstOrDefault(card => card.Id == entry.Id)?.Quantity.ToString() ?? "").ToList();
        }
        else if (ScreenId == "ability-recipes")
        {
            list.Items = entries.Select(entry => Data.Recipes.FirstOrDefault(recipe => recipe.Id == entry.Id) is { } recipe ? recipe.IngredientName ?? Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Name ?? recipe.IngredientId : entry.Label).ToList();
            list.SecondaryItems = entries.Select(entry => Data.Recipes.FirstOrDefault(recipe => recipe.Id == entry.Id) is { } recipe ? $"{recipe.IngredientQuantity} → {recipe.ResultQuantity} {recipe.ResultName}" : "").ToList();
        }
        else
        {
            return;
        }
        list.AutoSecondaryWidth = true;
        list.SecondaryAlignment = UiAlignment.End;
    }

    private MenuMagic? CommandMagic(MenuEntry entry)
    {
        return entry.Action?.Kind is "select-magic" or "junction-magic" or "junction-magic-reassign" ? Data.Magic.FirstOrDefault(magic => magic.CharacterId == Character?.Id && magic.Id == entry.Action.SubjectId) : null;
    }

    private MenuCharacter? CommandTargetCharacter(MenuEntry entry)
    {
        string? id = entry.Action?.Kind switch
        {
            "select-exchange" => entry.Action.SubjectId,
            "item-use" or "magic-use" => entry.Action.TargetId,
            _ => null
        };
        return Data.Characters.FirstOrDefault(character => character.Id == id);
    }

    private MenuGuardianForce? CommandTargetGuardianForce(MenuEntry entry)
    {
        return entry.Action?.Kind == "item-use" && Item?.NativeFieldTargetsGuardianForce == true ? Data.GuardianForces.FirstOrDefault(force => $"item-target-{force.NativeId + 16}" == entry.Action.TargetId) : null;
    }
}
