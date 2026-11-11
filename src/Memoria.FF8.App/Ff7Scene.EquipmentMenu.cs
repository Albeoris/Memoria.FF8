using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7EquipmentSlot selectedNativeEquipmentSlot;

    private void RegisterNativeEquipmentMenu(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "ff7-equip-choice",
            Title = "Equip",
            Help = "Choose gear from inventory.",
            Entries = _ => ReadNativeEquipmentChoices(),
            Details = controller => [controller.SelectedEntry?.Help ?? ""]
        });
    }

    private IReadOnlyList<MenuEntry> ReadNativeEquipment(MenuController _, Ff7CharacterState character)
    {
        return Enum.GetValues<Ff7EquipmentSlot>().Select(slot =>
        {
            int? equipped = Session!.EquippedMenuItem(character, slot);
            string name = equipped is int item ? menuText!.ItemNames.GetValueOrDefault(item, "Unavailable") : "None";
            string help = equipped is int id ? menuText!.ItemDescriptions.GetValueOrDefault(id, "Equipment description unavailable.") : "No gear equipped.";
            string label = slot.ToString();
            return new MenuEntry(label, $"{label}   {name}", help, Action: new("ff7-equip-slot", label));
        }).ToArray();
    }

    private IReadOnlyList<MenuEntry> ReadNativeEquipmentChoices()
    {
        IReadOnlyList<Ff7EquipmentChoice> choices = Session!.MenuEquipmentChoices(selectedNativeMenuCharacter, selectedNativeEquipmentSlot);
        if (choices.Count == 0)
        {
            return [new("empty", "No compatible gear", "This character has no compatible gear in inventory for this slot.", Enabled: false)];
        }
        return choices.Select(choice =>
        {
            if (choice.ItemId is not int item)
            {
                return new MenuEntry("remove", "Remove accessory", choice.UnavailableReason ?? "Return the equipped accessory to inventory.", Action: new("ff7-equip-apply"), Enabled: choice.UnavailableReason is null);
            }
            string name = menuText!.ItemNames.GetValueOrDefault(item, "Unavailable");
            string help = menuText.ItemDescriptions.GetValueOrDefault(item, "Equipment description unavailable.");
            if (choice.UnavailableReason is { } reason)
            {
                help = $"{help} {reason}";
            }
            string id = item.ToString(CultureInfo.InvariantCulture);
            return new MenuEntry(id, $"{name}   {choice.Quantity}", help, Action: new("ff7-equip-apply", id), Enabled: choice.UnavailableReason is null);
        }).ToArray();
    }

    private bool ApplyNativeEquipmentMenuAction(MenuAction action)
    {
        switch (action.Kind)
        {
            case "ff7-equip-slot":
                selectedNativeEquipmentSlot = Enum.Parse<Ff7EquipmentSlot>(action.SubjectId);
                playerMenu!.Push("ff7-equip-choice");
                return true;
            case "ff7-equip-apply":
                int? item = action.SubjectId.Length == 0 ? null : int.Parse(action.SubjectId, CultureInfo.InvariantCulture);
                if (Session!.TryEquipMenuItem(selectedNativeMenuCharacter, selectedNativeEquipmentSlot, item))
                {
                    playerMenu!.InvalidateLiveData();
                    playerMenu.Back();
                }
                return true;
            default:
                return false;
        }
    }
}
