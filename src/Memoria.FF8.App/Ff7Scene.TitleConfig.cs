using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MenuController? titleConfigMenu;
    private bool titleConfigPointerPressed;
    private static IEnumerable<string> TitlePreferenceKeys => PlayerPreferenceKeys.Except(["Battle ATB mode", "Battle policy", "Random encounters"]);

    private void OpenTitleConfig()
    {
        if (titleConfigMenu is null)
        {
            titleConfigMenu = new(ReadTitleConfigData, ApplyTitleConfigAction);
            titleConfigMenu.ConnectPreferences(() => PresentationPreferences, ApplyTitleConfigAction);
            ConnectMenuControls(titleConfigMenu);
            titleConfigMenu.Register(new()
            {
                Id = "config",
                Title = "Config",
                Help = "Shared presentation, control and autosave preferences.",
                Entries = menu => [.. menu.ConfigEntries().Where(entry => entry.Id is "config-graphics" or "config-audio" or "config-interface" or "config-controls"), new("config-speed", "Speed", "Movement and fast-forward preferences.", "config-speed"), new("config-exploration", "Exploration and saving", "Automatic save preferences.", "config-exploration")]
            });
        }
        titleConfigMenu.Presentation = PresentationPreferences.MenuPresentation;
        titleConfigMenu.Open("config");
        sharedInput.SuppressHeldUiInput();
        titleConfigPointerPressed = pointerState.LeftButton == ButtonState.Pressed;
    }

    private MenuData ReadTitleConfigData()
    {
        MenuData data = new();
        titleConfigMenu!.PopulateHostSettingValues(data, TitlePreferenceKeys);
        return data;
    }

    private void ApplyTitleConfigAction(MenuAction action)
    {
        if (action.Kind != "config" || !TitlePreferenceKeys.Contains(action.SubjectId))
        {
            throw new NotSupportedException("This setting is unavailable in the FFVII title menu.");
        }
        UiPreferences current = dialoguePreferences?.Invoke() ?? new UiPreferences();
        UiPreferences updated = current.WithSetting(action.SubjectId, action.Value);
        if (updated != current)
        {
            (setDialoguePreferences ?? throw new InvalidOperationException("Shared preferences are read-only in this host."))(updated);
            titleConfigMenu!.Presentation = updated.MenuPresentation;
        }
    }

    private bool UpdateTitleConfig(KeyboardState keys, GamePadState pad)
    {
        if (titleConfigMenu?.IsOpen != true)
        {
            return false;
        }
        UpdateSharedMenuInput(titleConfigMenu, keys, pad, ref titleConfigPointerPressed);
        if (!titleConfigMenu.IsOpen)
        {
            sharedInput.SuppressHeldUiInput();
        }
        return true;
    }
}
