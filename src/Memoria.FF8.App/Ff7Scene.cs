using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Media;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vector2N = System.Numerics.Vector2;

namespace Memoria.FF8;

internal sealed record Ff7MovieSource(string Path, MovieInformation Information, int NativeFrameCount, double LogicalFramesPerSecond, Func<int, PcMovieCamera?>? CameraAtFrame = null);

internal sealed partial class Ff7Scene : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly bool audioEnabled;
    private readonly Ff7Installation installation;
    private readonly string mediaToolsDirectory;
    private readonly Ff7ProjectionDepth projection;
    private readonly Func<int, Ff7MovieSource> resolveMovie;
    private readonly IFf7ActorAppearance? appearance;
    private readonly Ff7FontRenderer font;
    private readonly Texture2D pixel;
    private readonly Texture2D titleArtwork;
    private readonly GpuTriangleRenderer triangles;
    private CombinedSessionSave envelope;
    private Ff7FieldRenderer? renderer;
    private long renderedGeneration = -1;
    private KeyboardState previousKeys;
    private double tickRemainder;
    private int titleSelection;
    private bool debugMenu;
    private int debugSelection;
    private string? notice;
    private string? error;

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
    public bool InstantText => true;
    public string? Error => error;
    public event Action<CombinedSessionSave>? SaveRequested;
    public event Action<CombinedSessionSave>? WorldSwitchRequested;
    public event Action? ContinueRequested;

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
        Attach(restored);
    }

    public CombinedSessionSave Capture()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("The FFVII title has no gameplay continuation to save.");
        CombinedSessionSave saved = envelope with { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture() };
        saved.Validate();
        return saved;
    }

    public void Update(GameTime gameTime, KeyboardState keys)
    {
        try
        {
            if (Pressed(keys, Keys.F2))
            {
                debugMenu = !debugMenu;
                SetAudioPaused(debugMenu);
                notice = null;
                if (debugMenu)
                {
                    movie?.Suspend();
                }
                else
                {
                    movie?.Resume();
                }
            }
            if (error is not null)
            {
                return;
            }
            if (Session is null)
            {
                UpdateTitle(keys);
                return;
            }
            if (debugMenu)
            {
                UpdateDebugMenu(keys);
                return;
            }
            UpdateAudio();
            UpdateSessionInput(keys);
            UpdateMovie();
            tickRemainder += Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25);
            while (tickRemainder >= 1d / 30)
            {
                tickRemainder -= 1d / 30;
                ApplyLogicalInput();
                Session.AdvanceTick();
                UpdateDialogueTick();
                UpdateAudio();
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or InvalidOperationException or IOException)
        {
            SetError(exception);
        }
        finally
        {
            previousKeys = keys;
        }
    }

    public void Draw(SpriteBatch batch, Rectangle bounds)
    {
        if (Session is not null && error is null)
        {
            RunGuarded(() => RenderField(bounds));
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
                DrawMovie(batch, bounds);
                if (renderer is not null && error is null)
                {
                    batch.Draw(triangles.Target, bounds, Color.White);
                }
                if (error is null)
                {
                    DrawDialogues(batch, bounds);
                }
            }
            if (debugMenu)
            {
                DrawDebugMenu(batch, bounds);
            }
            if (notice is not null)
            {
                DrawPlain(batch, notice, 8, 220, bounds, 6);
            }
            if (error is not null)
            {
                DrawWindow(batch, new Rectangle(4, 4, 312, 224), bounds);
                DrawPlain(batch, "FFVII stopped at an unsupported contract", 12, 14, bounds, 6);
                DrawPlain(batch, Wrap(error, 42), 12, 38, bounds, 7);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or InvalidOperationException or IOException)
        {
            SetError(exception);
        }
        finally
        {
            batch.End();
        }
    }

    private void Attach(Ff7Session session)
    {
        StopAudio();
        Session?.Dispose();
        StopMovie();
        Session = session;
        Session.PresentationCommand = PresentationCommand;
        renderedGeneration = -1;
        renderer = null;
        dialogueTokens.Clear();
        error = null;
        notice = null;
        debugMenu = false;
        tickRemainder = 0;
    }

    private void RenderField(Rectangle bounds)
    {
        Ff7Session session = Session!;
        if (renderer is null || renderedGeneration != session.FieldGeneration)
        {
            renderer = new Ff7FieldRenderer(session.Field, session.Models.Select((model, index) => (model, index)).ToDictionary(pair => pair.index, pair => pair.model), projection, appearance);
            renderedGeneration = session.FieldGeneration;
        }
        Ff7FieldProcess state = session.Host.Process;
        Ff7ActorVisual[] actors = state.Actors.Select(actor => new Ff7ActorVisual(actor.Entity, actor.Model, actor.X4096, actor.Y4096, actor.Z4096, checked((byte)actor.Direction), actor.Animation, actor.AnimationFrame16, actor.Visible)).ToArray();
        PcFieldCamera? camera = CurrentMovieCamera();
        Ff7FieldRenderFrame frame = new(0, new Vector2N(state.CameraX, state.CameraY), actors, state.BackgroundStates, state.Movie is { Playing: true }, camera, state.MovieBackgroundEnabled);
        renderer.Render(triangles, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), frame);
    }

    private void PresentationCommand(FieldCommand command)
    {
        throw new NotSupportedException($"FFVII presentation does not implement {command.GetType().Name}.");
    }

    private void UpdateTitle(KeyboardState keys)
    {
        if (Pressed(keys, Keys.Up) || Pressed(keys, Keys.Down))
        {
            titleSelection = 1 - titleSelection;
        }
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Z))
        {
            if (titleSelection == 0)
            {
                StartNewGame();
            }
            else
            {
                ContinueRequested?.Invoke();
            }
        }
    }

    private void UpdateDebugMenu(KeyboardState keys)
    {
        if (Pressed(keys, Keys.Escape))
        {
            debugMenu = false;
            movie?.Resume();
            SetAudioPaused(false);
            return;
        }
        if (Pressed(keys, Keys.Up) || Pressed(keys, Keys.Down))
        {
            debugSelection = 1 - debugSelection;
        }
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Z))
        {
            if (!CanSave)
            {
                notice = "Available only where saving is allowed.";
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
                notice = "No suspended FFVIII session is available.";
            }
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
        DrawPlain(batch, ">", 108, 174 + titleSelection * 20, bounds);
    }

    private void DrawDebugMenu(SpriteBatch batch, Rectangle bounds)
    {
        DrawWindow(batch, new Rectangle(68, 65, 192, 110), bounds);
        DrawPlain(batch, "Debug menu", 80, 76, bounds, 6);
        DrawPlain(batch, "Save", 96, 100, bounds, CanSave ? 7 : 0);
        DrawPlain(batch, "Switch world: FFVIII", 96, 122, bounds, CanSave && envelope.Ff8Payload is { Length: > 0 } ? 7 : 0);
        DrawPlain(batch, ">", 80, 100 + debugSelection * 22, bounds);
        DrawPlain(batch, "Instant text: on (debug)", 80, 148, bounds, 0);
    }

    private void DrawPlain(SpriteBatch batch, string value, int x, int y, Rectangle bounds, int color = 7)
    {
        Vector2 scale = new(bounds.Width / 320f, bounds.Height / 240f);
        font.DrawPlain(batch, value, new Vector2(bounds.X + x * scale.X, bounds.Y + y * scale.Y), scale, color);
    }

    private void DrawWindow(SpriteBatch batch, Rectangle window, Rectangle bounds)
    {
        Rectangle area = Logical(window, bounds);
        batch.Draw(pixel, area, new Color(0, 0, 80, 240));
        int line = Math.Max(1, bounds.Width / 320);
        batch.Draw(pixel, new Rectangle(area.X, area.Y, area.Width, line), Color.White);
        batch.Draw(pixel, new Rectangle(area.X, area.Bottom - line, area.Width, line), Color.White);
        batch.Draw(pixel, new Rectangle(area.X, area.Y, line, area.Height), Color.White);
        batch.Draw(pixel, new Rectangle(area.Right - line, area.Y, line, area.Height), Color.White);
    }

    private static Rectangle Logical(Rectangle value, Rectangle bounds)
    {
        return new Rectangle(bounds.X + value.X * bounds.Width / 320, bounds.Y + value.Y * bounds.Height / 240, Math.Max(1, value.Width * bounds.Width / 320), Math.Max(1, value.Height * bounds.Height / 240));
    }

    private Texture2D CreateTexture(PcTexture source)
    {
        Color[] colors = new Color[source.Width * source.Height];
        for (int i = 0; i < colors.Length; i++)
        {
            uint value = source.BytesPerPixel == 1 && source.Palettes.Count > 0 ? source.Palettes[0][source.Pixels.Span[i]] : source.BytesPerPixel == 4 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.Pixels.Span[(i * 4)..]) : throw new NotSupportedException("FFVII title texture requires native indexed or BGRA pixels.");
            colors[i] = new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));
        }
        Texture2D texture = new(device, source.Width, source.Height);
        texture.SetData(colors);
        return texture;
    }

    private bool Pressed(KeyboardState keys, Keys key)
    {
        return keys.IsKeyDown(key) && previousKeys.IsKeyUp(key);
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
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or InvalidOperationException or IOException)
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
        Console.Error.WriteLine($"FFVII stopped: {exception}");
    }

    public void Dispose()
    {
        StopMovie();
        StopAudio();
        Session?.Dispose();
        triangles.Dispose();
        titleArtwork.Dispose();
        pixel.Dispose();
        font.Dispose();
    }
}
