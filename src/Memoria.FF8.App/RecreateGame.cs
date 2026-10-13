using System.Diagnostics;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed class RecreateGame : Game
{
    private readonly LaunchOptions options;
    private readonly GameSession session;
    private readonly GraphicsDeviceManager graphics;
    private readonly KeyboardInput keyboard = new();
    private readonly MovieClock movieClock;
    private readonly PlayerSaveStore playerSaves;
    private readonly PlayerSaveMenu saveMenu;
    private SpriteBatch batch = null!;
    private SystemTextRenderer text = null!;
    private Texture2D pixel = null!;
    private MoviePresentation? movie;
    private int movieIndex = -1;
    private int titleSelection;
    private string? error;
    private GameWorld? world;
    private FieldAssets? field => world?.Field;
    private EventEngine? events => world?.Events;
    private Texture2D fieldTexture = null!;
    private DialogueState? presentedDialogue;
    private int selectedChoice;
    private string enteredName = "Squall";
    private int updateFrames;
    private bool frameCaptured;
    private bool fieldFaulted;
    private bool fieldRenderFaulted;
    private bool shakeRenderDeferred;
    private double fieldSeconds;
    private ControlQueue? controls;
    private string? pendingCapture;
    private readonly GameLoopMetrics metrics = new();
    private string? saveNotice;
    private double saveNoticeSeconds;

    public RecreateGame(LaunchOptions options, GameSession session)
    {
        this.options = options;
        this.session = session;
        movieClock = new MovieClock(session, options.GameDirectory, options.MediaToolsDirectory);
        playerSaves = new PlayerSaveStore(options.SavePath);
        saveMenu = new PlayerSaveMenu(playerSaves);
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = options.Width, PreferredBackBufferHeight = options.Height, SynchronizeWithVerticalRetrace = true };
        Window.Title = "Memoria - Final Fantasy VIII";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 60);
        InactiveSleepTime = TimeSpan.Zero;
        Window.TextInput += OnTextInput;
        session.StateChanged += state => Console.WriteLine($"scene={state.Scene} field={state.FieldId}:{state.FieldName} movie={state.MovieIndex}");
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        text = new SystemTextRenderer(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        fieldTexture = new Texture2D(GraphicsDevice, FieldRenderer.Width, FieldRenderer.Height);
        if (options.ControlDirectory is not null)
        {
            controls = new ControlQueue(options.ControlDirectory, DispatchControl);
        }
        Console.WriteLine($"graphics={GraphicsDevice.Adapter.Description} cwd={Environment.CurrentDirectory}");
    }

    protected override void Update(GameTime gameTime)
    {
        long updateStarted = Stopwatch.GetTimestamp();
        InputFrame input = keyboard.Read(IsActive);
        controls?.Poll();
        if (!saveMenu.IsOpen)
        {
            world?.AdvanceDebug(gameTime.ElapsedGameTime);
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
        if (UpdatePlayerSaves(input))
        {
            base.Update(gameTime);
            metrics.Update(updateStarted);
            return;
        }
        if (session.State.Scene == SceneKind.Title)
        {
            if (input.MenuVertical != 0)
            {
                titleSelection = input.MenuVertical > 0 ? 1 : 0;
            }
            if (input.Confirm)
            {
                if (titleSelection == 0)
                {
                    StartNewGameSession();
                }
                else
                {
                    OpenPlayerSaveMenu(PlayerSaveMenuMode.Load);
                }
            }
            if (input.Cancel)
            {
                Exit();
            }
        }
        else if (session.State.Scene == SceneKind.Movie)
        {
            UpdateMovie(gameTime, input);
        }
        else if (session.State.Scene == SceneKind.WorldMap)
        {
            UpdateWorldMap(gameTime, input);
        }
        else
        {
            UpdateField(gameTime, input);
        }
        if (session.State.Scene != SceneKind.Movie && movie is not null)
        {
            movie.Dispose();
            movie = null;
            movieIndex = -1;
        }
        base.Update(gameTime);
        metrics.Update(updateStarted);
    }

    private void UpdateMovie(GameTime gameTime, InputFrame input)
    {
        try
        {
            if (input.Skip)
            {
                if (world is not null)
                {
                    world.Apply(new PlayerAction(PlayerActionKind.SkipMovie));
                }
                else
                {
                    movieClock.Skip();
                }
                return;
            }
            if (world is null && session.State.EventState is not null)
            {
                world = new GameWorld(session, options.GameDirectory, movieClock);
                world.EnsureField();
            }
            if (movie is null || movieIndex != session.State.MovieIndex)
            {
                movie?.Dispose();
                string directory = movieClock.ToolsDirectory;
                string path = Path.Combine(options.GameDirectory, "Data", "movies", $"disc00_{session.State.MovieIndex:00}h.avi");
                movie = new MoviePresentation(GraphicsDevice, directory, path, session.State.MovieSeconds, movieClock.Prepare());
                movieIndex = session.State.MovieIndex;
                error = null;
            }
            int playbackField = session.State.FieldId;
            int playbackMovie = session.State.MovieIndex;
            movie.Update((playback, seconds) =>
            {
                session.SetMoviePlayback(playback);
                session.SetMoviePosition(seconds);
                // MOVIE's ordinary callback presents frames0/1 through55A530
                // and the tail of4767B0; they do not own extra field callbacks.
                if (world is not null && !fieldFaulted && playback.FrameIndex >= 2)
                {
                    events!.Step();
                    metrics.Simulate(1);
                }
                return session.State.Scene == SceneKind.Movie && session.State.MovieIndex == playbackMovie && session.State.FieldId == playbackField;
            });
            if (session.State.Scene != SceneKind.Movie || session.State.MovieIndex != playbackMovie || session.State.FieldId != playbackField)
            {
                fieldSeconds = 0;
                return;
            }
            session.SetMoviePlayback(movie.Playback);
            session.SetMoviePosition(movie.PositionSeconds);
            if (movie.Error is string playbackError)
            {
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
            error = $"Movie playback failed: {exception.Message}\nSet --media-tools to the FFmpeg bin directory. Escape skips.";
        }
    }

    private void UpdateField(GameTime gameTime, InputFrame input)
    {
        if (fieldFaulted && field?.Id == session.State.FieldId)
        {
            return;
        }
        try
        {
            world ??= new GameWorld(session, options.GameDirectory, movieClock);
            if (world.EnsureField())
            {
                presentedDialogue = null;
                error = null;
                fieldFaulted = false;
                fieldRenderFaulted = false;
                Console.WriteLine($"loaded={field!.Id}:{field.Name} models={field.Models.Count} tiles={field.Background.Count}");
            }
            DialogueState? dialogue = events!.CurrentDialogue;
            if (dialogue != presentedDialogue)
            {
                presentedDialogue = dialogue;
                (selectedChoice, enteredName) = DialoguePresentation.ResolveDraft(dialogue, session.State.DialogueDraft, events.Scene.Names.GetValueOrDefault(0, "Squall"));
            }
            if (dialogue is { CanConfirm: true })
            {
                if (dialogue.FirstChoice is int first && dialogue.LastChoice is int last && input.MenuVertical != 0)
                {
                    selectedChoice = Math.Clamp(selectedChoice + input.MenuVertical, first, last);
                }
                if (dialogue.CanConfirm && (input.Confirm || input.Cancel && (dialogue.CancelChoice.HasValue || !dialogue.IsNamePrompt && dialogue.FirstChoice is null)))
                {
                    PlayerAction action = input.Cancel && !input.Confirm ? new PlayerAction(PlayerActionKind.Cancel) : dialogue.IsNamePrompt ? new PlayerAction(PlayerActionKind.EnterName, Text: enteredName) : dialogue.FirstChoice.HasValue ? new PlayerAction(PlayerActionKind.Choose, selectedChoice) : new PlayerAction(PlayerActionKind.Confirm);
                    if (world.Apply(action).Accepted)
                    {
                        session.SetDialogueDraft(null);
                    }
                }
            }
            else if (events.IsUserControlEnabled)
            {
                ActorState? player = events.Actors.FirstOrDefault(actor => actor.Active && actor.CharacterId == 0 && actor.Visible);
                if (player is not null)
                {
                    if (input.Confirm)
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
            fieldSeconds += options.ControlStep ? 0 : gameTime.ElapsedGameTime.TotalSeconds;
            while (fieldSeconds + 1e-9 >= 1d / EventEngine.SimulationFps)
            {
                fieldSeconds -= 1d / EventEngine.SimulationFps;
                if ((input.Horizontal != 0 || input.Vertical != 0) && events.IsUserControlEnabled && !events.IsDialogueBlocking)
                {
                    metrics.Simulate(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -input.Horizontal, Vertical: input.Vertical)).FramesAdvanced);
                }
                else
                {
                    events.Step();
                    metrics.Simulate(1);
                }
                if (session.State.Scene != SceneKind.Field || session.State.FieldId != field!.Id || events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
                {
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
        if (saveMenu.IsOpen || events?.CurrentDialogue?.IsNamePrompt != true)
        {
            return;
        }
        if (args.Character == '\b')
        {
            enteredName = enteredName.Length == 0 ? enteredName : enteredName[..^1];
        }
        else if (!char.IsControl(args.Character) && enteredName.Length < 12)
        {
            enteredName += args.Character;
        }
    }

    private void UpdateWorldMap(GameTime gameTime, InputFrame input)
    {
        if (fieldFaulted)
        {
            return;
        }
        try
        {
            world ??= new GameWorld(session, options.GameDirectory, movieClock);
            if (world.EnsureField())
            {
                presentedDialogue = null;
                fieldSeconds = 0;
                error = null;
                Console.WriteLine($"loaded=world entry={session.State.WorldArrival}");
            }
            fieldSeconds += options.ControlStep ? 0 : gameTime.ElapsedGameTime.TotalSeconds;
            while (fieldSeconds + 1e-9 >= 1d / WorldMapScene.SimulationFps)
            {
                fieldSeconds -= 1d / WorldMapScene.SimulationFps;
                PlayerAction action = input.Horizontal != 0 || input.Vertical != 0 ? new PlayerAction(PlayerActionKind.Move, Horizontal: input.Horizontal, Vertical: input.Vertical) : new PlayerAction(PlayerActionKind.Wait);
                metrics.Simulate(world.Apply(action).FramesAdvanced);
                if (session.State.Scene != SceneKind.WorldMap)
                {
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

    private void SaveCheckpoint(string path)
    {
        CaptureSession();
        session.Save(path);
    }

    private void CaptureSession()
    {
        if (session.State.Scene == SceneKind.WorldMap)
        {
            world?.EnsureField();
        }
        else if (events is not null && events.FieldId == session.State.FieldId && events.NextTrigger.Kind != PlayerTriggerKind.FieldTransition)
        {
            session.SetEventState(events.Capture());
            DialogueState? dialogue = events.CurrentDialogue;
            session.SetDialogueDraft(dialogue is null ? null : new DialogueDraft(dialogue.Channel, dialogue.MessageId, selectedChoice, enteredName));
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
            saveMenu.Update(input, SavePlayerSlot, LoadPlayerSlot, RecreatePlayerSlot);
            return true;
        }
        if (input.Save || input.QuickLoad)
        {
            try
            {
                if (input.QuickLoad)
                {
                    LoadPlayerSlot(0);
                }
                else
                {
                    SavePlayerSlot(0);
                }
            }
            catch (Exception exception)
            {
                ShowSaveNotice($"{(input.QuickLoad ? "Quick load" : "Quick save")} failed: {exception.Message}");
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

    private void SavePlayerSlot(int slot)
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
        playerSaves.Save(slot, session);
        ShowSaveNotice(slot == 0 ? "Quick saved." : $"Saved in {PlayerSaveStore.GetLabel(slot)}.");
    }

    private void LoadPlayerSlot(int slot)
    {
        GameState state = playerSaves.Load(slot);
        ValidatePlayerState(state);
        RestorePlayerState(state);
        ShowSaveNotice(slot == 0 ? "Quick loaded." : $"Loaded {PlayerSaveStore.GetLabel(slot)}.");
    }

    private void RecreatePlayerSlot(int slot)
    {
        GameState recreated = playerSaves.Recreate(slot, source =>
        {
            GameState candidate = GameWorld.RecreateSave(source, options.GameDirectory, options.MediaToolsDirectory);
            ValidatePlayerState(candidate);
            return candidate;
        });
        RestorePlayerState(recreated);
        ShowSaveNotice($"Recreated and loaded {PlayerSaveStore.GetLabel(slot)} from recorded New Game history.");
    }

    private void ValidatePlayerState(GameState state)
    {
        // Validate the VM and asset continuation before replacing the running game.
        var candidateSession = new GameSession(state);
        if (state.Scene != SceneKind.Movie || state.EventState is not null)
        {
            using var candidate = new GameWorld(candidateSession, options.GameDirectory, new MovieClock(candidateSession, options.GameDirectory, options.MediaToolsDirectory));
            candidate.EnsureField();
        }
    }

    private void RestorePlayerState(GameState state)
    {
        movie?.Dispose();
        movie = null;
        movieIndex = -1;
        world?.Dispose();
        world = null;
        session.Restore(state);
        presentedDialogue = null;
        fieldSeconds = 0;
        error = null;
        fieldFaulted = false;
        fieldRenderFaulted = false;
    }

    private void ShowSaveNotice(string message)
    {
        saveNotice = message;
        saveNoticeSeconds = 5;
    }

    private void StartNewGameSession()
    {
        movie?.Dispose();
        movie = null;
        movieIndex = -1;
        world?.Dispose();
        world = null;
        session.StartNewGame();
        presentedDialogue = null;
        fieldSeconds = 0;
        error = null;
        fieldFaulted = false;
        fieldRenderFaulted = false;
    }

    private object DispatchControl(ControlRequest request)
    {
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
            if (events?.CurrentDialogue is not DialogueState dialogue)
            {
                throw new InvalidOperationException("No dialogue is open.");
            }
            if (request.Name is string name)
            {
                if (!dialogue.IsNamePrompt || name.Length > 12 || name.Any(char.IsControl))
                {
                    throw new ArgumentException("Name drafts require a name prompt and at most twelve printable characters.");
                }
                enteredName = name;
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
            world ??= new GameWorld(session, options.GameDirectory, movieClock);
            world.ApplyDebug(request.Debug ?? throw new ArgumentException("A typed Debug payload is required."));
        }
        else if (request.Command.Equals("action", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("advance", StringComparison.OrdinalIgnoreCase))
        {
            if (session.State.Scene == SceneKind.Title)
            {
                throw new InvalidOperationException("Start a new game before applying player actions.");
            }
            if (fieldFaulted)
            {
                throw new InvalidOperationException("The VM stopped at a script fault. Resume a safe checkpoint or start a new game.");
            }
            world ??= new GameWorld(session, options.GameDirectory, movieClock);
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
            throw new ArgumentException("Commands: status, new-game, action, advance, draft, save, capture, debug-scene.");
        }
        return ControlStatus(request.Compact);
    }

    private object ControlStatus(bool compact = false)
    {
        return new
        {
            session.State.Scene,
            session.State.FieldId,
            FieldName = field?.Id == session.State.FieldId ? field.Name : session.State.FieldName,
            session.State.MovieIndex,
            session.State.MovieSeconds,
            session.State.MoviePlayback,
            ExamTimer = session.State.ExamTimer is NativeExamTimerState timer ? new { timer.DisplayEnabled, timer.DisplayX, timer.DisplayY, Text = timer.FieldDisplayText(), RemainingMinimum = timer.Alternatives.Min(alternative => alternative.Remaining), RemainingMaximum = timer.Alternatives.Max(alternative => alternative.Remaining) } : null,
            session.State.FieldArrival,
            session.State.WorldArrival,
            World = compact && session.State.World is WorldState worldState ? worldState with { Actions = [] } : session.State.World,
            WorldActionCount = session.State.World?.Actions.Count ?? 0,
            FieldFaulted = fieldFaulted,
            FieldRenderFaulted = fieldRenderFaulted,
            LastFault = events?.LastFault,
            Performance = metrics.Latest,
            DebugPresentation = session.State.DebugView,
            DebugActionLog = world?.DebugActions,
            Trigger = world?.NextTrigger is PlayerTrigger trigger ? trigger with { Text = trigger.Text is string triggerText ? DialogueTextFormatter.Format(triggerText, events!.Scene.Names) : null } : null,
            Dialogue = events?.CurrentDialogue is DialogueState current ? current with { Text = DialogueTextFormatter.Format(current.Text, events.Scene.Names) } : null,
            Dialogues = events?.Dialogues.OrderBy(dialogue => dialogue.Channel).Select(dialogue => dialogue with { Text = DialogueTextFormatter.Format(dialogue.Text, events.Scene.Names) }).ToArray(),
            RawDialogueText = events?.CurrentDialogue?.Text,
            IsUserControlEnabled = session.State.Scene == SceneKind.WorldMap || events?.IsUserControlEnabled == true,
            CameraIndex = events?.Scene.CameraIndex,
            WorldRenderTarget = world is null ? null : new { Width = world.RenderWidth, Height = world.RenderHeight, Limit = options.WorldRenderLimit },
            LockedTriangles = events?.Scene.LockedTriangles,
            Actors = events?.Actors,
            Names = events?.Scene.Names,
            Party = events?.Scene.Party,
            Music = new { Loaded = events?.Scene.LoadedMusic, Playing = events?.Scene.MusicPlaying, Volume = events?.Scene.MusicVolume },
            FieldAudio = new { Effects = "Muted", Music = "Unsupported", Resources = events?.Scene.LoadedSoundEffects, Requests = events?.Scene.SoundRequests, GlobalRequests = events?.Scene.GlobalSoundRequests, GlobalVoiceAttempts = events?.Scene.GlobalSoundVoices },
            Coverage = compact ? null : events?.Coverage,
            Decisions = compact ? null : events?.Decisions,
            DialogueOutcomes = compact ? null : events?.DialogueOutcomes,
            ActionLog = compact ? null : events?.ActionLog,
            TraceCounts = new { Coverage = events?.Coverage.Count ?? 0, Decisions = events?.Decisions.Count ?? 0, DialogueOutcomes = events?.DialogueOutcomes.Count ?? 0, Actions = events?.ActionLog.Count ?? 0 },
            Draft = new { SelectedChoice = selectedChoice, EnteredName = enteredName },
            CapturePath = pendingCapture,
            Error = error
        };
    }

    private void UploadField(TimeSpan elapsed)
    {
        if (world!.WorldScene?.IsPresentationDeferred == true)
        {
            return;
        }
        Rectangle viewport = GraphicsDevice.Viewport.Bounds;
        PixelBounds target = ViewportLayout.WorldRenderTarget(viewport.Width, viewport.Height, options.WorldRenderLimit, session.State.Scene == SceneKind.WorldMap);
        world!.SetRenderSize(target.Width, target.Height);
        if (fieldTexture.Width != target.Width || fieldTexture.Height != target.Height)
        {
            Texture2D next = new(GraphicsDevice, target.Width, target.Height);
            fieldTexture.Dispose();
            fieldTexture = next;
        }
        long started = Stopwatch.GetTimestamp();
        byte[] pixels = world.Render(elapsed);
        double renderMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        fieldTexture.SetData(pixels);
        metrics.Render(renderMilliseconds, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Catch-up updates advance the VM only. Rasterize once for the frame actually presented.
        if (!fieldRenderFaulted && world is not null && (session.State.Scene == SceneKind.WorldMap || world.Field is not null && (session.State.Scene == SceneKind.Field || session.State.Scene == SceneKind.Movie && movie?.HasFrame != true)))
        {
            try
            {
                UploadField(gameTime.TotalGameTime);
                if (shakeRenderDeferred)
                {
                    shakeRenderDeferred = false;
                    error = null;
                }
            }
            catch (NativeShakeRenderException exception)
            {
                // Uncertain camera pixels do not stop the original VM/callback history.
                error = exception.Message;
                shakeRenderDeferred = true;
            }
            catch (NativeWorldPresentationDeferredException exception)
            {
                error = exception.Message;
                shakeRenderDeferred = true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                fieldFaulted = true;
                fieldRenderFaulted = true;
                Console.Error.WriteLine(exception);
            }
        }
        GraphicsDevice.Clear(Color.Black);
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        float uiScale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        if (session.State.Scene == SceneKind.Title)
        {
            DrawTitle(bounds);
        }
        else if (session.State.Scene == SceneKind.Movie && movie?.HasFrame == true)
        {
            movie.Draw(batch, bounds);
            text.Draw(batch, "Escape / Space: skip", 16 * uiScale, new Vector2(24 * uiScale, bounds.Height - 34 * uiScale), Color.White * 0.7f);
        }
        else if (session.State.Scene == SceneKind.WorldMap || session.State.Scene is SceneKind.Field or SceneKind.Movie && world?.Field is not null)
        {
            PixelBounds fit = ViewportLayout.Fit(fieldTexture.Width, fieldTexture.Height, bounds.Width, bounds.Height);
            batch.Draw(fieldTexture, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White);
            if (session.State.Scene != SceneKind.WorldMap && events?.Scene.FadeOpacity > 0)
            {
                batch.Draw(pixel, bounds, Color.Black * events.Scene.FadeOpacity);
            }
        }
        if (events is not null && field?.Id == session.State.FieldId && session.State.Scene is SceneKind.Field or SceneKind.Movie && session.State.DebugView?.DialogueVisible != false)
        {
            PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
            float dialogueScale = viewport.Width / 320f;
            float fontSize = 8 * dialogueScale;
            foreach (DialogueState dialogue in events.Dialogues.OrderBy(dialogue => dialogue.Channel))
            {
                string displayed = DialogueTextFormatter.Format(dialogue.Text, events.Scene.Names);
                if (dialogue == events.CurrentDialogue && dialogue.IsNamePrompt)
                {
                    displayed += "\n" + enteredName + "_  (Enter: accept)";
                }
                else if (dialogue == events.CurrentDialogue && dialogue is { FirstChoice: int first, LastChoice: int last })
                {
                    displayed = string.Join('\n', displayed.Split('\n').Select((line, index) => index >= first && index <= last ? (index == selectedChoice ? "> " : "  ") + line : line));
                    displayed += "\nArrows: select   Enter: confirm";
                }
                DialogueWindow? window = dialogue.X == 0 && dialogue.Y == 0 ? events.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel) : null;
                float maximumWidth = Math.Clamp((window?.Width ?? 304) - 16, 1, 288) * dialogueScale;
                Vector2 measured = text.MeasureWrapped(displayed, fontSize, maximumWidth);
                PixelBounds panelBounds = DialoguePresentation.Bounds(dialogue, window, viewport, measured.X, measured.Y);
                Rectangle panel = new(panelBounds.X, panelBounds.Y, panelBounds.Width, panelBounds.Height);
                DrawPanel(panel);
                text.DrawWrapped(batch, displayed, fontSize, new Vector2(panel.X + 8 * dialogueScale, panel.Y + 8 * dialogueScale), Math.Max(1, panel.Width - 16 * dialogueScale), Color.White);
            }
        }
        if (session.State.Scene == SceneKind.Field && session.State.ExamTimer is { DisplayEnabled: true } timer)
        {
            PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
            float timerScale = viewport.Width / 320f;
            text.Draw(batch, timer.FieldDisplayText(), 10 * timerScale, new Vector2(viewport.X + timer.DisplayX * timerScale, viewport.Y + timer.DisplayY * timerScale), Color.White);
        }
        if (error is not null)
        {
            Rectangle panel = DialogueBounds(bounds, uiScale);
            DrawPanel(panel);
            text.DrawWrapped(batch, error, 20 * uiScale, new Vector2(panel.X + 18 * uiScale, panel.Y + 18 * uiScale), panel.Width - 36 * uiScale, Color.White);
        }
        if (saveMenu.IsOpen)
        {
            DrawPlayerSaveMenu(bounds, uiScale);
        }
        else if (saveNoticeSeconds > 0 && saveNotice is not null)
        {
            Rectangle panel = new((int)(24 * uiScale), (int)(24 * uiScale), Math.Max(1, bounds.Width - (int)(48 * uiScale)), (int)(100 * uiScale));
            DrawPanel(panel);
            text.DrawWrapped(batch, saveNotice, 19 * uiScale, new Vector2(panel.X + 16 * uiScale, panel.Y + 14 * uiScale), panel.Width - 32 * uiScale, Color.White);
        }
        batch.End();
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
        base.Draw(gameTime);
        metrics.Draw();
    }

    private void DrawTitle(Rectangle bounds)
    {
        float scale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        batch.Draw(pixel, bounds, new Color(12, 17, 25));
        for (int index = 0; index < 12; index++)
        {
            batch.Draw(pixel, new Rectangle((int)(bounds.Width / 2f + (-240 + index * 42) * scale), (int)(bounds.Height * 0.18f + index * 7 * scale), Math.Max(1, (int)(4 * scale)), (int)(110 * scale)), new Color(115 + index * 8, 45 + index * 7, 40, 50));
        }
        text.DrawCentered(batch, "FINAL FANTASY VIII", 52 * scale, new Vector2(bounds.Width / 2f, bounds.Height * 0.245f), new Color(239, 232, 218));
        text.DrawCentered(batch, "MEMORIA", 19 * scale, new Vector2(bounds.Width / 2f, bounds.Height * 0.343f), new Color(167, 167, 160));
        Rectangle menu = new((int)(bounds.Width / 2f - 150 * scale), (int)(bounds.Height * 0.52f), (int)(300 * scale), (int)(144 * scale));
        DrawPanel(menu);
        text.Draw(batch, (titleSelection == 0 ? ">  " : "   ") + "New Game", 28 * scale, new Vector2(menu.X + 32 * scale, menu.Y + 22 * scale), titleSelection == 0 ? Color.White : Color.Gray);
        text.Draw(batch, (titleSelection == 1 ? ">  " : "   ") + "Continue", 28 * scale, new Vector2(menu.X + 32 * scale, menu.Y + 78 * scale), titleSelection == 1 ? Color.White : Color.Gray);
        text.DrawCentered(batch, "Arrows: select    Enter: confirm    F10: load", 16 * scale, new Vector2(bounds.Width / 2f, bounds.Height - 38 * scale), Color.Gray);
    }

    private void DrawPlayerSaveMenu(Rectangle bounds, float scale)
    {
        batch.Draw(pixel, bounds, Color.Black * 0.6f);
        Rectangle panel = new((int)(40 * scale), (int)(60 * scale), Math.Max(1, bounds.Width - (int)(80 * scale)), Math.Max(1, bounds.Height - (int)(120 * scale)));
        DrawPanel(panel);
        text.Draw(batch, saveMenu.Mode == PlayerSaveMenuMode.Save ? "Save Game" : "Load Game", 28 * scale, new Vector2(panel.X + 24 * scale, panel.Y + 18 * scale), Color.White);
        text.Draw(batch, $"Bank {saveMenu.Bank + 1} / {PlayerSaveStore.BankCount}    Page {saveMenu.Page + 1} / {PlayerSaveMenu.PageCount}", 18 * scale, new Vector2(panel.X + 24 * scale, panel.Y + 56 * scale), Color.Gray);
        if (saveMenu.QuickEntry is PlayerSaveEntry quick)
        {
            DrawPlayerSaveEntry(quick, panel, panel.Y + 86 * scale, scale);
        }
        IReadOnlyList<PlayerSaveEntry> visible = saveMenu.VisibleEntries;
        float firstY = saveMenu.QuickEntry is null ? 104 : 144;
        for (int index = 0; index < visible.Count; index++)
        {
            DrawPlayerSaveEntry(visible[index], panel, panel.Y + (firstY + index * 52) * scale, scale);
        }
        string instructions = "Up/Down: select   Left/Right: bank   PgUp/PgDn: page\nEnter: confirm   Escape: cancel\nF5: quick save   F9: quick load   F6: save   F10: load";
        if (saveMenu.Mode == PlayerSaveMenuMode.Load)
        {
            Rectangle action = new((int)(panel.X + 24 * scale), (int)(panel.Bottom - 184 * scale), Math.Max(1, panel.Width - (int)(48 * scale)), Math.Max(1, (int)(38 * scale)));
            DrawPanel(action);
            text.Draw(batch, "R: Recreate selected save from start", 20 * scale, new Vector2(action.X + 12 * scale, action.Y + 7 * scale), saveMenu.CanRecreate ? Color.White : Color.Gray);
            string detail = saveMenu.Message ?? saveMenu.RecreateUnavailableReason ?? "Rebuild saved progress with this engine, then replace and load this slot after success.";
            string displayed = detail.Length > 160 ? detail[..157] + "..." : detail;
            text.DrawWrapped(batch, displayed, 16 * scale, new Vector2(panel.X + 24 * scale, panel.Bottom - 138 * scale), panel.Width - 48 * scale, Color.White);
            text.DrawWrapped(batch, instructions, 18 * scale, new Vector2(panel.X + 24 * scale, panel.Bottom - 90 * scale), panel.Width - 48 * scale, Color.White);
        }
        else
        {
            if (saveMenu.Message is string message)
            {
                string displayed = message.Length > 140 ? message[..137] + "..." : message;
                instructions = displayed + "\n" + instructions;
            }
            text.DrawWrapped(batch, instructions, 18 * scale, new Vector2(panel.X + 24 * scale, panel.Bottom - 120 * scale), panel.Width - 48 * scale, Color.White);
        }
    }

    private void DrawPlayerSaveEntry(PlayerSaveEntry entry, Rectangle panel, float y, float scale)
    {
        Color color = entry.Slot == saveMenu.SelectedSlot ? Color.White : Color.Gray;
        string label = (entry.Slot == saveMenu.SelectedSlot ? ">  " : "   ") + entry.Label;
        if (entry.SavedAt is DateTime savedAt)
        {
            label += "    " + savedAt.ToString("yyyy-MM-dd HH:mm:ss");
        }
        text.Draw(batch, label, 22 * scale, new Vector2(panel.X + 24 * scale, y), color);
        string details = entry.Details.Length > 80 ? entry.Details[..77] + "..." : entry.Details;
        text.Draw(batch, details, 17 * scale, new Vector2(panel.X + 52 * scale, y + 27 * scale), color);
    }

    private static Rectangle DialogueBounds(Rectangle bounds, float scale)
    {
        int margin = Math.Max(1, (int)(24 * scale));
        int height = Math.Min(bounds.Height - 2 * margin, (int)(200 * scale));
        return new Rectangle(margin, bounds.Height - height - margin, Math.Max(1, bounds.Width - 2 * margin), Math.Max(1, height));
    }

    private void DrawPanel(Rectangle rectangle)
    {
        batch.Draw(pixel, rectangle, new Color(156, 156, 160));
        int border = Math.Max(1, (int)(2 * ViewportLayout.UiScale(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height)));
        batch.Draw(pixel, new Rectangle(rectangle.X + border, rectangle.Y + border, Math.Max(1, rectangle.Width - 2 * border), Math.Max(1, rectangle.Height - 2 * border)), new Color(16, 23, 43, 244));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            movie?.Dispose();
            world?.Dispose();
            fieldTexture?.Dispose();
            pixel?.Dispose();
            text?.Dispose();
            batch?.Dispose();
        }
        base.Dispose(disposing);
    }
}
