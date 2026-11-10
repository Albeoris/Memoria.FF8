using System.Globalization;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int materiaCharacter;
    private int materiaEquippedSlot;
    private IReadOnlyList<string>? materiaNames;

    private void RegisterMateriaMenus(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "ff7-materia",
            Title = "Materia",
            Help = "Choose a party member.",
            Entries = controller => controller.Data.Characters.Where(character => character.InParty).Select(character => new MenuEntry(character.Id, character.Name, "View weapon and armor slots.", Action: new("ff7-materia-character", character.Id))).ToArray()
        });
        menu.Register(new()
        {
            Id = "ff7-materia-slots",
            Title = "Materia slots",
            Help = "Adjacent linked slots share support materia.",
            Entries = _ => Session!.EquippedMateria(materiaCharacter).Select(slot =>
            {
                string id = slot.Slot.ToString(CultureInfo.InvariantCulture);
                string gear = slot.Slot < 8 ? "Weapon" : "Armor";
                string link = slot.Linked ? " [linked]" : "";
                return new MenuEntry(id, $"{gear} {slot.Slot % 8 + 1}{link}   {MateriaName(slot.MateriaId)}", $"AP {slot.AbilityPoints}", Action: new("ff7-materia-slot", id));
            }).ToArray()
        });
        menu.Register(new()
        {
            Id = "ff7-materia-stock",
            Title = "Materia stock",
            Help = "Choose materia to equip in this slot.",
            Entries = _ =>
            [
                new("remove", "Remove materia", "Return the equipped materia to inventory.", Action: new("ff7-materia-equip"), Enabled: Session!.Progress.Materia.Length < 200 && Session.EquippedMateria(materiaCharacter).Single(slot => slot.Slot == materiaEquippedSlot).MateriaId != 255),
                .. Ff7NativeMateriaInventory.Normalize(Session.Progress.Materia).Select(materia =>
                {
                    string id = materia.NativeSlot!.Value.ToString(CultureInfo.InvariantCulture);
                    return new MenuEntry(id, MateriaName(materia.MateriaId), $"AP {materia.AbilityPoints}", Action: new("ff7-materia-equip", id));
                })
            ]
        });
    }

    private bool ApplyMateriaMenuAction(MenuAction action)
    {
        switch (action.Kind)
        {
            case "ff7-materia-character":
                materiaCharacter = int.Parse(action.SubjectId.AsSpan("ff7-character/".Length), CultureInfo.InvariantCulture);
                playerMenu!.Push("ff7-materia-slots");
                return true;
            case "ff7-materia-slot":
                materiaEquippedSlot = int.Parse(action.SubjectId, CultureInfo.InvariantCulture);
                playerMenu!.Push("ff7-materia-stock");
                return true;
            case "ff7-materia-equip":
                int? slot = action.SubjectId.Length == 0 ? null : int.Parse(action.SubjectId, CultureInfo.InvariantCulture);
                if (Session!.EquipMateria(materiaCharacter, materiaEquippedSlot, slot))
                {
                    playerMenu!.InvalidateLiveData();
                    playerMenu.Back();
                }
                return true;
            default:
                return false;
        }
    }

    private string MateriaName(int id)
    {
        if (id == 255)
        {
            return "Empty";
        }
        materiaNames ??= KernelTextArchive.ReadPlainStrings(KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path))[14].Span, 96);
        return materiaNames[id];
    }
}
