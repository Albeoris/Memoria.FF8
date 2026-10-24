using Memoria.FF8.Events;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeTutorialPlayer? tutorial;
    private NativeTutorialRequest? tutorialRequest;
    private NativeMenuTutorialRequest? menuTutorialRequest;
    private SceneEventState? tutorialScene;

    private void CaptureNativeTutorialPlayback()
    {
        if (tutorial is not null && ReferenceEquals(tutorialScene, Events.Scene) && tutorialRequest == Events.Scene.NativeTutorial && menuTutorialRequest == Events.Scene.NativeMenuTutorial)
        {
            Events.Scene.NativeTutorialPlayback = tutorial.Capture();
        }
    }

    public NativeTutorialPlayer? Tutorial
    {
        get
        {
            NativeTutorialRequest? request = Events.Scene.NativeTutorial;
            NativeMenuTutorialRequest? menuRequest = Events.Scene.NativeMenuTutorial;
            if (menuRequest is null && request?.TutorialId is null)
            {
                tutorial = null;
                tutorialRequest = null;
                menuTutorialRequest = null;
                tutorialScene = null;
                return null;
            }
            if (tutorial is null || !ReferenceEquals(tutorialScene, Events.Scene) || tutorialRequest != request || menuTutorialRequest != menuRequest)
            {
                //4B3216..4B3224 clamp the unsigned native tutorial selection to8.
                int id = menuRequest?.TutorialId ?? Math.Min(request!.TutorialId!.Value, (byte)8);
                tutorial = new NativeTutorialPlayer(assets.ReadNativeTutorialCatalog(), FieldItemCatalog, id, Events.Scene.NativeTutorialPlayback, Events.Scene.Names);
                tutorialRequest = request;
                menuTutorialRequest = menuRequest;
                tutorialScene = Events.Scene;
                Events.Scene.NativeTutorialPlayback = tutorial.Capture();
            }
            return tutorial;
        }
    }

    public void BeginMenuTutorial(int tutorialId)
    {
        EnsureField();
        uint unlock = assets.ReadNativeTutorialCatalog().Get(tutorialId).Descriptor.UnlockId;
        if (!Events.Scene.UnlockedTutorials.Contains(checked((int)unlock)))
        {
            throw new InvalidOperationException("The native browser lesson has not been unlocked.");
        }
        Events.BeginMenuTutorial(tutorialId);
        _ = Tutorial;
    }

    private PlayerActionResult ApplyNativeTutorial(PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Frames is < 1 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Tutorial input requires one to 30000 callbacks.");
        }
        long frame = Events.Frame;
        NativeTutorialPlayer? player = Tutorial;
        bool accepted;
        if (player is null)
        {
            //The browser retains field ownership while its host selects isolated demonstrations.
            accepted = action.Kind is PlayerActionKind.Cancel or PlayerActionKind.Wait;
            if (action.Kind == PlayerActionKind.Cancel)
            {
                Events.CompleteNativeTutorial();
            }
        }
        else
        {
            accepted = (action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Cancel or PlayerActionKind.Wait) && player.Apply(action);
            if (accepted)
            {
                if (player.IsComplete)
                {
                    if (Events.Scene.NativeMenuTutorial is not null)
                    {
                        Events.EndMenuTutorial();
                    }
                    else
                    {
                        Events.CompleteNativeTutorial();
                    }
                    tutorial = null;
                    tutorialRequest = null;
                    menuTutorialRequest = null;
                    tutorialScene = null;
                }
            }
        }
        return PublishFieldAction(new PlayerActionResult(accepted, NextTrigger, checked((int)(Events.Frame - frame)), accepted ? null : "The native tutorial owns input until it closes."));
    }
}
