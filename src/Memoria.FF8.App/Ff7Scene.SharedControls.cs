using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private KeyboardInput sharedInput = new();
    private GameplayClock sharedClock = new();
    private InputFrame semanticInput;
    private bool userPaused;

    public bool IsMenuPresentation => IsTitle || debugMenu || namingMenus?.IsOpen == true;
    public GameUi.UiPreferences PresentationPreferences => dialoguePreferences?.Invoke() ?? new GameUi.UiPreferences();
    public int PresentationFrameLimit => PresentationTiming.FrameLimit(PresentationPreferences, IsMenuPresentation, false);

    public void ConfigureSharedControls(KeyboardInput input, GameplayClock clock)
    {
        sharedInput = input ?? throw new ArgumentNullException(nameof(input));
        sharedClock = clock ?? throw new ArgumentNullException(nameof(clock));
        ConfigureButtonPrompts(buttons => sharedInput.NativeButtonBindingPrompt(Ff7ControlButtons.ToShared(buttons)));
    }

    private void SampleSharedInput(KeyboardState keys, GamePadState pad)
    {
        sharedInput.NativeButtonsOwnInput = false;
        sharedInput.TextEntryEnabled = error is null && namingMenus?.IsTypingName == true;
        sharedInput.NamingNavigationEnabled = error is null && namingMenus?.IsOpen == true;
        semanticInput = sharedInput.Read(keys, pad, !focusPaused);
        if (sharedInput.PauseRequested)
        {
            userPaused = !userPaused;
            SetAudioPaused(userPaused);
            if (userPaused)
            {
                movie?.Suspend();
            }
            else
            {
                movie?.Resume();
            }
        }
        if (sharedInput.SpeedToggleRequested && namingMenus?.IsOpen != true)
        {
            sharedClock.ToggleSpeed();
        }
        if (dialoguePreferences?.Invoke() is { } preferences)
        {
            sharedClock.Configure(preferences.FastForwardMultiplier, preferences.BattleSpeedPercent);
            Session?.ConfigureMovementSpeed(preferences.FieldMovementSpeedPercent);
        }
    }
}

internal static class Ff7ControlButtons
{
    public static ushort FromShared(ushort buttons)
    {
        // The host actions retain their configured bindings. The two games assign
        // confirm, cancel and menu to different native field button slots.
        return (ushort)((buttons & ~0x70) | ((buttons & 0x40) != 0 ? 0x20 : 0) | ((buttons & 0x10) != 0 ? 0x40 : 0) | ((buttons & 0x20) != 0 ? 0x10 : 0));
    }

    public static ushort ToShared(ushort buttons)
    {
        return (ushort)((buttons & ~0x70) | ((buttons & 0x20) != 0 ? 0x40 : 0) | ((buttons & 0x40) != 0 ? 0x10 : 0) | ((buttons & 0x10) != 0 ? 0x20 : 0));
    }

    public static bool Run(ushort sharedButtons) => (sharedButtons & 0x10) == 0;
}
