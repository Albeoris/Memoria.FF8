using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private AutoPlayController? autoPlay;
    private bool autoPlayAdvancedThisFrame;
    private bool autoPlayFailureHeld;
    private string? autoPlayHostError;

    private void StopAutoPlayForPlayerSession()
    {
        autoPlay?.Stop();
        autoPlay = null;
        autoPlayAdvancedThisFrame = false;
        autoPlayFailureHeld = false;
        autoPlayHostError = null;
    }

    private void InitializeAutoPlayUi()
    {
        gameMenus!.ConnectAutoPlay(ReadAutoPlayMenuState, ApplyAutoPlayCommand);
    }

    private AutoPlayMenuState ReadAutoPlayMenuState()
    {
        return autoPlay is null ? new AutoPlayMenuState(false, false, "Ready. Start a new game with fixed seed 1.", HasLiveGame: world is not null) : new AutoPlayMenuState(autoPlay.Running, autoPlay.Paused, autoPlay.Status, autoPlayHostError ?? autoPlay.Error, autoPlay.ReproductionPath, autoPlay.ResumeCommand, HasLiveGame: world is not null);
    }

    private void ApplyAutoPlayCommand(string command)
    {
        autoPlay ??= new AutoPlayController(session, () => world ??= CreateWorld(), RestorePlayerState, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!, "autoplay"), createColdProcess: () => GameWorld.CreateColdProcess(options.GameDirectory));
        try
        {
            switch (command)
            {
                case "start":
                    EnterAchievementToolSession();
                    autoPlayHostError = null;
                    autoPlayFailureHeld = false;
                    userPaused = false;
                    autoPlay.Start();
                    ShowAutoPlayFailure();
                    break;
                case "pause":
                    autoPlay.Pause();
                    break;
                case "resume":
                    autoPlay.Resume();
                    gameMenus!.Close();
                    break;
                case "stop":
                    autoPlay.SaveAndStop();
                    autoPlayFailureHeld = false;
                    break;
                case "load-reproduction":
                    EnterAchievementToolSession();
                    autoPlayHostError = null;
                    autoPlayFailureHeld = false;
                    autoPlay.ReplayFailure();
                    break;
                default:
                    throw new ArgumentException($"Unknown automatic play command '{command}'.", nameof(command));
            }
        }
        catch (Exception exception)
        {
            autoPlayHostError = exception.Message;
            autoPlay.Stop();
            ShowAutoPlayFailure();
            Console.Error.WriteLine(exception);
        }
        SynchronizeUiMoviePause();
    }

    private bool UpdateAutoPlay(GameTime gameTime, InputFrame input)
    {
        if (autoPlay?.Running != true && !autoPlayFailureHeld)
        {
            return false;
        }
        if (battleDebug is not null)
        {
            if (autoPlay!.Running && !autoPlay.Paused)
            {
                autoPlay.Pause();
            }
            UpdateBattleDebugUi(input);
            return true;
        }
        if (modelBrowser is not null)
        {
            if (autoPlay!.Running && !autoPlay.Paused)
            {
                autoPlay.Pause();
            }
            UpdateModelBrowserUi(input, gameTime.ElapsedGameTime);
            return true;
        }
        if (sceneInspector is not null)
        {
            if (autoPlay!.Running && !autoPlay.Paused)
            {
                autoPlay.Pause();
            }
            UpdateSceneInspectorUi(input);
            return true;
        }
        if (keyboard.DebugRoomRequested || gameMenus?.IsOpen == true)
        {
            autoPlay?.ResetIdleDeadline();
            UpdateGameUi(gameTime.ElapsedGameTime, input, hostOnly: true);
            return true;
        }
        if (autoPlayFailureHeld || autoPlay!.Paused || autoPlayAdvancedThisFrame)
        {
            return true;
        }
        autoPlayAdvancedThisFrame = true;
        autoPlay.Advance(256);
        fieldSeconds = 0;
        presentedDialogue = null;
        dialoguePages.Clear();
        gameMenus?.InvalidateLiveData();
        ShowAutoPlayFailure();
        fieldAudio?.Update();
        return true;
    }

    private void ShowAutoPlayFailure()
    {
        if (autoPlay is { Running: false } && (autoPlay.Error is not null || autoPlayHostError is not null))
        {
            autoPlayFailureHeld = true;
            fieldFaulted = true;
            fieldRenderFaulted = true;
            gameMenus!.Open("debug-autoplay", true);
        }
    }

    private void ReportAutoPlayHostFailure(Exception exception)
    {
        if (autoPlay?.Running == true)
        {
            autoPlay.ReportHostFailure(exception);
            ShowAutoPlayFailure();
        }
    }

    private void DrawAutoPlayStatus(Rectangle bounds)
    {
        if (autoPlay?.Running == true && gameMenus?.IsOpen != true)
        {
            DrawNoticeUi(bounds, "Auto · " + autoPlay.Status, false);
        }
    }
}
