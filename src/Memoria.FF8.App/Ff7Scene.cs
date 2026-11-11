using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Media;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vector2N = System.Numerics.Vector2;

namespace Memoria.FF8;

internal sealed record Ff7MovieSource(string Path, MovieInformation Information, int NativeFrameCount, double LogicalFramesPerSecond, Func<int, PcMovieCamera?>? CameraAtFrame = null, Action<int, long>? PublishCameraFrame = null)
{
    public int CameraRecordCount { get; init; }
    public Func<int, long, PcMovieCamera?>? SnapshotCameraFrame { get; init; }
}

internal sealed partial class Ff7Scene : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly bool audioEnabled;
    private readonly Ff7Installation installation;
    private readonly string mediaToolsDirectory;
    private readonly Ff7ProjectionDepth projection;
    private readonly Func<int, Ff7MovieSource> resolveMovie;
    private IFf7ActorAppearance? appearance;
    private readonly Ff7FontRenderer font;
    private readonly Texture2D pixel;
    private readonly Texture2D titleArtwork;
    private readonly GpuTriangleRenderer triangles;
    private CombinedSessionSave envelope;
    private Ff7FieldRenderer? renderer;
    private long renderedGeneration = -1;
    private double tickRemainder;
    private int titleSelection;
    private bool debugMenu;
    private int debugSelection;
    private readonly NoticeWindow noticeWindow = new();
    private string? error;
    private bool focusPaused;

    public Ff7Scene(GraphicsDevice device, Ff7Installation installation, string mediaToolsDirectory, Ff7ProjectionDepth projection, Func<int, Ff7MovieSource> resolveMovie, CombinedSessionSave? envelope = null, IFf7ActorAppearance? appearance = null, bool audioEnabled = true)
    {
        this.device = device;
        this.audioEnabled = audioEnabled;
        this.installation = installation;
        this.mediaToolsDirectory = mediaToolsDirectory;
        this.projection = projection;
        this.resolveMovie = resolveMovie;
        this.appearance = appearance;
        this.envelope = envelope ?? new CombinedSessionSave { ActiveGame = SessionGame.Ff7 };
        font = new Ff7FontRenderer(device, Ff7FontResources.Load(installation));
        pixel = new Texture2D(device, 1, 1);
        pixel.SetData(new[] { Color.White });
        Ff7MenuResources menu = Ff7MenuResources.Load(installation);
        titleArtwork = CreateTexture(menu.GetRequiredTexture("buster.tex"));
        triangles = new GpuTriangleRenderer(device);
    }

    public Ff7Session? Session { get; private set; }
    public bool CanSave => error is null && Session?.CanSave == true;
    public bool IsTitle => Session is null;
    public bool UseHostDebugMenu { get; set; }
    public int RenderLimit { get; set; } = 2048;
    public bool IsDebugMenuOpen => debugMenu;
    internal void InvalidateGraphicsPresentation()
    {
        triangles.InvalidatePresentation();
    }

    public string? Error => error;
    public event Action<CombinedSessionSave>? SaveRequested;
    public event Action<InputFrame>? PlayerSaveRequested;
    public event Action<CombinedSessionSave>? WorldSwitchRequested;
    public event Action? ContinueRequested;
    public event Action? DebugMenuRequested;

    public void SuppressInitialPresses(KeyboardState keys)
    {
        sharedInput.Read(keys, false);
        sharedInput.SuppressHeldUiInput();
        pendingPressedKeys = 0;
    }

    public void ShowNotice(string message, double durationSeconds = NoticeWindow.DefaultDurationSeconds)
    {
        ShowNotice(() => message, durationSeconds);
    }

    public void ShowNotice(Func<string> message, double durationSeconds)
    {
        noticeWindow.Show(message, durationSeconds);
    }

    public void SetFocusPaused(bool paused)
    {
        focusPaused = paused;
        SetAudioPaused(paused || userPaused || debugMenu || error is not null);
        if (paused || userPaused || debugMenu || error is not null)
        {
            movie?.Suspend();
        }
        else
        {
            movie?.Resume();
        }
    }

    public void StartNewGame()
    {
        RunGuarded(() => Attach(Ff7Session.NewGame(installation)));
    }

    public void Continue(CombinedSessionSave saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        saved.Validate();
        Ff7SessionSave ff7 = saved.Ff7 ?? throw new InvalidDataException("This save contains no FFVII continuation.");
        Ff7Session restored = Ff7Session.Restore(installation, ff7);
        envelope = saved;
        Attach(restored, ff7.AudioPresentation, restoring: true);
    }

    public CombinedSessionSave Capture()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("The FFVII title has no gameplay continuation to save.");
        CombinedSessionSave saved = envelope with { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture() with { AudioPresentation = CaptureAudioPresentation() } };
        saved = CaptureMixedCombat(saved);
        saved.Validate();
        return saved;
    }

    public void Update(GameTime gameTime, KeyboardState keys, GamePadState pad = default)
    {
        try
        {
            ownedDialogueText?.BeginFrame();
            ownedDialogueCanvas?.BeginFrame(gameTime.TotalGameTime);
            SampleSharedInput(keys, pad);
            if (focusPaused || userPaused)
            {
                movieSkipGesture.Reset();
                return;
            }
            noticeWindow.Advance(gameTime.ElapsedGameTime);
            if (playerMenu?.IsCapturingControls != true && PlayerSaveRequested is not null && (semanticInput.Save || semanticInput.QuickLoad || semanticInput.SaveMenu || semanticInput.LoadMenu))
            {
                PlayerSaveRequested.Invoke(semanticInput);
                return;
            }
            if (sharedInput.DebugRoomRequested && playerMenu?.IsCapturingControls != true)
            {
                if (IsPlayerMenuOpen)
                {
                    ClosePlayerMenu();
                }
                pendingPressedKeys = 0;
                sharedInput.SuppressHeldUiInput();
                if (UseHostDebugMenu)
                {
                    DebugMenuRequested?.Invoke();
                    return;
                }
                debugMenu = !debugMenu;
                debugView = null;
                debugPointerPressed = true;
                SetAudioPaused(debugMenu);
                noticeWindow.Clear();
                if (debugMenu)
                {
                    movie?.Suspend();
                }
                else
                {
                    movie?.Resume();
                }
                return;
            }
            if (error is not null)
            {
                UpdateErrorPresentation();
                return;
            }
            if (debugMenu)
            {
                UpdateDebugMenu();
                return;
            }
            if (Session is null)
            {
                UpdateTitle();
                return;
            }
            if (Session.Battle is { } battle)
            {
                UpdateBattle(gameTime, battle);
                return;
            }
            if (UpdateShop(gameTime, keys, pad) || UpdatePlayerMenu(gameTime, keys, pad))
            {
                return;
            }
            if (Session.HasPendingTick)
            {
                Session.AdvanceTick(advanceClock: false);
                RememberReportCheckpoint();
                UpdateAudio();
                PublishCurrentMovieCameraFrame();
                return;
            }
            Session.AdvanceClock(gameTime.ElapsedGameTime);
            UpdateAudio();
            if (autoPlay?.Running != true || autoPlay.Paused)
            {
                if (Session.World is null && UpdateNamingMenu())
                {
                    return;
                }
                UpdateSessionInput();
            }
            UpdateMovie();
            UpdateMovieSkip(gameTime.ElapsedGameTime);
            SceneKind timingScene = Session.World is not null ? SceneKind.WorldMap : Session.Host.Process.Movie is { Playing: true } ? SceneKind.Movie : SceneKind.Field;
            tickRemainder += sharedClock.Advance(TimeSpan.FromSeconds(Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25)), timingScene).TotalSeconds;
            while (tickRemainder >= 1d / 30)
            {
                tickRemainder -= 1d / 30;
                if (Session.World is null)
                {
                    CapturePreviousActors();
                }
                if (autoPlay?.Running == true && !autoPlay.Paused)
                {
                    AdvanceAutoPlay();
                    if (error is not null)
                    {
                        break;
                    }
                }
                else
                {
                    ApplyLogicalInput();
                    Session.AdvanceTick(advanceClock: false);
                }
                if (Session.Battle is { } startedBattle)
                {
                    EnsureMixedBattle(startedBattle);
                }
                RememberReportCheckpoint();
                UpdateAudio();
                if (IsShopMenuPresentation || Session.Battle is not null)
                {
                    tickRemainder = 0;
                    break;
                }
            }
            PublishCurrentMovieCameraFrame();
        }
        catch (Exception exception) when (exception is FieldExecutionException or InvalidDataException or NotSupportedException or InvalidOperationException or IOException)
        {
            SetError(exception);
        }
    }

    public void Draw(SpriteBatch batch, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        Rectangle windowBounds = bounds;
        Memoria.FF8.Runtime.PixelBounds fit = Memoria.FF8.Runtime.ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
        bounds = new Rectangle(bounds.X + fit.X, bounds.Y + fit.Y, fit.Width, fit.Height);
        if (Session is not null && error is null)
        {
            PixelBounds target = ViewportLayout.FieldRenderTarget(windowBounds.Width, windowBounds.Height, RenderLimit);
            Rectangle renderBounds = new(0, 0, target.Width, target.Height);
            RunGuarded(() =>
            {
                if (Session.Battle is { } battle)
                {
                    RenderBattle(battle, renderBounds);
                }
                else
                {
                    RenderField(renderBounds);
                }
            });
        }
        batch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        try
        {
            if (Session is null)
            {
                DrawTitle(batch, bounds);
            }
            else
            {
                if (Session.World is null && Session.Battle is null)
                {
                    DrawMovie(batch, bounds);
                }
                if ((renderer is not null || Session.World is not null || Session.Battle is not null) && error is null)
                {
                    PixelBounds fieldFit = ViewportLayout.Fit(triangles.Target.Width, triangles.Target.Height, windowBounds.Width, windowBounds.Height);
                    batch.Draw(triangles.Target, new Rectangle(windowBounds.X + fieldFit.X, windowBounds.Y + fieldFit.Y, fieldFit.Width, fieldFit.Height), Color.White);
                }
                if (error is null)
                {
                    if (Session.Battle is { } battle)
                    {
                        DrawBattle(windowBounds, battle);
                    }
                    else
                    {
                        DrawDialogues(batch, bounds);
                    }
                    int worldFade = Session.World?.Process.FadeDisplayedAlpha ?? 0;
                    if (worldFade > 0)
                    {
                        batch.Draw(pixel, windowBounds, Color.Black * (worldFade / 255f));
                    }
                }
            }
            if (error is null)
            {
                DrawPlayerMenu(windowBounds);
                DrawNamingMenu(windowBounds);
                DrawShop(batch, windowBounds);
            }
            DrawMovieSkipHint(windowBounds);
            if (debugMenu)
            {
                DrawDebugMenu(batch, windowBounds);
            }
            if (!debugMenu && dialogueCanvas is not null && noticeWindow.Text is { } notice)
            {
                noticeWindow.Draw(dialogueCanvas, SharedTheme(), windowBounds.Width, windowBounds.Height, notice);
            }
            if (error is not null)
            {
                DrawErrorPresentation(windowBounds);
            }
        }
        catch (Exception exception) when (exception is FieldExecutionException or InvalidDataException or NotSupportedException or InvalidOperationException or IOException)
        {
            SetError(exception);
        }
        finally
        {
            batch.End();
        }
    }

    private void Attach(Ff7Session session, Ff7AudioPresentationState? savedAudio = null, bool restoring = false)
    {
        autoPlay = null;
        DisposeNamingPresentation();
        DisposePlayerMenu();
        DisposeShopMenu();
        StopAudio();
        ResetMixedCombat();
        Session?.Dispose();
        StopMovie();
        preparedMovieSource = null;
        preparedMovieIdentity = null;
        Session = session;
        restoredNativeBattle = session.Battle is { GuestVitals: null } nativeBattle ? nativeBattle : null;
        AttachCharacterIdentity(session);
        if (!restoring && MixedCombatEnabled)
        {
            session.SetFullCombatEnabled(true);
        }
        Session.Host.NativeBlinkCamera = BlinkCamera;
        Session.DialogueMessageSpeed = native => Ff7DialoguePresentationPolicy.MessageSpeed(native, dialoguePreferences?.Invoke().FieldMessageSpeed ?? 3);
        RestoreAudioPresentation(savedAudio, restoring);
        Session.PresentationCommand = PresentationCommand;
        renderedGeneration = -1;
        renderer = null;
        ResetBattlePresentation();
        ResetWorldPresentation();
        ResetInterpolationPresentation();
        dialogueTokens.Clear();
        commonDialoguePages.Clear();
        reportCheckpoint = null;
        error = null;
        noticeWindow.Clear();
        debugMenu = false;
        tickRemainder = 0;
        RememberReportCheckpoint();
    }

    private void RenderField(Rectangle bounds)
    {
        Ff7Session session = Session!;
        if (session.World is { } world)
        {
            RenderWorld(world, bounds);
            return;
        }
        if (renderer is null || renderedGeneration != session.FieldGeneration)
        {
            renderer = new Ff7FieldRenderer(session.Field, session.Models.Select((model, index) => (model, index)).ToDictionary(pair => pair.index, pair => pair.model), new(session.FieldNearPlane, projection.FarPlane), appearance);
            renderedGeneration = session.FieldGeneration;
        }
        Ff7FieldProcess state = session.Host.Process;
        PcFieldCamera? camera = CurrentMovieCamera();
        PcFieldCamera effectCamera = camera ?? session.Field.Cameras[0];
        Ff7ActorVisual[] actors = state.Actors.Select(PresentationActor).Select(actor => new Ff7ActorVisual(actor.Entity, actor.Model, checked(actor.X4096 + actor.OffsetX * 4096), checked(actor.Y4096 + actor.OffsetY * 4096), checked(actor.Z4096 + actor.OffsetZ * 4096), checked((byte)actor.Direction), actor.Animation, actor.AnimationFrame16, actor.Visible) { PartyCharacter = actor.PartyCharacter, AutomaticBlinkDisabled = actor.AutomaticBlinkDisabled, NativeEyeLeft = actor.NativeEyeLeft, NativeEyeRight = actor.NativeEyeRight, ModelEffects = ModelEffects(actor, effectCamera) }).ToArray();
        Ff7FieldRenderFrame frame = new(0, PresentationCameraOffset(state, camera), actors, state.BackgroundStates, state.Movie is { Playing: true }, camera, state.MovieBackgroundEnabled, state.PaletteRows, state.PaletteVersion) { FieldColorArgb = state.FadeState?.FieldColorArgb, QuadColorArgb = state.FadeState?.QuadColorArgb, QuadBlend = FadeQuadBlend(state.FadeState), PlayerPointer = PlayerPointer() };
        renderer.Render(triangles, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), frame);
    }

    internal static TriangleBlend FadeQuadBlend(Ff7FadeProcess? fade)
    {
        return fade?.QuadBlend switch
        {
            null or Ff7FadeQuadBlend.Alpha => TriangleBlend.Alpha,
            Ff7FadeQuadBlend.Additive => TriangleBlend.Add,
            _ => throw new NotSupportedException("The FFVII fade process requires an admitted native quad blend.")
        };
    }

    internal static Vector2N FieldCameraOffset(Ff7FieldProcess state, PcFieldCamera? movieCamera = null)
    {
        return Ff7NativeCamera.PresentationOffset(state, movieCamera);
    }

    private void PresentationCommand(FieldCommand command)
    {
        throw new NotSupportedException($"FFVII presentation does not implement {command.GetType().Name}.");
    }

    private void UpdateTitle()
    {
        int count = CanResumeMixedOrigin ? 3 : 2;
        titleSelection = Math.Min(titleSelection, count - 1);
        if (semanticInput.MenuVertical != 0)
        {
            titleSelection = (titleSelection + Math.Sign(semanticInput.MenuVertical) + count) % count;
        }
        if (semanticInput.Confirm)
        {
            if (titleSelection == 0)
            {
                StartNewGame();
            }
            else if (titleSelection == 1)
            {
                ContinueRequested?.Invoke();
            }
            else
            {
                ResumeMixedOrigin();
            }
        }
    }

    private void UpdateDebugMenu()
    {
        if (semanticInput.Cancel)
        {
            debugMenu = false;
            movie?.Resume();
            SetAudioPaused(false);
            return;
        }
        if (semanticInput.MenuVertical != 0)
        {
            int direction = semanticInput.MenuVertical < 0 ? -1 : 1;
            debugSelection = (debugSelection + direction + DebugCommandCount) % DebugCommandCount;
        }
        if (semanticInput.Confirm)
        {
            ActivateDebugSelection();
        }
        if (debugMenu)
        {
            UpdateDebugPointer();
        }
    }

    private void ActivateDebugSelection()
    {
        if (debugSelection == BattlePolicyDebugCommand)
        {
            if (Session is { } session)
            {
                session.SetFullCombatEnabled(!session.Progress.FullCombatEnabled);
                ShowNotice(session.Progress.FullCombatEnabled ? "Full combat is enabled for the next battle." : "Instant victory is enabled for the next battle.");
            }
            return;
        }
        if (debugSelection == RandomEncountersDebugCommand)
        {
            if (Session is { } session)
            {
                session.SetRandomEncountersEnabled(!session.Progress.RandomEncountersEnabled);
                ShowNotice(session.Progress.RandomEncountersEnabled ? "Native random encounters are enabled." : "Random encounters are disabled. Scripted battles remain enabled.");
            }
            return;
        }
        if (debugSelection == 3)
        {
            if (IsTitle)
            {
                ShowNotice("Start or continue a game before starting auto-play.");
                return;
            }
            ApplyAutoPlayCommand(autoPlay?.Running == true && !autoPlay.Paused ? "pause" : "start");
            debugMenu = false;
            SetAudioPaused(false);
            movie?.Resume();
            return;
        }
        if (debugSelection == 2)
        {
            ToggleDialoguePresentation();
            return;
        }
        if (!CanSave)
        {
            ShowNotice("Available only where saving is allowed.");
        }
        else if (debugSelection == 0)
        {
            SaveRequested?.Invoke(Capture());
        }
        else if (envelope.Ff8Payload is { Length: > 0 })
        {
            WorldSwitchRequested?.Invoke(Capture());
        }
        else
        {
            ShowNotice("No suspended FFVIII session is available.");
        }
    }

    private void DrawTitle(SpriteBatch batch, Rectangle bounds)
    {
        float artworkScale = Math.Min(160f / titleArtwork.Width, 144f / titleArtwork.Height);
        int artworkWidth = Math.Max(1, (int)(titleArtwork.Width * artworkScale));
        int artworkHeight = Math.Max(1, (int)(titleArtwork.Height * artworkScale));
        Rectangle image = Logical(new Rectangle((320 - artworkWidth) / 2, 20 + (144 - artworkHeight) / 2, artworkWidth, artworkHeight), bounds);
        batch.Draw(titleArtwork, image, Color.White);
        DrawPlain(batch, "New Game", 122, 174, bounds);
        DrawPlain(batch, "Continue", 122, 194, bounds);
        if (CanResumeMixedOrigin)
        {
            DrawPlain(batch, "Return to FFVIII", 122, 214, bounds);
        }
        DrawPlain(batch, ">", 108, 174 + titleSelection * 20, bounds);
    }

    private void DrawDebugMenu(SpriteBatch batch, Rectangle bounds)
    {
        DrawSharedDebugMenu(bounds);
    }

    private void DrawPlain(SpriteBatch batch, string value, int x, int y, Rectangle bounds, int color = 7)
    {
        Vector2 scale = new(bounds.Width / 320f, bounds.Height / 240f);
        if (!UseNativeDialogue && dialogueText is not null && dialoguePreferences is not null)
        {
            UiPreferences preferences = dialoguePreferences();
            Color tint = color switch
            {
                0 => Color.Gray,
                6 => Color.Yellow,
                _ => Color.White
            };
            dialogueText.DrawLine(batch, value, 12 * scale.X, new Vector2(bounds.X + x * scale.X, bounds.Y + y * scale.Y), tint, preferences.UiFontFamily);
            return;
        }
        font.DrawPlain(batch, value, new Vector2(bounds.X + x * scale.X, bounds.Y + y * scale.Y), scale, color);
    }

    private void DrawWindow(SpriteBatch batch, Rectangle window, Rectangle bounds)
    {
        Rectangle area = Logical(window, bounds);
        DrawWindow(batch, new UiRect(area.X, area.Y, area.Width, area.Height), Math.Max(1, bounds.Width / 320));
    }

    private void DrawWindow(SpriteBatch batch, UiRect area, int line)
    {
        if (!UseNativeDialogue && dialogueCanvas is not null && dialoguePreferences is not null)
        {
            UiPreferences preferences = dialoguePreferences();
            UiTheme theme = dialogueTheme?.Invoke() ?? (preferences.MenuPresentation == MenuPresentation.Vector ? new VectorUiTheme { FontFamily = preferences.UiFontFamily } : new UiTheme { FontFamily = preferences.UiFontFamily });
            theme.Panel(dialogueCanvas, area);
            return;
        }
        Fill(area.X, area.Y, area.Width, area.Height, new Color(0, 0, 80, 240));
        Fill(area.X, area.Y, area.Width, line, Color.White);
        Fill(area.X, area.Y + area.Height - line, area.Width, line, Color.White);
        Fill(area.X, area.Y, line, area.Height, Color.White);
        Fill(area.X + area.Width - line, area.Y, line, area.Height, Color.White);

        void Fill(float x, float y, float width, float height, Color color)
        {
            batch.Draw(pixel, new Vector2(x, y), null, color, 0, Vector2.Zero, new Vector2(width, height), SpriteEffects.None, 0);
        }
    }

    private static Rectangle Logical(Rectangle value, Rectangle bounds)
    {
        return new Rectangle(bounds.X + value.X * bounds.Width / 320, bounds.Y + value.Y * bounds.Height / 240, Math.Max(1, value.Width * bounds.Width / 320), Math.Max(1, value.Height * bounds.Height / 240));
    }

    private Texture2D CreateTexture(PcTexture source)
    {
        var decoded = Ff7FieldRenderer.DecodeTexture(source);
        Color[] colors = decoded.Pixels.Select(pixel => Color.FromNonPremultiplied(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray();
        return UploadTexture(decoded.Width, decoded.Height, colors);
    }

    private Texture2D UploadTexture(int width, int height, Color[] colors)
    {
        Texture2D texture = new(device, width, height);
        texture.SetData(colors);
        return texture;
    }

    private static string Wrap(string value, int columns)
    {
        List<string> lines = new();
        foreach (string source in value.Replace('\r', ' ').Split('\n'))
        {
            string remaining = source;
            while (remaining.Length > columns)
            {
                int split = remaining.LastIndexOf(' ', columns);
                if (split < 1)
                {
                    split = columns;
                }
                lines.Add(remaining[..split]);
                remaining = remaining[split..].TrimStart();
            }
            lines.Add(remaining);
        }
        return string.Join('\n', lines.Take(11));
    }

    private void RunGuarded(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is FieldExecutionException or InvalidDataException or NotSupportedException or InvalidOperationException or IOException)
        {
            SetError(exception);
        }
    }

    private void SetError(Exception exception)
    {
        if (error is not null)
        {
            return;
        }
        error = exception.Message;
        movie?.Suspend();
        SetAudioPaused(true);
        CaptureFf7Error(exception);
        Console.Error.WriteLine($"FFVII stopped: {exception}");
    }

    public void Dispose()
    {
        ResetMixedCombat();
        StopMovie();
        DisposeNamingPresentation();
        DisposePlayerMenu();
        DisposeShopMenu();
        StopAudio();
        Session?.Dispose();
        DisposeDialoguePresentation();
        errorReporter?.Dispose();
        reportClipboard.Dispose();
        battleArchive?.Dispose();
        battleMagicArchive?.Dispose();
        triangles.Dispose();
        titleArtwork.Dispose();
        pixel.Dispose();
        font.Dispose();
    }
}
