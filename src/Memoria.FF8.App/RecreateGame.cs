using System.Diagnostics;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.TitleScene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame : Game
{
    private readonly LaunchOptions options;
    private GameResourceHost? resourceHost;
    private GameSession session;
    private readonly GraphicsDeviceManager graphics;
    private GraphicsHostReadiness? graphicsReadiness;
    private KeyboardInput keyboard = new();
    private ControllerDevice? controller;
    private ControllerFeedback? controllerFeedback;
    private bool capturingControls;
    private GameplayClock gameplayClock = new();
    private MovieClock movieClock;
    private FieldAudioPresentation? fieldAudio;
    private bool resumeFieldMusic;
    private readonly PlayerSaveStore playerSaves;
    private readonly PlayerSaveMenu saveMenu;
    private SpriteBatch batch = null!;
    private SystemTextRenderer text = null!;
    private Texture2D pixel = null!;
    private MoviePresentation? movie;
    private readonly MovieSkipGesture movieSkipGesture = new();
    private (int Field, int Movie, NativeMovieSource? Source)? movieSkipIdentity;
    private int movieIndex = -1;
    private int movieDiscIndex = -1;
    private int titleSelection;
    private TitleSceneRenderer? titleScene;
    private TitleArtworkRenderer? titleArtwork;
    private string? error;
    private GameWorld? world;
    private FieldAssets? field => world?.Field;
    private EventEngine? events => world?.Events;
    private Texture2D fieldTexture = null!;
    private GpuTriangleRenderer? fieldGpu;
    private GpuTriangleRenderer? battleGpu;
    private bool fieldGpuPresented;
    private Texture2D PresentedFieldTexture => fieldGpuPresented ? fieldGpu!.Target : fieldTexture;
    private DialogueState? presentedDialogue;
    private int selectedChoice;
    private string enteredName = "Squall";
    private int updateFrames;
    private readonly WorldSimulationClock worldSimulationClock = new();
    private WorldMapScene? worldSimulationOwner;
    private bool frameCaptured;
    private bool fieldFaulted;
    private bool fieldRenderFaulted;
    private bool worldPresentationDeferred;
    private double fieldSeconds;
    private ControlQueue? controls;
    private ControlQueue? updateControls;
    private string? pendingCapture;
    private readonly GameLoopMetrics metrics = new();
    private Func<string>? saveNotice;
    private double saveNoticeSeconds;
    private bool userPaused;
    private readonly UpdateSaveHandoff updateSave;

    public RecreateGame(LaunchOptions options, GameSession session, bool playerSession = false)
    {
        this.options = options;
        globalSettings = new GlobalSettingsPaths(isolatedDirectory: options.SettingsDirectory);
        this.session = session;
        achievementToolSession = !playerSession || options.IsAchievementToolSession;
        updateSave = new UpdateSaveHandoff(CaptureUpdateState, ValidatePlayerState, EnsureUpdateSaveIsSafe);
        startup = new StartupSequence(StartupSequence.ShouldPlay(options));
        movieClock = new MovieClock(session, options.GameDirectory, options.MediaToolsDirectory);
        playerSaves = new PlayerSaveStore(options.SavePath, directory: options.BattleTestPath is null ? null : Path.Combine(globalSettings.DirectoryPath, "player-saves"), locationName: state =>
        {
            EnsureLiveMenuCatalog();
            return liveMenuCatalog!.LocationName(state);
        }, originalDirectory: ReadOriginalSaveDirectory(), loadOriginal: LoadOriginalPlayerState);
        saveMenu = new PlayerSaveMenu(playerSaves);
        saveMenu.BeforeBankChange = EnsurePlayerSaveBank;
        saveMenu.RequiresMemoriaFormat = () => PlayerSaveStore.RequiresMemoriaFormat(this.session.State, allowGuardianForceProjection: true);
        saveMenu.SoundRequested += cue => fieldAudio?.PlayUiSound(cue);
        if (options.SettingsDirectory is null)
        {
            globalSettings.ImportLegacyFiles(options.SavePath);
        }
        uiPreferences = GameUi.UiPreferences.Load(UiPreferencesPath);
        playerSaves.SetQuickSaveCount(uiPreferences.QuickSaveCount);
        (int width, int height) = uiPreferences.WindowSize(options);
        appliedMsaaSamples = uiPreferences.Graphics.MsaaSamples;
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = width, PreferredBackBufferHeight = height, SynchronizeWithVerticalRetrace = uiPreferences.VSync, GraphicsProfile = GraphicsProfile.HiDef, PreferMultiSampling = appliedMsaaSamples > 0 };
        graphics.PreparingDeviceSettings += (_, settings) => settings.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = uiPreferences.Graphics.MsaaSamples;
        Window.Title = "Memoria - Final Fantasy VIII";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 60);
        InactiveSleepTime = TimeSpan.Zero;
        Window.TextInput += OnTextInput;
        session.StateChanged += LogSessionState;
        InitializeAchievements();
    }

    protected override void LoadContent()
    {
        graphicsReadiness = GraphicsHostReadiness.Initialize(() => new GraphicsHostReadiness(GraphicsDevice));
        resourceHost = new GameResourceHost(options.GameDirectory, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory);
        movieClock.ResolveGameFile = resourceHost.ResolveGameFile;
        controller = new ControllerDevice();
        controllerFeedback = new ControllerFeedback(controller.SetVibration);
        batch = GraphicsHostReadiness.Initialize(() => new SpriteBatch(GraphicsDevice));
        text = new SystemTextRenderer(GraphicsDevice);
        pixel = GraphicsHostReadiness.Initialize(() => new Texture2D(GraphicsDevice, 1, 1));
        pixel.SetData([Color.White]);
        fieldTexture = GraphicsHostReadiness.Initialize(() => new Texture2D(GraphicsDevice, FieldRenderer.Width, FieldRenderer.Height));
        if (options.AudioEnabled)
        {
            fieldAudio = new FieldAudioPresentation(options.GameDirectory, movieClock.ToolsDirectory, () => resourceHost.Mods.Catalog);
        }
        InitializeGameUi();
        if (options.ControlDirectory is not null)
        {
            controls = new ControlQueue(options.ControlDirectory, DispatchControl);
        }
        if (options.UpdateControlDirectory is not null)
        {
            updateControls = new ControlQueue(options.UpdateControlDirectory, DispatchUpdateControl);
        }
        if (!options.SoftwareRenderer)
        {
            fieldGpu = GraphicsHostReadiness.Initialize(() => new GpuTriangleRenderer(GraphicsDevice));
            battleGpu = GraphicsHostReadiness.Initialize(() => new GpuTriangleRenderer(GraphicsDevice));
        }
        if (options.BattleTestPath is not null)
        {
            StartDiagnosticBattleHost();
        }
        Console.WriteLine($"graphics={GraphicsDevice.Adapter.Description} scene-renderer={(options.SoftwareRenderer ? "software-diagnostic" : "gpu")} cwd={Environment.CurrentDirectory}");
    }

    private readonly GameDialogContext dialogContext = new();
    private readonly SdlDesktopDialogs desktopDialogs = new();
    private SynchronizationContext? previousDialogContext;
    private bool dialogInputBlocked;

    protected override void Update(GameTime gameTime)
    {
        presentationTiming.BeginPresentationFrame(ActiveBattle, CanAdvanceBattleRenderPhase);
        SynchronizationContext.SetSynchronizationContext(dialogContext);
        dialogContext.Drain();
        desktopDialogs.PumpPendingEvents();
        // Keep launcher handoff and recovery responsive during native pickers.
        updateControls?.Poll();
        if (dialogContext.IsBusy)
        {
            dialogInputBlocked = true;
            UpdateGameConfirmation();
            controllerFeedback?.Update(session.State.VibrationProcess, false, keyboard.Profile.Controller.VibrationEnabled);
            presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
            return;
        }
        if (dialogInputBlocked)
        {
            keyboard.SuppressHeldUiInput();
            drainUiButtons = true;
            dialogInputBlocked = false;
        }
        UpdateWindowModeHotkey();
        ShowPendingNewGameChocoboClaim();
        UpdateAchievements();
        if (UpdateFocusPause())
        {
            return;
        }
        if (updateSave.Paused)
        {
            presentationPacer.WaitForNextFrame(appliedFrameLimit ?? 60);
            controllerFeedback?.Update(session.State.VibrationProcess, false, keyboard.Profile.Controller.VibrationEnabled);
            return;
        }
        autoPlayAdvancedThisFrame = false;
        if (battleEntryTransition is not null)
        {
            UpdateBattleEntry(gameTime);
            return;
        }
        ApplyDisplaySettings();
        try
        {
            SynchronizeGuardianModProfile();
            resourceHost?.Pump();
            world?.SynchronizeGameplayTuning();
            RefreshLocalizationPresentation();
            gameMenus?.RefreshModView();
            UpdateModLibraryNotifications();
            UpdateDisplayFrame(gameTime);
            SynchronizeFieldVoices();
        }
        catch (Exception exception) when (autoPlay?.Running == true)
        {
            ReportAutoPlayHostFailure(exception);
            Console.Error.WriteLine(exception);
        }
        finally
        {
            if (battleEntryTransition is null)
            {
                controllerFeedback?.Update(session.State.VibrationProcess, IsActive && !userPaused && !capturingControls && !fieldFaulted && !fieldRenderFaulted && gameMenus?.IsOpen != true && !saveMenu.IsOpen, keyboard.Profile.Controller.VibrationEnabled);
            }
        }
    }

    private void UpdateGameFrame(GameTime gameTime)
    {
        long updateStarted = Stopwatch.GetTimestamp();
        SynchronizeMenuPresentation();
        ControllerDevice inputController = controller ?? throw new InvalidOperationException("The graphical host requires its initialized input device.");
        GamePadState pad = options.BattleTestPath is null ? inputController.Read() : default;
        if (gameMenus is not null)
        {
            gameMenus.NamingControllerConnected = pad.IsConnected;
        }
        if (autoPlay?.Running != true && !autoPlayFailureHeld)
        {
            SynchronizeNativeNamePrompt();
        }
        KeyboardState keys = options.BattleTestPath is null ? Microsoft.Xna.Framework.Input.Keyboard.GetState() : default;
        keys = windowModeHotkey.Filter(keys);
        keyboard.ControllerPresentation = inputController.Presentation;
        keyboard.ControllerGeneration = inputController.Generation;
        bool wasCapturing = gameMenus?.IsCapturingControls == true;
        bool capturing = gameMenus?.UpdateControlsCapture(keys, pad, IsActive) == true;
        UpdateMenuPresentationHotkey(keys, wasCapturing || capturing || DuelOwnsInput);
        capturingControls = capturing;
        keyboard.RecoveryNavigationEnabled = gameMenus is { IsOpen: true, IsControlsScreen: true };
        bool typingName = gameMenus?.IsTypingName == true || modelBrowser?.SearchFocused == true || gameMenus?.IsGuardianSearchEditing == true || gameMenus?.IsModSearchEditing == true;
        keyboard.GuardianEditorEnabled = gameMenus?.IsGuardianAbilityEditor == true;
        keyboard.ModsManagerEnabled = gameMenus?.IsModManager == true;
        bool namingNavigation = gameMenus?.IsEditingName == true;
        if (keyboard.TextEntryEnabled != typingName || keyboard.NamingNavigationEnabled != namingNavigation)
        {
            keyboard.SuppressHeldNameInput();
        }
        keyboard.TextEntryEnabled = typingName;
        keyboard.NamingNavigationEnabled = namingNavigation;
        keyboard.NativeButtonsOwnInput = DuelOwnsInput;
        InputFrame input = keyboard.Read(keys, pad, IsActive && !capturing);
        var currentMovieIdentity = (session.State.FieldId, session.State.MovieIndex, session.State.MovieSource);
        if (session.State.Scene != SceneKind.Movie || movieSkipIdentity != currentMovieIdentity || !IsActive)
        {
            movieSkipGesture.Reset();
        }
        movieSkipIdentity = session.State.Scene == SceneKind.Movie ? currentMovieIdentity : null;
        if (capturing)
        {
            movieSkipGesture.Reset();
            UpdateControlsCapturePointer();
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (keyboard.PauseRequested && !saveMenu.IsOpen && gameMenus?.IsOpen != true && sceneInspector is null && modelBrowser is null)
        {
            if (autoPlay?.Running == true)
            {
                if (autoPlay.Paused)
                {
                    autoPlay.Resume();
                }
                else
                {
                    autoPlay.Pause();
                }
            }
            else
            {
                userPaused = !userPaused;
            }
            SynchronizeUiMoviePause();
        }
        if (userPaused)
        {
            movieSkipGesture.Reset();
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (!BattleModalBlocksGameplay && keyboard.SpeedToggleRequested && gameMenus?.IsNamePrompt != true)
        {
            gameplayClock.ToggleSpeed();
        }
        // A capture fences the queue until Draw publishes that exact presented frame.
        if (pendingCapture is null)
        {
            controls?.Poll();
        }
        updateFrames++;
        if (options.ExitAfterFrames is int frames && updateFrames >= frames)
        {
            Exit();
        }
        if (options.ExitAfterSeconds is double duration && gameTime.TotalGameTime.TotalSeconds >= duration)
        {
            Exit();
        }
        saveNoticeSeconds = Math.Max(0, saveNoticeSeconds - gameTime.ElapsedGameTime.TotalSeconds);
        if (UpdateAutoPlay(gameTime, input))
        {
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (UpdateStartup(gameTime.ElapsedGameTime, input))
        {
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (session.State.Scene == SceneKind.Title)
        {
            fieldAudio?.PlayTitleMusic();
        }
        UpdateNativeFontPresentation();
        if (UpdateLocalizationAuthoring(gameTime.ElapsedGameTime))
        {
            fieldAudio?.Update();
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (!BattleModalBlocksGameplay && gameMenus?.IsNamePrompt != true && (UpdateModelBrowserUi(input, gameTime.ElapsedGameTime) || UpdateSceneInspectorUi(input) || UpdateBattleDebugUi(input)))
        {
            movieSkipGesture.Reset();
            fieldAudio?.Update();
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (!BattleModalBlocksGameplay && (UpdateTripleTriadUi(gameTime.ElapsedGameTime, input) || UpdateGameUi(gameTime.ElapsedGameTime, input)))
        {
            movieSkipGesture.Reset();
            fieldAudio?.Update();
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (!BattleModalBlocksGameplay && UpdatePlayerSaves(input))
        {
            movieSkipGesture.Reset();
            fieldAudio?.Update();
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        TimeSpan gameplayElapsed = gameplayClock.Advance(gameTime.ElapsedGameTime, session.State.Scene, events?.IsScriptedInterface == true);
        world?.AdvanceDebug(gameplayElapsed);
        if (UpdateBattle(gameplayElapsed, input, gameTime.ElapsedGameTime))
        {
            if (battleEntryTransition is null)
            {
                fieldAudio?.Update();
            }
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (session.State.Scene == SceneKind.Title)
        {
            if (input.MenuVertical != 0)
            {
                int nextSelection = (titleSelection + (input.MenuVertical > 0 ? 1 : -1) + TitleChoices.Length) % TitleChoices.Length;
                if (titleSelection != nextSelection)
                {
                    fieldAudio?.PlayUiSound(GameUi.MenuSound.Move);
                }
                titleSelection = nextSelection;
            }
            if (input.Confirm)
            {
                fieldAudio?.PlayUiSound(GameUi.MenuSound.Confirm);
                if (titleSelection == 0)
                {
                    newGameMagicPreferences = newGameMagicPreferences with { ChocoboWorldMode = DefaultChocoboWorldMode };
                    gameMenus!.Open("new-game-options", false);
                }
                else if (titleSelection == 1)
                {
                    OpenPlayerSaveMenu(PlayerSaveMenuMode.Load);
                }
                else
                {
                    gameMenus!.Open("config", false);
                }
            }
            if (input.Cancel)
            {
                fieldAudio?.PlayUiSound(GameUi.MenuSound.Cancel);
                gameMenus!.ShowTitleExitConfirmation(Exit);
            }
        }
        else if (session.State.Scene == SceneKind.Movie)
        {
            UpdateMovie(gameTime, input);
        }
        else if (session.State.Scene == SceneKind.WorldMap)
        {
            UpdateWorldMap(gameplayElapsed, input);
        }
        else
        {
            UpdateField(gameplayElapsed, input);
        }
        PreserveCompletedMovieFrame();
        if (session.State.Scene != SceneKind.Movie && movie is not null)
        {
            movie.Dispose();
            movie = null;
            movieIndex = -1;
        }
        if (resumeFieldMusic && world?.Field is not null)
        {
            fieldAudio?.ResumeMusic(session.State.GlobalSoundProcess?.MusicPlayback);
            resumeFieldMusic = false;
        }
        fieldAudio?.Update();
        base.Update(gameTime);
        metrics.Update(updateStarted);
    }

    private GameWorld CreateWorld()
    {
        var next = new GameWorld(session, options.GameDirectory, movieClock, resourceHost?.Resources, options.AudioEnabled, readSceneMods: () => resourceHost?.SceneMods ?? Memoria.FF8.Mods.FieldSceneMods.Empty, modelOverrides: resourceHost, readGameplayPolicy: () => resourceHost?.GameplayPolicy ?? GameplayTuningPolicy.Empty);
        fieldAudio?.Attach(next.Events);
        resumeFieldMusic = session.State.EventState is not null || session.State.DepartingFieldState is not null;
        return next;
    }

    private void UpdateMovie(GameTime gameTime, InputFrame input)
    {
        try
        {
            if (movieSkipGesture.Update(gameTime.ElapsedGameTime, keyboard.CancelHeld))
            {
                world ??= CreateWorld();
                world.EnsureField();
                world.Apply(new PlayerAction(PlayerActionKind.SkipMovie));
                return;
            }
            if (world is null && session.State.EventState is not null)
            {
                world = CreateWorld();
                world.EnsureField();
            }
            if (movie is null || movieIndex != session.State.MovieIndex || movieDiscIndex != movieClock.SourceDiscIndex)
            {
                movie?.Dispose();
                string directory = movieClock.ToolsDirectory;
                string path = movieClock.SourcePath;
                Memoria.FF8.Media.MovieInformation information = movieClock.Prepare();
                Memoria.FF8.Media.MovieInformation presentationInformation = movieClock.PreparePresentation();
                // Resume from the actual last decoded frame. Wall-clock/audio position can be ahead
                // when drawing falls behind and must not skip native callbacks on decoder restart.
                double startSeconds = movieClock.State.FrameIndex / information.FramesPerSecond;
                movie = new MoviePresentation(GraphicsDevice, directory, path, startSeconds, presentationInformation, options.AudioEnabled, information);
                movieIndex = session.State.MovieIndex;
                movieDiscIndex = movieClock.SourceDiscIndex;
                error = null;
            }
            if (world is not null && !options.ControlStep && (world.Events.ButtonInput?.PhysicalHeld ?? 0) != keyboard.HeldFieldButtons)
            {
                world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, keyboard.HeldFieldButtons));
            }
            int playbackField = session.State.FieldId;
            int playbackMovie = session.State.MovieIndex;
            NativeMovieSource playbackSource = MovieClock.RequireSource(session.State);
            movie.Update((playback, seconds) =>
            {
                if (world is not null && !fieldFaulted)
                {
                    long before = world.Events.Frame;
                    bool active = world.AdvanceMovieFrame(playback, seconds, options.ControlStep ? 0 : -input.Horizontal, options.ControlStep ? 0 : input.Vertical);
                    if (world.Events.Frame != before)
                    {
                        metrics.Simulate(1);
                    }
                    return active;
                }
                session.SetMoviePlayback(playback);
                session.SetMoviePosition(seconds);
                return session.State.Scene == SceneKind.Movie && session.State.MovieIndex == playbackMovie && session.State.FieldId == playbackField && session.State.MovieSource == playbackSource;
            }, movieClock.SetPresentationFrame);
            if (session.State.Scene != SceneKind.Movie || session.State.MovieIndex != playbackMovie || session.State.FieldId != playbackField || session.State.MovieSource != playbackSource)
            {
                fieldSeconds = 0;
                return;
            }
            session.SetMoviePlayback(movie.Playback);
            session.SetMoviePosition(movie.PositionSeconds);
            if (movie.Error is string playbackError)
            {
                if (error != playbackError)
                {
                    Console.Error.WriteLine(playbackError);
                }
                error = playbackError;
            }
            if (movie.Finished)
            {
                Console.WriteLine($"movie-complete={movieIndex} position={movie.PositionSeconds:F3} video-eof=true audio-drained=true");
                session.CompleteMovie();
            }
        }
        catch (EventScriptException exception)
        {
            error = exception.Message;
            fieldFaulted = true;
            Console.Error.WriteLine(exception);
        }
        catch (Exception exception)
        {
            error = $"Movie playback failed: {exception.Message}";
            Console.Error.WriteLine(exception);
        }
    }

    private void UpdateField(TimeSpan elapsed, InputFrame input)
    {
        if (fieldFaulted && field?.Id == session.State.FieldId)
        {
            return;
        }
        try
        {
            world ??= CreateWorld();
            if (world.EnsureField())
            {
                presentedDialogue = null;
                dialoguePages.Clear();
                error = null;
                fieldFaulted = false;
                fieldRenderFaulted = false;
                Console.WriteLine($"loaded={field!.Id}:{field.Name} models={field.Models.Count} tiles={field.Background.Count}");
            }
            if (SynchronizeNativeNamePrompt())
            {
                return;
            }
            if (events!.PartyMenu is not null)
            {
                UpdatePartyMenu(elapsed, input);
                return;
            }
            if (!options.ControlStep && (events!.ButtonInput?.PhysicalHeld ?? 0) != keyboard.HeldFieldButtons)
            {
                world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, keyboard.HeldFieldButtons));
            }
            UpdateDialoguePages(elapsed);
            DialogueState? dialogue = events!.CurrentDialogue;
            if (dialogue != presentedDialogue)
            {
                presentedDialogue = dialogue;
                (selectedChoice, enteredName) = DialoguePresentation.ResolveDraft(dialogue, session.State.DialogueDraft, events.PendingNameDefault ?? "Squall");
            }
            if (dialogue is { CanConfirm: true, IsNamePrompt: false })
            {
                if (dialogue.FirstChoice is null && !input.Confirm && !input.Cancel && CanVoiceAutoAdvance(dialogue))
                {
                    input = input with { Confirm = true };
                }
                if (DialoguePresentation.ResolveInput(dialogue, input, ref selectedChoice, enteredName) is PlayerAction action && !AdvanceDialoguePages())
                {
                    if (world.Apply(action).Accepted)
                    {
                        dialoguePages.Complete(dialogue);
                        session.SetDialogueDraft(null);
                    }
                }
            }
            else if (events.IsUserControlEnabled)
            {
                ActorState? player = events.PlayerActor;
                if (player is { Active: true, Visible: true })
                {
                    if (keyboard.UiDetailRequested)
                    {
                        metrics.Simulate(world.Apply(new PlayerAction(PlayerActionKind.CardChallenge, Value: -1)).FramesAdvanced);
                    }
                    else if (input.Confirm)
                    {
                        metrics.Simulate(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).FramesAdvanced);
                    }
                    else if (input.Cancel)
                    {
                        metrics.Simulate(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).FramesAdvanced);
                    }
                }
            }
            if (events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
            {
                fieldSeconds = 0;
                return;
            }
            fieldSeconds += options.ControlStep ? 0 : elapsed.TotalSeconds;
            while (fieldSeconds + 1e-9 >= 1d / EventEngine.SimulationFps)
            {
                fieldSeconds -= 1d / EventEngine.SimulationFps;
                if ((input.Horizontal != 0 || input.Vertical != 0) && events.IsUserControlEnabled && !events.IsMovementBlockedByDialogue)
                {
                    metrics.Simulate(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -input.Horizontal, Vertical: input.Vertical)).FramesAdvanced);
                }
                else
                {
                    events.Step();
                    metrics.Simulate(1);
                }
                world.CommitFieldPresentation();
                if (session.State.Scene != SceneKind.Field || session.State.FieldId != field!.Id || events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition || events.PendingInteractiveBattle is not null || events.PendingTripleTriad is not null || events.Scene.NativeTutorial is not null || events.CurrentDialogue?.IsNamePrompt == true)
                {
                    SynchronizeNativeNamePrompt();
                    fieldSeconds = 0;
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            fieldFaulted = true;
            Console.Error.WriteLine(exception);
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (IsActive && localizationWorkbench.Enabled && localizationWorkbench.OwnsKeyboard)
        {
            if (!char.IsControl(args.Character))
            {
                authoringTextInput?.ReceiveCharacter(args.Character);
            }
            return;
        }

        if (DuelOwnsInput)
        {
            return;
        }
        if (IsActive && modelBrowser?.SearchFocused == true)
        {
            if (args.Character == '\b')
            {
                modelBrowser.Backspace();
            }
            else
            {
                modelBrowser.TextInput(args.Character);
            }
            return;
        }
        if (IsActive && keyboard.AllowsNameText(args.Key))
        {
            if (gameMenus?.EnterGameplayNumberCharacter(args.Character) != true && gameMenus?.EnterLocalizationCharacter(args.Character) != true && gameMenus?.EnterFontSearchCharacter(args.Character) != true && gameMenus?.EnterModSearchCharacter(args.Character) != true && gameMenus?.EnterGuardianSearchCharacter(args.Character) != true)
            {
                gameMenus?.EnterNameCharacter(args.Character);
            }
        }
    }

    private void UpdateWorldMap(TimeSpan elapsed, InputFrame input)
    {
        if (fieldFaulted)
        {
            return;
        }
        try
        {
            world ??= CreateWorld();
            if (world.EnsureField())
            {
                presentedDialogue = null;
                dialoguePages.Clear();
                worldSimulationClock.Reset();
                error = null;
                Console.WriteLine($"loaded=world entry={session.State.WorldArrival}");
            }
            if (!ReferenceEquals(worldSimulationOwner, world.WorldScene))
            {
                worldSimulationOwner = world.WorldScene;
                worldSimulationClock.Reset();
            }
            UpdateDialoguePages(elapsed);
            DialogueState? dialogue = world.CurrentDialogue;
            if (dialogue != presentedDialogue)
            {
                presentedDialogue = dialogue;
                selectedChoice = dialogue?.InitialChoice ?? 0;
            }
            if (dialogue is { FirstChoice: not null, LastChoice: not null })
            {
                if (DialoguePresentation.ResolveInput(dialogue, input, ref selectedChoice, enteredName) is PlayerAction action && !AdvanceDialoguePages())
                {
                    metrics.Simulate(world.Apply(action).FramesAdvanced);
                    dialoguePages.Complete(dialogue);
                    return;
                }
            }
            else if (dialogue is { CanConfirm: true } && input.Confirm)
            {
                if (!AdvanceDialoguePages())
                {
                    metrics.Simulate(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).FramesAdvanced);
                    dialoguePages.Complete(dialogue);
                }
                return;
            }
            worldSimulationClock.Accumulate(options.ControlStep ? TimeSpan.Zero : elapsed);
            while (worldSimulationClock.TryStep())
            {
                PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, session.State, keyboard.HeldControlButtons);
                metrics.Simulate(world.Apply(action).FramesAdvanced);
                if (session.State.Scene != SceneKind.WorldMap)
                {
                    worldSimulationClock.Reset();
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            fieldFaulted = true;
            Console.Error.WriteLine(exception);
        }
    }

    private void SaveCheckpoint(string path)
    {
        CaptureSession();
        session.Save(path);
    }

    private void CaptureSession()
    {
        world?.CaptureBattleState();
        if (world?.PendingEntityName is not null && gameMenus?.IsNamePrompt == true)
        {
            session.SetEntityNameDraft(gameMenus.NameDraft);
        }
        if (session.State.Scene == SceneKind.WorldMap)
        {
            world?.EnsureField();
            // Persistence prepares the dormant field snapshot once, after all live owners have been captured.
        }
        else if (events is not null && world?.HasCurrentFieldAllocation == true)
        {
            DialogueState? dialogue = events.CurrentDialogue;
            session.SetDialogueDraft(dialogue is null ? null : new DialogueDraft(dialogue.Channel, dialogue.MessageId, selectedChoice, gameMenus?.IsNamePrompt == true ? gameMenus.NameDraft : enteredName));
        }
        else if (events is not null)
        {
            session.SetDepartingFieldState(events.Capture());
        }
    }

    private bool UpdatePlayerSaves(InputFrame input)
    {
        if (input.LoadMenu || input.SaveMenu)
        {
            if (input.SaveMenu && (session.State.Scene == SceneKind.Title || fieldFaulted))
            {
                ShowSaveNotice("Start or load a running game before saving.");
            }
            else
            {
                OpenPlayerSaveMenu(input.LoadMenu ? PlayerSaveMenuMode.Load : PlayerSaveMenuMode.Save);
            }
            return true;
        }
        if (saveMenu.IsOpen)
        {
            UpdatePlayerSavePointer();
            UpdatePlayerSaveMenuAsync(input);
            return true;
        }
        if (input.Save || input.QuickLoad)
        {
            try
            {
                if (input.QuickLoad)
                {
                    OpenPlayerSaveMenu(PlayerSaveMenuMode.QuickLoad);
                }
                else
                {
                    SavePlayerSlot(0);
                }
            }
            catch (Exception exception)
            {
                ShowSaveNotice($"{(input.QuickLoad ? "Quick load" : "Quick save")} failed: {exception.Message}");
                Console.Error.WriteLine(exception);
            }
            return true;
        }
        return false;
    }

    private void OpenPlayerSaveMenu(PlayerSaveMenuMode mode)
    {
        if (movie is not null)
        {
            session.SetMoviePosition(movie.PositionSeconds);
            session.SetMoviePlayback(movie.Playback);
            movie.Dispose();
            movie = null;
            movieIndex = -1;
        }
        saveMenu.Open(mode);
    }

    private void PreparePlayerSlotSave()
    {
        if (fieldFaulted)
        {
            throw new InvalidOperationException("The game stopped at a script or rendering fault. Load a running save before saving.");
        }
        if (movie is not null)
        {
            session.SetMoviePosition(movie.PositionSeconds);
            session.SetMoviePlayback(movie.Playback);
        }
        CaptureSession();
    }

    private void SavePlayerSlot(int slot)
    {
        PreparePlayerSlotSave();
        playerSaves.Save(slot, session, ValidatePlayerState);
        saveNotice = null;
        saveNoticeSeconds = 0;
    }

    private void LoadPlayerSlot(int slot)
    {
        GameState state = playerSaves.Load(slot);
        ValidatePlayerState(state);
        StopAutoPlayForPlayerSession();
        RestorePlayerState(state);
        saveNotice = null;
        saveNoticeSeconds = 0;
    }


    private void ValidatePlayerState(GameState state)
    {
        // Validate the VM and asset continuation before replacing the running game.
        var candidateSession = new GameSession(state);
        if (state.Scene != SceneKind.Movie || state.EventState is not null)
        {
            using var candidateClock = new MovieClock(candidateSession, options.GameDirectory, options.MediaToolsDirectory, resourceHost is null ? null : resourceHost.ResolveGameFile);
            using var candidate = new GameWorld(candidateSession, options.GameDirectory, candidateClock, resourceHost?.Resources, sharedAssets: world?.ValidationAssets, readSceneMods: () => resourceHost?.SceneMods ?? Memoria.FF8.Mods.FieldSceneMods.Empty, modelOverrides: resourceHost, readGameplayPolicy: () => resourceHost?.GameplayPolicy ?? GameplayTuningPolicy.Empty);
            candidate.EnsureField();
        }
    }

    private void RestorePlayerState(GameState state)
    {
        ReplacePlayerSession(new GameSession(ChocoboWorldSettings.Migrate(state, resourceHost!.Resources.Source.Edition)));
        promptNewGameChocoboClaim = session.State.GameplayPreferences.ChocoboWorldMode == ChocoboWorldMode.Memoria;
    }

    private static void LogSessionState(GameState state)
    {
        Console.WriteLine($"scene={state.Scene} field={state.FieldId}:{state.FieldName} movie={state.MovieIndex}");
    }

    private void ReplacePlayerSession(GameSession replacement)
    {
        EndStartup();
        ClearBattlePresentation();
        sceneInspector = null;
        gameMenus?.Close();
        nativeNamePrompt = null;
        queuedEntityName = null;
        returnToItemMenuAfterNaming = false;
        suppressNativeNameInput = false;
        SynchronizeUiMoviePause();
        movie?.Dispose();
        movie = null;
        movieIndex = -1;
        fieldAudio?.StopAll();
        world?.Dispose();
        world = null;
        movieClock.Dispose();
        session.StateChanged -= LogSessionState;
        session.CaptureAchievementOwner = null;
        session = replacement;
        session.StateChanged += LogSessionState;
        if (achievements is not null)
        {
            session.CaptureAchievementOwner = achievements.CaptureOwner;
        }
        movieClock = new MovieClock(session, options.GameDirectory, options.MediaToolsDirectory, resourceHost is null ? null : resourceHost.ResolveGameFile);
        autoPlay?.BindSession(session);
        gameplayClock = new();
        worldSimulationClock.Reset();
        worldSimulationOwner = null;
        keyboard = new() { Profile = keyboard.Profile };
        movieSkipGesture.Reset();
        movieSkipIdentity = null;
        movieDiscIndex = -1;
        displayedTutorial = null;
        tutorialParentMenu = null;
        nativeTutorialBrowserOpen = false;
        tutorialSeconds = 0;
        guardianPendingModReview = null;
        guardianAcceptedModReview = null;
        guardianAcceptedModState = null;
        updateSave.Cancel();
        userPaused = false;
        capturingControls = false;
        uiMovieSuspended = false;
        resumeFieldMusic = false;
        fieldGpuPresented = false;
        worldPresentationDeferred = false;
        presentedDialogue = null;
        dialoguePages.Clear();
        selectedChoice = 0;
        enteredName = "Squall";
        saveNotice = null;
        saveNoticeSeconds = 0;
        fieldSeconds = 0;
        error = null;
        fieldFaulted = false;
        fieldRenderFaulted = false;
        presentationTiming.DiscardPendingTime();
        presentationPacer.Reset();
        if (gameMenus is not null)
        {
            InitializeSessionMenus();
        }
    }

    private void ShowSaveNotice(string message, double durationSeconds = 5)
    {
        ShowSaveNotice(() => message, durationSeconds);
    }

    private void ShowSaveNotice(Func<string> message, double durationSeconds = 5)
    {
        saveNotice = message;
        saveNoticeSeconds = durationSeconds;
    }

    private void StartNewGameSession()
    {
        GameSession fresh = GameWorld.CreateColdProcess(options.GameDirectory);
        fresh.StartNewGame(fresh.State.NativeBattleMemory);
        fresh.SetGameplayPreferences(newGameMagicPreferences with { ChocoboWorldMode = newGameMagicPreferences.ChocoboWorldMode ?? DefaultChocoboWorldMode, WeaponMagazineRequired = uiPreferences.WeaponMagazineRequired, WeaponUpgradeOrder = uiPreferences.WeaponUpgradeOrder, CardModOnce = uiPreferences.CardModOnce, LimitOncePerTurn = uiPreferences.LimitOncePerTurn, SelphieCardSlots = uiPreferences.SelphieCardSlots, DrawOncePerSpell = uiPreferences.DrawOncePerSpell, Difficulty = uiPreferences.Difficulty, AtbMode = uiPreferences.AtbMode, GunbladeMode = uiPreferences.GunbladeMode, TripleTriad = uiPreferences.TripleTriad, GuardianFormsEnabled = uiPreferences.GuardianFormsEnabled });
        StopAutoPlayForPlayerSession();
        ReplacePlayerSession(fresh);
        BattleInput[] battleSettingsInputs = uiPreferences.CreateBattleSettingsInputs();
        if (battleSettingsInputs.Length > 0)
        {
            world = CreateWorld();
            foreach (BattleInput input in battleSettingsInputs)
            {
                world.ApplyBattleInput(input);
            }
        }
        promptNewGameChocoboClaim = session.State.GameplayPreferences.ChocoboWorldMode == ChocoboWorldMode.Memoria;
        presentedDialogue = null;
        dialoguePages.Clear();
        fieldSeconds = 0;
        error = null;
        fieldFaulted = false;
        fieldRenderFaulted = false;
    }

    private object DispatchControl(ControlRequest request)
    {
        if (diagnosticDriver is { Finished: false } && request.Command.ToLowerInvariant() is not ("status" or "battle-status" or "quit"))
        {
            throw new InvalidOperationException("The in-process diagnostic driver owns command submission until it exits.");
        }
        if (request.Command.Equals("cancel-update", StringComparison.OrdinalIgnoreCase))
        {
            updateSave.Cancel();
            SynchronizeUiMoviePause();
            return new { Resumed = true };
        }
        if (request.Command.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            Exit();
            return new { Exiting = true };
        }
        if (request.Command.Equals("graphics-restart", StringComparison.OrdinalIgnoreCase))
        {
            return RestartGraphicsBackend(Memoria.FF8.Platform.GraphicsBackendSelection.Parse(request.Name ?? throw new ArgumentException("A graphics backend is required.")), request.ExpiresAt);
        }
        if (updateSave.Paused && !request.Command.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The game is paused for an update. Cancel the update before changing the game.");
        }
        if (request.Command.Equals("save-for-update", StringComparison.OrdinalIgnoreCase))
        {
            UpdateSaveResult saved = updateSave.Save(request.Path, request.ExpiresAt);
            SynchronizeUiMoviePause();
            return saved;
        }
        if (SlotsModalBlocksGameplay && request.Command.ToLowerInvariant() is not ("status" or "capture" or "save" or "battle-status" or "battle-input"))
        {
            throw new InvalidOperationException("The pending Slots card choice owns gameplay input.");
        }
        if (request.Command.StartsWith("battle-", StringComparison.OrdinalIgnoreCase))
        {
            return DispatchBattleControl(request);
        }
        if (request.Command.Equals("debug-room", StringComparison.OrdinalIgnoreCase) || request.Command.StartsWith("ui-", StringComparison.OrdinalIgnoreCase))
        {
            return DispatchUiControl(request);
        }
        if (request.Command.Equals("new-game", StringComparison.OrdinalIgnoreCase))
        {
            StartNewGameSession();
        }
        else if (request.Command.Equals("save", StringComparison.OrdinalIgnoreCase))
        {
            string path = Path.GetFullPath(request.Path ?? options.SavePath);
            if (playerSaves.ContainsPath(path))
            {
                throw new ArgumentException("Research checkpoints cannot target the player-saves directory.");
            }
            SaveCheckpoint(path);
        }
        else if (request.Command.Equals("capture", StringComparison.OrdinalIgnoreCase))
        {
            pendingCapture = Path.GetFullPath(request.Path ?? Path.Combine(options.ControlDirectory!, "captures", Path.GetFileName(request.Id) + ".png"));
        }
        else if (request.Command.Equals("draft", StringComparison.OrdinalIgnoreCase))
        {
            if (world?.CurrentDialogue is not DialogueState dialogue)
            {
                throw new InvalidOperationException("No dialogue is open.");
            }
            if (request.Name is string name)
            {
                if (!dialogue.IsNamePrompt || !GameNames.IsValidDraft(name))
                {
                    throw new ArgumentException("Name drafts require a name prompt and at most twelve printable characters.");
                }
                enteredName = name;
                if (gameMenus?.IsNamePrompt == true)
                {
                    gameMenus.SetNameDraft(name);
                }
            }
            if (request.Selection is int selection)
            {
                if (dialogue.FirstChoice is not int first || dialogue.LastChoice is not int last || selection < first || selection > last)
                {
                    throw new ArgumentOutOfRangeException(nameof(request), "Selection is outside the current choice lines.");
                }
                selectedChoice = selection;
            }
        }
        else if (request.Command.Equals("debug-scene", StringComparison.OrdinalIgnoreCase))
        {
            if (session.State.Scene == SceneKind.Title)
            {
                throw new InvalidOperationException("A field is required for debug presentation overrides.");
            }
            world ??= CreateWorld();
            world.ApplyDebug(request.Debug ?? throw new ArgumentException("A typed Debug payload is required."));
        }
        else if (request.Command.Equals("action", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("advance", StringComparison.OrdinalIgnoreCase))
        {
            if (startup.Active && request.Action is { Kind: PlayerActionKind.Confirm or PlayerActionKind.Cancel } startupAction)
            {
                startup.HandleInput(startupAction.Kind == PlayerActionKind.Confirm, startupAction.Kind == PlayerActionKind.Cancel);
                ReleaseStartupStage();
                return ControlStatus(request.Compact);
            }
            if (world?.Tutorial is NativeTutorialPlayer player)
            {
                PlayerAction tutorialAction = request.Command.Equals("advance", StringComparison.OrdinalIgnoreCase) ? new PlayerAction(PlayerActionKind.Wait, Frames: request.Frames) : request.Action ?? throw new ArgumentException("An action payload is required.");
                if (tutorialAction.Kind is not (PlayerActionKind.Wait or PlayerActionKind.Confirm or PlayerActionKind.Cancel))
                {
                    throw new InvalidOperationException("The native tutorial owns input until its original close boundary.");
                }
                PresentTutorial(player);
                ApplyTutorialInput(tutorialAction);
                return ControlStatus(request.Compact);
            }
            if (gameMenus?.IsOpen == true || modelBrowser is not null || sceneInspector is not null)
            {
                throw new InvalidOperationException("Close the UI before advancing the game-owned simulation.");
            }
            if (session.State.Scene == SceneKind.Title)
            {
                throw new InvalidOperationException("Start a new game before applying player actions.");
            }
            if (fieldFaulted)
            {
                throw new InvalidOperationException("The VM stopped at a script fault. Resume a safe checkpoint or start a new game.");
            }
            world ??= CreateWorld();
            world.EnsureField();
            PlayerAction action = request.Command.Equals("advance", StringComparison.OrdinalIgnoreCase) ? new PlayerAction(PlayerActionKind.Wait, Frames: request.Frames) : request.Action ?? throw new ArgumentException("An action payload is required.");
            PlayerActionResult result;
            try
            {
                result = world.Apply(action);
                metrics.Simulate(result.FramesAdvanced);
            }
            catch (EventScriptException exception)
            {
                fieldFaulted = true;
                error = exception.Message;
                throw;
            }
            if (result.Accepted && action.Kind is PlayerActionKind.Confirm or PlayerActionKind.Choose or PlayerActionKind.EnterName or PlayerActionKind.Cancel)
            {
                session.SetDialogueDraft(null);
            }
            return new { ActionResult = result, Status = ControlStatus(request.Compact) };
        }
        else if (!request.Command.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Commands: status, new-game, action, advance, draft, save, save-for-update, cancel-update, quit, capture, debug-scene.");
        }
        return ControlStatus(request.Compact);
    }

    private object ControlStatus(bool compact = false)
    {
        return new
        {
            session.State.Scene,
            Startup = new { Stage = startup.Stage.ToString(), Seconds = startup.Elapsed.TotalSeconds, HasMovieFrame = startupMovie?.HasFrame == true, Configured = startup.IsConfigured, Policy = startup.Policy },
            session.State.FieldId,
            FieldName = field?.Id == session.State.FieldId ? field.Name : session.State.FieldName,
            WindowTitle = Window.Title,
            session.State.MovieIndex,
            session.State.MovieSeconds,
            session.State.MoviePlayback,
            MoviePresentation = new
            {
                HasFrame = movie?.HasFrame == true,
                DecodedFrame = compact ? null : movie?.InspectDecodedFrame(),
                CanPresentCurrentField = world?.CanPresentCurrentField,
                FieldCallbackClock = events?.Scene.FieldCallbackClock,
                MovieMaskPlayback = events?.Scene.NativeMovieMask?.Playback,
                FieldGpuPresented = fieldGpuPresented,
                FieldGpuPresentationVersion = fieldGpu?.PresentationVersion
            },
            ExamTimer = session.State.ExamTimer is NativeExamTimerState timer ? new { timer.DisplayEnabled, timer.DisplayX, timer.DisplayY, Text = timer.FieldDisplayText(), RemainingMinimum = timer.Alternatives.Min(alternative => alternative.Remaining), RemainingMaximum = timer.Alternatives.Max(alternative => alternative.Remaining) } : null,
            session.State.FieldArrival,
            session.State.WorldArrival,
            World = session.State.World,
            FieldFaulted = fieldFaulted,
            FieldRenderFaulted = fieldRenderFaulted,
            UpdatePaused = updateSave.Paused,
            LastFault = events?.LastFault,
            Performance = metrics.Latest,
            EventSpeedMultiplier = gameplayClock.SpeedMultiplier,
            DebugPresentation = session.State.DebugView,
            GameUi = GameUiStatus(),
            Battle = BattleStatus(),
            DebugActionLog = world?.DebugActions,
            Trigger = world?.NextTrigger is PlayerTrigger trigger ? trigger with { Text = trigger.Text is string triggerText ? DialogueTextFormatter.Format(triggerText, events!.Scene.Names, events.MessageVariables, keyboard.NativeButtonBindingHint, world?.TextTerms) : null } : null,
            Dialogue = world?.CurrentDialogue is DialogueState current ? current with { Text = DialogueTextFormatter.Format(current.Text, events!.Scene.Names, events.MessageVariables, keyboard.NativeButtonBindingHint, world?.TextTerms) } : null,
            Dialogues = session.State.Scene == SceneKind.WorldMap ? world?.CurrentDialogue is { } worldDialogue ? new[] { worldDialogue } : [] : events?.Dialogues.OrderBy(dialogue => dialogue.Channel).Select(dialogue => dialogue with { Text = DialogueTextFormatter.Format(dialogue.Text, events.Scene.Names, events.MessageVariables, keyboard.NativeButtonBindingHint, world?.TextTerms) }).ToArray(),
            RawDialogueText = world?.CurrentDialogue?.Text,
            IsUserControlEnabled = session.State.Scene == SceneKind.WorldMap || events?.IsUserControlEnabled == true,
            CameraIndex = events?.Scene.CameraIndex,
            SceneRenderer = options.SoftwareRenderer ? "software-diagnostic" : "gpu",
            WorldRenderTarget = world is null ? null : new { Width = world.RenderWidth, Height = world.RenderHeight, Limit = options.WorldRenderLimit },
            LockedTriangles = events?.Scene.LockedTriangles,
            Actors = events?.Actors,
            Names = events?.Scene.Names,
            Party = events?.Scene.Party,
            Music = new { Loaded = events?.Scene.LoadedMusic, Playing = events?.Scene.MusicPlaying, Volume = events?.Scene.MusicVolume },
            FieldAudio = new { Effects = options.AudioEnabled ? "SDL3 PCM" : "Muted", Music = options.AudioEnabled ? "SGT/DLS + PCM" : "Muted", Error = fieldAudio?.Error, Resources = events?.Scene.LoadedSoundEffects, Requests = events?.Scene.SoundRequests, GlobalRequests = events?.Scene.GlobalSoundRequests, GlobalVoiceAttempts = events?.Scene.GlobalSoundLatestAttempts },
            Coverage = compact ? null : events?.Coverage,
            Decisions = compact ? null : events?.Decisions,
            DialogueOutcomes = compact ? null : events?.DialogueOutcomes,
            ActionLog = compact ? null : events?.ActionLog,
            PartyMenu = events?.PartyMenu,
            PartyMenuCandidates = events?.PartyMenuCandidates,
            TraceCounts = new { Coverage = events?.Coverage.Count ?? 0, Decisions = events?.Decisions.Count ?? 0, DialogueOutcomes = events?.DialogueOutcomes.Count ?? 0, Actions = events?.ActionLog.Count ?? 0 },
            Draft = new { SelectedChoice = selectedChoice, EnteredName = enteredName },
            CapturePath = pendingCapture,
            Error = error
        };
    }

    private void UploadField(TimeSpan elapsed, float worldInterpolation = 1)
    {
        if (events?.PartyMenu is not null)
        {
            return;
        }
        if (session.State.Scene != SceneKind.WorldMap && FieldPresentationReadiness.HasPendingInitialization(world!.Events.Scene.NativeFieldInitialization))
        {
            return;
        }
        if (world!.WorldScene?.IsPresentationDeferred == true)
        {
            return;
        }
        Rectangle viewport = GraphicsDevice.Viewport.Bounds;
        PixelBounds target = session.State.Scene == SceneKind.Field ? ViewportLayout.FieldRenderTarget(viewport.Width, viewport.Height, options.WorldRenderLimit) : ViewportLayout.WorldRenderTarget(viewport.Width, viewport.Height, options.WorldRenderLimit, session.State.Scene == SceneKind.WorldMap);
        world!.SetRenderSize(target.Width, target.Height);
        if (fieldGpu is null && (fieldTexture.Width != target.Width || fieldTexture.Height != target.Height))
        {
            Texture2D next = new(GraphicsDevice, target.Width, target.Height);
            fieldTexture.Dispose();
            fieldTexture = next;
        }
        long started = Stopwatch.GetTimestamp();
        bool smoothField = session.State.Scene == SceneKind.Field && uiPreferences.FieldFps == 60 && !options.ControlStep;
        float fieldInterpolation = smoothField ? (float)Math.Clamp(fieldSeconds * EventEngine.SimulationFps, 0, 1) : 1;
        byte[] pixels = world.Render(elapsed, uiPreferences.RareCardMarkers, fieldGpu, worldInterpolation, fieldInterpolation, smoothField);
        double renderMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        if (fieldGpu is null)
        {
            fieldTexture.SetData(pixels);
        }
        fieldGpuPresented = fieldGpu is not null;
        metrics.Render(renderMilliseconds, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    protected override void Draw(GameTime gameTime)
    {
        authoringHits.Clear();
        authoringDrawGroup = 0;
        uiCanvas?.BeginFrame(gameTime.TotalGameTime);
        if (modelBrowser is not null)
        {
            RenderModelBrowserPreview(gameTime.TotalGameTime);
            BeginMeasuredFrame();
            GraphicsDevice.Clear(new Color(12, 14, 20));
            text.BeginFrame();
            batch.Begin(samplerState: SamplerState.LinearClamp);
            DrawModelBrowserUi(GraphicsDevice.Viewport.Bounds);
            DrawFps(GraphicsDevice.Viewport.Bounds);
            DrawGameConfirmation(GraphicsDevice.Viewport.Bounds);
            batch.End();
            CapturePresentedFrame(gameTime);
            base.Draw(gameTime);
            metrics.Draw();
            return;
        }
        if (battleEntryTransition is not null)
        {
            DrawBattleEntry(gameTime);
            return;
        }
        UpdateLocationWindowTitle();
        // Catch-up updates advance the VM only. Rasterize once for the frame actually presented.
        if (!updateSave.Paused && ActiveBattle is null && world?.ActiveTripleTriad is null && gameMenus?.IsOpen != true && !fieldFaulted && !fieldRenderFaulted && world is not null && (session.State.Scene == SceneKind.WorldMap || world.CanPresentCurrentField))
        {
            try
            {
                switch (FieldPresentationReadiness.SelectRenderer(session.State.Scene, world.CanPresentCurrentField, movie?.HasFrame == true))
                {
                    case FieldPresentationRenderer.Movie:
                        byte[] moviePixels = world.RenderMovieFrame(movie!.DecodedPixels, movie.Texture.Width, movie.Texture.Height, fieldGpu);
                        if (fieldGpu is null)
                        {
                            if (fieldTexture.Width != world.RenderWidth || fieldTexture.Height != world.RenderHeight)
                            {
                                fieldTexture.Dispose();
                                fieldTexture = new Texture2D(GraphicsDevice, world.RenderWidth, world.RenderHeight);
                            }
                            fieldTexture.SetData(moviePixels);
                        }
                        fieldGpuPresented = fieldGpu is not null;
                        break;
                    case FieldPresentationRenderer.Field:
                        UploadField(gameplayClock.Total, options.ControlStep ? 1 : worldSimulationClock.Fraction);
                        break;
                }
                if (worldPresentationDeferred)
                {
                    worldPresentationDeferred = false;
                    error = null;
                }
            }
            catch (NativeWorldPresentationDeferredException exception)
            {
                fieldGpu?.Abort();
                if (!worldPresentationDeferred || error != exception.Message)
                {
                    Console.Error.WriteLine(exception);
                }
                error = exception.Message;
                worldPresentationDeferred = true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                fieldFaulted = true;
                fieldRenderFaulted = true;
                fieldGpu?.Abort();
                ReportAutoPlayHostFailure(exception);
                Console.Error.WriteLine(exception);
            }
        }
        if (ActiveBattle is not null && !startup.Active && !autoPlayFailureHeld && world?.ActiveTripleTriad is null)
        {
            PrepareBattleTexture(GraphicsDevice.Viewport.Bounds);
        }
        BeginMeasuredFrame();
        GraphicsDevice.Clear(Color.Black);
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        if (!startup.Active && session.State.Scene == SceneKind.Title)
        {
            titleScene ??= GraphicsHostReadiness.Initialize(() => new TitleSceneRenderer(GraphicsDevice));
            titleArtwork ??= new TitleArtworkRenderer(GraphicsDevice);
            titleScene.Draw(bounds, gameTime.TotalGameTime.TotalSeconds);
            titleArtwork.Draw(bounds, gameTime.TotalGameTime.TotalSeconds);
        }
        RenderModelBrowserPreview(gameTime.TotalGameTime);
        float uiScale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        text.BeginFrame();
        batch.Begin(samplerState: SamplerState.LinearClamp);
        if (startup.Active)
        {
            DrawStartup(bounds);
        }
        else if (!autoPlayFailureHeld && world?.ActiveTripleTriad is { } cards)
        {
            Memoria.FF8.Ui.UiView view = cards.BuildView();
            view.Resize(bounds.Width, bounds.Height);
            view.Draw(uiCanvas!, uiTheme);
        }
        else if (!autoPlayFailureHeld && ActiveBattle is not null)
        {
            DrawBattle(bounds);
        }
        else if (session.State.Scene == SceneKind.Title)
        {
            DrawTitle(bounds);
        }
        else if (session.State.Scene == SceneKind.Movie && movie?.HasFrame == true)
        {
            if (!fieldRenderFaulted)
            {
                if (world?.Field is not null)
                {
                    PixelBounds fit = ViewportLayout.Fit(PresentedFieldTexture.Width, PresentedFieldTexture.Height, bounds.Width, bounds.Height);
                    batch.Draw(PresentedFieldTexture, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White);
                }
                else
                {
                    movie.Draw(batch, bounds);
                }
            }
        }
        else if (session.State.Scene == SceneKind.WorldMap || session.State.Scene == SceneKind.Field && world?.Field is not null)
        {
            Rectangle fieldBounds = sceneInspector is null ? bounds : SceneInspectorPreviewBounds(bounds);
            PixelBounds fit = ViewportLayout.Fit(PresentedFieldTexture.Width, PresentedFieldTexture.Height, fieldBounds.Width, fieldBounds.Height);
            batch.Draw(PresentedFieldTexture, new Rectangle(fieldBounds.X + fit.X, fieldBounds.Y + fit.Y, fit.Width, fit.Height), Color.White);
        }
        if (sceneInspector is null && events is not null && (session.State.Scene == SceneKind.WorldMap || field?.Id == session.State.FieldId && session.State.Scene is SceneKind.Field or SceneKind.Movie) && session.State.DebugView?.DialogueVisible != false)
        {
            PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
            foreach (DialogueState dialogue in DialoguePresentation.DrawOrder(VisibleDialogues))
            {
                DrawDialogueUi(bounds, viewport, dialogue, gameTime.TotalGameTime.TotalSeconds);
            }
        }
        if (sceneInspector is null && ActiveBattle is null && session.State.Scene is SceneKind.Field or SceneKind.Movie && session.State.FieldBars is { } bars)
        {
            DrawFieldBars(bounds, bars);
        }
        if (sceneInspector is null && ActiveBattle is null && session.State.Scene == SceneKind.Field && session.State.ExamTimer is { DisplayEnabled: true } timer)
        {
            DrawTimerUi(bounds, timer);
        }
        if (sceneInspector is null && ActiveBattle is null && session.State.Scene != SceneKind.Title && session.State.SalaryPresentation is { Scale9A: > 0 } salary)
        {
            DrawSalaryUi(bounds, salary);
        }
        if (!BattleModalBlocksGameplay && sceneInspector is null && events?.PartyMenu is not null)
        {
            DrawPartyMenu(bounds);
        }
        MovieSkipHint.Draw(uiCanvas!, new Memoria.FF8.Ui.UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), session.State.Scene, movieSkipGesture, keyboard.BindingPrompt(ControlAction.Cancel), uiPreferences.UiFontFamily, LocalizeUiText);
        if (error is not null)
        {
            DrawNoticeUi(bounds, error, true);
        }
        DrawGameUi(bounds);
        if (saveMenu.IsOpen)
        {
            DrawPlayerSaveMenu(bounds, uiScale);
        }
        else if (saveNoticeSeconds > 0 && saveNotice is not null)
        {
            DrawNoticeUi(bounds, saveNotice(), false);
        }
        DrawAutoPlayStatus(bounds);
        if (userPaused && sceneInspector is null)
        {
            DrawNoticeUi(bounds, "Paused", true);
        }
        DrawBattleDebugUi(bounds);
        DrawSceneInspectorUi(bounds);
        DrawLocalizationAuthoring(bounds);
        DrawModelBrowserUi(bounds);
        DrawFps(bounds);
        DrawGameConfirmation(bounds);
        batch.End();
        CapturePresentedFrame(gameTime);
        base.Draw(gameTime);
        metrics.Draw();
    }

    private void CapturePresentedFrame(GameTime gameTime)
    {
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        string? capturePath = pendingCapture ?? (!frameCaptured && gameTime.TotalGameTime.TotalSeconds >= options.CaptureAfterSeconds ? options.CaptureFramePath : null);
        if (capturePath is not null)
        {
            var colors = new Color[bounds.Width * bounds.Height];
            GraphicsDevice.GetBackBufferData(colors);
            using var capture = new Texture2D(GraphicsDevice, bounds.Width, bounds.Height);
            capture.SetData(colors);
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
            using Stream output = File.Create(capturePath);
            capture.SaveAsPng(output, bounds.Width, bounds.Height);
            frameCaptured = true;
            pendingCapture = null;
            Console.WriteLine($"captured={capturePath} frame={updateFrames}");
        }
    }

    private void DrawTitle(Rectangle bounds)
    {
        DrawTitleUi(bounds);
    }

    private void DrawPlayerSaveMenu(Rectangle bounds, float scale)
    {
        DrawPlayerSavesUi(bounds);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
#if MEMORIA_NATIVE
            nativeBackBuffer?.Dispose();
#endif
            confirmationOverlay.Dispose();
            if (ModelBrowserDialogs.Confirmation?.Target == this)
            {
                ModelBrowserDialogs.Confirmation = null;
            }
            desktopDialogs.Dispose();
            dialogContext.Dispose();
            if (ReferenceEquals(SynchronizationContext.Current, dialogContext))
            {
                SynchronizationContext.SetSynchronizationContext(previousDialogContext);
            }
            focusDisposed = true;
            DisposeBattleEntry();
            DisposeStartup();
            DisposeModelBrowser();
            titleArtwork?.Dispose();
            gameMenus?.DisconnectMods();
            titleScene?.Dispose();
            controller?.Dispose();
            fieldAudio?.Dispose();
            movie?.Dispose();
            world?.Dispose();
            movieClock.Dispose();
            DisposeAchievements();
            DisposeDiagnosticBattleImage();
            diagnosticTelemetry?.Dispose();
            presentationPacer.Dispose();
            authoringTextInput?.Dispose();
            authoringClipboard.Dispose();
            localizationWorkbench.Dispose();
            authoringSelection?.Inputs?.Dispose();
            authoringCancellation.Cancel();
            authoringCancellation.Dispose();
            Memoria.FF8.Resources.Localization.LocalizationTextTrace.Enabled = false;
            if (authoringTermsLoading is { } pendingTerms)
            {
                try
                {
                    pendingTerms.GetAwaiter().GetResult();
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
                {
                    authoringNotice = exception.Message;
                }
            }
            nativeTextFont?.Dispose();
            resourceHost?.Dispose();
            DisposeBattlePresentation();
            DisposeFrameRate();
            fieldTexture?.Dispose();
            fieldGpu?.Dispose();
            battleGpu?.Dispose();
            uiCanvas?.Dispose();
            pixel?.Dispose();
            text?.Dispose();
            batch?.Dispose();
        }
        base.Dispose(disposing);
    }
}
