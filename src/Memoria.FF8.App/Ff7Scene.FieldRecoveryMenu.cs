using System.Buffers.Binary;
using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int nativeRecoveryId;

    private void RegisterNativeFieldRecoveryMenus(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "ff7-items",
            Title = "Item",
            Help = "Choose an item to use, or browse the inventory.",
            Entries = ReadNativeFieldItems,
            Details = controller => [controller.SelectedEntry?.Help ?? ""]
        });
        menu.Register(new()
        {
            Id = "ff7-item-target",
            Title = "Item",
            Help = "Choose a party member.",
            Entries = controller => ReadNativeFieldRecoveryTargets(controller, magic: false)
        });
        menu.Register(new()
        {
            Id = "ff7-magic-target",
            Title = "Magic",
            Help = "Choose a party member.",
            Entries = controller => ReadNativeFieldRecoveryTargets(controller, magic: true)
        });
    }

    private IReadOnlyList<MenuEntry> ReadNativeFieldItems(MenuController menu)
    {
        Ff7Session session = Session!;
        return menu.Data.Items.Select(item =>
        {
            int itemId = int.Parse(item.Id.AsSpan("ff7-item/".Length), CultureInfo.InvariantCulture);
            if (!session.SupportsFieldItem(itemId))
            {
                return new MenuEntry(item.Id, $"{item.Name}   {item.Quantity}", item.Description);
            }

            bool usable = session.Progress.Party.Any(target => session.CanUseFieldItem(itemId, target));
            string help = usable ? item.Description : $"{item.Description} No eligible party member.";
            return new MenuEntry(item.Id, $"{item.Name}   {item.Quantity}", help, Action: new("ff7-field-item-select", item.Id), Enabled: usable);
        }).ToArray();
    }

    private IReadOnlyList<MenuEntry> ReadNativeFieldMagic(Ff7CharacterState character)
    {
        Ff7Session session = Session!;
        return session.ReadFieldMagic(character.Id).Select(spell =>
        {
            string id = $"spell-{spell.AttackId}";
            string label = $"{BattleSpellName(spell.AttackId)}   {spell.Attack.CastingCost} MP";
            string help = $"Current MP {character.MagicPoints}";
            if (!Ff7NativeFieldRecovery.SupportsMagic(spell.AttackId))
            {
                return new MenuEntry(id, label, help);
            }

            bool usable = session.Progress.Party.Any(target => session.CanCastFieldMagic(character.Id, spell, target));
            if (!usable)
            {
                help += character.MagicPoints < spell.Attack.CastingCost ? "   Not enough MP." : "   No eligible party member.";
            }
            return new MenuEntry(id, label, help, Action: new("ff7-field-magic-select", id), Enabled: usable);
        }).ToArray();
    }

    private IReadOnlyList<MenuEntry> ReadNativeFieldRecoveryTargets(MenuController menu, bool magic)
    {
        Ff7Session session = Session!;
        Ff7BattleSpell? spell = magic ? session.ReadFieldMagic(selectedNativeMenuCharacter).SingleOrDefault(candidate => candidate.AttackId == nativeRecoveryId) : null;
        return menu.Data.Characters.Where(character => character.InParty).Select(character =>
        {
            int id = NativeMenuCharacterId(character.Id);
            if (IsGuestStoryCharacter(id))
            {
                return new MenuEntry(character.Id, character.Name, "Use the Final Fantasy VIII page for this guest.", Enabled: false);
            }

            Ff7CharacterState target = session.Progress.Characters.Single(candidate => candidate.Id == id);
            ushort maxMp = BinaryPrimitives.ReadUInt16LittleEndian(target.NativeRecord.AsSpan(58));
            bool usable = magic ? spell is not null && session.CanCastFieldMagic(selectedNativeMenuCharacter, spell, id) : session.CanUseFieldItem(nativeRecoveryId, id);
            string help = usable
                ? magic ? BattleSpellName(nativeRecoveryId) : menuText!.ItemNames[nativeRecoveryId]
                : "Cannot use this on this party member.";
            string label = $"{character.Name}   HP {character.CurrentHp}/{character.MaximumHp}   MP {target.MagicPoints}/{maxMp}";
            return new MenuEntry(character.Id, label, help, Action: new("ff7-field-recovery-apply", TargetId: character.Id), Enabled: usable);
        }).ToArray();
    }

    private bool ApplyNativeFieldRecoveryAction(MenuAction action)
    {
        Ff7Session session = Session!;
        MenuController menu = playerMenu!;
        switch (action.Kind)
        {
            case "ff7-field-item-select":
                nativeRecoveryId = int.Parse(action.SubjectId.AsSpan("ff7-item/".Length), CultureInfo.InvariantCulture);
                if (session.Progress.Party.Any(target => session.CanUseFieldItem(nativeRecoveryId, target)))
                {
                    menu.Push("ff7-item-target");
                }
                return true;
            case "ff7-field-magic-select":
                nativeRecoveryId = int.Parse(action.SubjectId.AsSpan("spell-".Length), CultureInfo.InvariantCulture);
                Ff7BattleSpell? spell = session.ReadFieldMagic(selectedNativeMenuCharacter).SingleOrDefault(candidate => candidate.AttackId == nativeRecoveryId);
                if (spell is not null && session.Progress.Party.Any(target => session.CanCastFieldMagic(selectedNativeMenuCharacter, spell, target)))
                {
                    menu.Push("ff7-magic-target");
                }
                return true;
            case "ff7-field-recovery-apply":
                int targetId = NativeMenuCharacterId(action.TargetId);
                if (menu.ScreenId == "ff7-item-target" && session.TryUseFieldItem(nativeRecoveryId, targetId))
                {
                    menu.InvalidateLiveData();
                    if (!session.Progress.Inventory.Any(slot => slot.ItemId == nativeRecoveryId && slot.Quantity > 0))
                    {
                        menu.Back();
                    }
                }
                else if (menu.ScreenId == "ff7-magic-target" && session.TryCastFieldMagic(selectedNativeMenuCharacter, nativeRecoveryId, targetId))
                {
                    menu.InvalidateLiveData();
                }
                return true;
            default:
                return false;
        }
    }
}
