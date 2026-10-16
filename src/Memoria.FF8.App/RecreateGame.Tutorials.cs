using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private NativeTutorialPlayer? displayedTutorial;
    private MenuController? tutorialParentMenu;
    private bool nativeTutorialBrowserOpen;
    private double tutorialSeconds;

    private void ConnectNativeTutorialUi()
    {
        using GameAssets assets = new(options.GameDirectory);
        NativeTutorialCatalog nativeTutorialCatalog = assets.ReadNativeTutorialCatalog();
        gameMenus!.ConnectNativeTutorials(nativeTutorialCatalog, assets.ReadNativeTutorialBrowserCatalog(), () => events?.Scene.UnlockedTutorials ?? new SortedSet<int>(), id =>
        {
            world ??= CreateWorld();
            world.BeginMenuTutorial(id);
        });
        gameMenus.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), () =>
        {
            NativeCharacterVitals? party = session.State.CharacterVitals?.DeepCopy();
            int level = party is null ? 1 : NativeStatDefinitions.Level(party.Records[0].Experience, party.Definitions.Characters[0]);
            int rank = session.State.SeeD is { SalaryUiEnabled: true } seeD ? seeD.RankPoints / 100 : -1;
            // Practice owns the original fresh test counter and copies display inputs; its result never writes saved rank or learning state.
            return new NativeTutorialExamProgress(0, rank, level, party);
        });
    }

    private bool UpdateNativeTutorialUi(TimeSpan elapsed, InputFrame input)
    {
        if (world is null)
        {
            return false;
        }
        if (!options.ControlStep && events is not null && (events.Scene.NativeTutorial is not null || events.Scene.NativeMenuTutorial is not null))
        {
            events.ObserveNativeMenuButtons(keyboard.HeldFieldButtons);
        }
        NativeTutorialPlayer? current = world.Tutorial;
        if (current is not null)
        {
            PresentTutorial(current);
            PlayerActionKind pressed = input.Cancel || keyboard.UiCancelRequested ? PlayerActionKind.Cancel : input.Confirm || keyboard.UiConfirmRequested ? PlayerActionKind.Confirm : PlayerActionKind.Wait;
            if (pressed != PlayerActionKind.Wait)
            {
                ApplyTutorialInput(new PlayerAction(pressed));
            }
            tutorialSeconds += options.ControlStep ? 0 : elapsed.TotalSeconds;
            while (tutorialSeconds + 1e-9 >= 1d / EventEngine.SimulationFps && displayedTutorial is not null)
            {
                tutorialSeconds -= 1d / EventEngine.SimulationFps;
                ApplyTutorialInput(new PlayerAction(PlayerActionKind.Wait));
            }
            SynchronizeUiMoviePause();
            return true;
        }
        RestoreTutorialParent();
        if (events?.Scene.NativeTutorial is { TutorialId: null })
        {
            if (!nativeTutorialBrowserOpen)
            {
                gameMenus!.Open("tutorial");
                nativeTutorialBrowserOpen = true;
            }
            else if (!gameMenus!.IsOpen)
            {
                world.Apply(new PlayerAction(PlayerActionKind.Cancel));
                nativeTutorialBrowserOpen = false;
                drainUiButtons = true;
                fieldSeconds = 0;
                return true;
            }
        }
        return false;
    }

    private void PresentTutorial(NativeTutorialPlayer current)
    {
        if (ReferenceEquals(displayedTutorial, current))
        {
            return;
        }
        if (tutorialParentMenu is null && events?.Scene.NativeMenuTutorial is { } menuRequest)
        {
            if (!gameMenus!.IsOpen || gameMenus.ScreenId != "tutorial-native-online-help")
            {
                gameMenus.Open("tutorial");
                gameMenus.Push("tutorial-native-online-help");
            }
            gameMenus.SelectEntry("tutorial-native-online-help", $"tutorial-{menuRequest.TutorialId}");
            nativeTutorialBrowserOpen = events.Scene.NativeTutorial is { TutorialId: null };
        }
        tutorialParentMenu ??= gameMenus;
        displayedTutorial = current;
        gameMenus = current.Menu;
        gameMenus.ConnectReadOnlyTutorialControls(() => keyboard.Profile, () => keyboard.ControllerPresentation, () => keyboard.LastInputDevice);
        current.RefreshPresentation();
        gameMenus.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        tutorialSeconds = 0;
    }

    private void ApplyTutorialInput(PlayerAction action)
    {
        NativeTutorialPlayer player = displayedTutorial ?? world!.Tutorial ?? throw new InvalidOperationException("No tutorial owns this input.");
        world!.Apply(action);
        if (player.IsComplete)
        {
            RestoreTutorialParent();
            drainUiButtons = true;
            fieldSeconds = 0;
        }
    }

    private void RestoreTutorialParent()
    {
        if (tutorialParentMenu is null)
        {
            return;
        }
        gameMenus = tutorialParentMenu;
        tutorialParentMenu = null;
        displayedTutorial = null;
        tutorialSeconds = 0;
        gameMenus.InvalidateLiveData();
    }
}
