using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int selectedNativeMenuCharacter;

    private void RegisterNativeCharacterMenus(MenuController menu)
    {
        AddCharacterMenu("ff7-magic", "Magic", "View spell costs or choose a recovery spell.", (_, character) => ReadNativeFieldMagic(character));
        AddCharacterMenu("ff7-equip", "Equip", "Choose a slot and equip gear from inventory.", ReadNativeEquipment);
        RegisterNativeEquipmentMenu(menu);
        AddCharacterMenu("ff7-order", "Order", "Choose the first party member.", ReadNativeOrder, includeGuests: true);
        AddCharacterMenu("ff7-limit", "Limit", "View learned Limits or change the active Limit level.", ReadNativeLimit);
        RegisterNativeLimitMenu(menu);
        menu.Register(new()
        {
            Id = "ff7-phs",
            Title = "PHS",
            Help = "Choose the active member to replace.",
            Entries = controller => controller.Data.Characters.Where(character => character.InParty).Select(character =>
            {
                bool locked = IsPartyMemberLocked(NativeMenuCharacterId(character.Id));
                return new MenuEntry(character.Id, character.Name, locked ? "Locked by the current story." : "Choose a reserve member next.", Action: new("ff7-character-menu-select", character.Id, Value: "ff7-phs-reserve"), Enabled: !locked);
            }).ToArray()
        });
        menu.Register(new()
        {
            Id = "ff7-phs-reserve",
            Title = "PHS",
            Help = "Choose a reserve member.",
            Entries = controller => controller.Data.Characters.Where(character => !character.InParty).Select(character =>
            {
                int id = NativeMenuCharacterId(character.Id);
                bool locked = IsPartyMemberLocked(id);
                bool duplicate = ConflictsWithActiveGuest(id);
                string help = locked ? "Locked by the current story." : duplicate ? "This FFVIII guest already occupies another party slot." : "Bring this member into the active party.";
                return new MenuEntry(character.Id, character.Name, help, Action: new("ff7-phs-apply", character.Id), Enabled: !locked && !duplicate);
            }).ToArray()
        });

        void AddCharacterMenu(string id, string title, string help, Func<MenuController, Ff7CharacterState, IReadOnlyList<MenuEntry>> details, bool includeGuests = false)
        {
            string destination = id + "-character";
            menu.Register(new()
            {
                Id = id,
                Title = title,
                Help = "Choose a party member.",
                Entries = controller => controller.Data.Characters.Where(character => character.InParty).Select(character =>
                {
                    bool guest = IsGuestStoryCharacter(NativeMenuCharacterId(character.Id));
                    return new MenuEntry(character.Id, character.Name, guest && !includeGuests ? "Use the Final Fantasy VIII page for this guest." : help, Action: new("ff7-character-menu-select", character.Id, Value: destination), Enabled: includeGuests || !guest);
                }).ToArray()
            });
            menu.Register(new()
            {
                Id = destination,
                Title = title,
                Help = help,
                Entries = controller => details(controller, Session!.Progress.Characters.Single(character => character.Id == selectedNativeMenuCharacter))
            });
        }
    }

    private IReadOnlyList<MenuEntry> ReadNativeOrder(MenuController menu, Ff7CharacterState first)
    {
        return menu.Data.Characters.Where(character => character.InParty).Select(character =>
        {
            int id = NativeMenuCharacterId(character.Id);
            bool same = id == first.Id;
            bool guest = IsGuestStoryCharacter(id);
            string row = guest ? "FFVIII guest" : (Session!.Progress.Characters.Single(candidate => candidate.Id == id).NativeRecord[32] & 1) == 0 ? "Back" : "Front";
            string help = !same ? "Swap the two party slots." : guest ? "This guest does not have an FFVII battle row." : "Switch this character's front/back row.";
            return new MenuEntry(character.Id, $"{character.Name}   {row}", help, Action: new("ff7-order-apply", character.Id), Enabled: !same || !guest);
        }).ToArray();
    }

    private bool IsGuestStoryCharacter(int id) => Session?.GuestFieldVitals?.StoryCharacters.Contains(id) == true;

    private bool IsPartyMemberLocked(int id) => (Session!.Progress.LockedCharacters & (1 << id)) != 0;

    private bool ConflictsWithActiveGuest(int reserve)
    {
        CharacterAppearanceMapping? mapping = envelope.Mappings.SingleOrDefault(candidate => candidate.StoryCharacter == reserve);
        return mapping is not null && envelope.Ff8Payload is { Length: > 0 } && envelope.Mappings.Any(candidate => candidate.StoryCharacter != selectedNativeMenuCharacter && Session!.Progress.Party.Contains(candidate.StoryCharacter) && candidate.AppearanceCharacter == mapping.AppearanceCharacter);
    }

    private static int NativeMenuCharacterId(string id) => int.Parse(id.AsSpan("ff7-character/".Length), CultureInfo.InvariantCulture);

    private bool ApplyNativeCharacterMenuAction(MenuAction action)
    {
        if (action.Kind == "ff7-character-menu-select")
        {
            selectedNativeMenuCharacter = NativeMenuCharacterId(action.SubjectId);
            playerMenu!.Push(action.Value);
            return true;
        }
        if (action.Kind == "ff7-order-apply")
        {
            if (Session!.TryChangeMenuOrder(selectedNativeMenuCharacter, NativeMenuCharacterId(action.SubjectId)))
            {
                playerMenu!.Back();
            }
            return true;
        }
        if (action.Kind == "ff7-phs-apply")
        {
            int reserve = NativeMenuCharacterId(action.SubjectId);
            if (!ConflictsWithActiveGuest(reserve) && Session!.TryReplaceMenuPartyMember(selectedNativeMenuCharacter, reserve))
            {
                Session.PublishNativeMenuPartyStatistics();
                playerMenu!.Back();
            }
            return true;
        }
        return ApplyNativeEquipmentMenuAction(action) || ApplyNativeLimitMenuAction(action);
    }
}
