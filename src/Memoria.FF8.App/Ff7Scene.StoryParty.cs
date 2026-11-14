using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Globalization;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private long? storyPartyMenuRequest;
    private int[] storyPartyMembers = [];
    private int storyPartySlot;

    private void RegisterStoryPartyMenu(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "ff7-story-party",
            Title = "Reform",
            Help = "Please make a party of three.",
            Entries = controller =>
            [
                .. storyPartyMembers.Select((id, index) => new MenuEntry($"slot-{index}", controller.Data.Characters.FirstOrDefault(character => NativeMenuCharacterId(character.Id) == id)?.Name ?? "Empty slot", "", Action: new("ff7-story-party-slot", index.ToString(CultureInfo.InvariantCulture)), Enabled: id == 255 || !IsPartyMemberLocked(id))),
                new("confirm", "Confirm party", "", Action: new("ff7-story-party-confirm"), Enabled: storyPartyMembers.Count(id => id != 255) == 3)
            ]
        });
        menu.Register(new()
        {
            Id = "ff7-story-party-reserve",
            Title = "Choose party member",
            Help = "Select a member to swap with this slot.",
            Entries = controller => controller.Data.Characters.Where(character => NativeMenuCharacterId(character.Id) != storyPartyMembers[storyPartySlot]).Select(character => new MenuEntry(character.Id, character.Name, "", Action: new("ff7-story-party-member", character.Id), Enabled: !IsPartyMemberLocked(NativeMenuCharacterId(character.Id)))).ToArray()
        });
    }

    private bool ApplyStoryPartyMenuAction(MenuAction action)
    {
        switch (action.Kind)
        {
            case "ff7-story-party-slot":
                storyPartySlot = int.Parse(action.SubjectId, CultureInfo.InvariantCulture);
                playerMenu!.Push("ff7-story-party-reserve");
                return true;
            case "ff7-story-party-member":
                int member = NativeMenuCharacterId(action.SubjectId);
                int previousSlot = Array.IndexOf(storyPartyMembers, member);
                if (previousSlot >= 0)
                {
                    storyPartyMembers[previousSlot] = storyPartyMembers[storyPartySlot];
                }
                storyPartyMembers[storyPartySlot] = member;
                playerMenu!.Back();
                return true;
            case "ff7-story-party-confirm":
                if (storyPartyMenuRequest is long request && Session!.CompleteStoryPartyMenu(request, storyPartyMembers))
                {
                    storyPartyMenuRequest = null;
                    autoPlay?.ResetIdleDeadline();
                    ClosePlayerMenu();
                }
                return true;
            default:
                return false;
        }
    }

    private bool UpdateStoryPartyMenu(GameTime time, KeyboardState keys, GamePadState pad)
    {
        if (Session?.Host.Process.Menu is not { Mode: 7, Completed: false } request)
        {
            return false;
        }
        EnsurePlayerMenu();
        MenuController menu = playerMenu!;
        if (storyPartyMenuRequest != request.RequestId)
        {
            storyPartyMenuRequest = request.RequestId;
            storyPartyMembers = Session.Progress.Party.ToArray();
        }
        if (!menu.IsOpen)
        {
            menu.Open("ff7-story-party");
            sharedInput.SuppressHeldUiInput();
            menuPointerPressed = pointerState.LeftButton == ButtonState.Pressed;
        }
        else
        {
            UpdateSharedMenuInput(menu, keys, pad, ref menuPointerPressed);
        }
        AdvancePlayerMenuClock(time.ElapsedGameTime);
        RefreshPlayerMenuPresentation();
        return true;
    }
}
