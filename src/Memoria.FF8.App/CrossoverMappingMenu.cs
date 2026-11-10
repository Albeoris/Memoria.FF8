using System.Collections.Concurrent;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class CrossoverMappingMenu : IDisposable
{
    private static readonly string[] StoryNames = CrossoverCharacterCatalog.StoryNames;
    private static readonly string[] AppearanceNames = CrossoverCharacterCatalog.AppearanceNames;
    private readonly Texture2D[] storyPortraits;
    private readonly Texture2D[] appearancePortraits;
    private readonly SystemTextRenderer text;
    private readonly MonoGameUiCanvas canvas;
    private readonly Func<UiTheme> theme;
    private readonly SessionGame destinationGame;
    private readonly Ff7Installation installation;
    private readonly string ff8Directory;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim coverageGate = new(1, 1);
    private readonly HashSet<int> requestedReports = [];
    private readonly ConcurrentDictionary<(int Story, int Appearance), CharacterCompatibility> reports = new();
    private CharacterAppearanceMapping[] mappings;
    private MouseState previousMouse;
    private int selection;
    private int selectedStory;
    private bool choosingAppearance;
    private bool showingMissing;
    private int missingPage;
    private string? notice;

    public CrossoverMappingMenu(GraphicsDevice device, Ff7Installation installation, string ff8Directory, IEnumerable<CharacterAppearanceMapping> mappings, KeyboardState keys, MouseState mouse, SystemTextRenderer text, MonoGameUiCanvas canvas, Func<UiTheme> theme, SessionGame destinationGame = SessionGame.Ff7)
    {
        Ff7MenuResources menu = Ff7MenuResources.LoadPortraits(installation);
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(ff8Directory);
        storyPortraits = Enumerable.Range(0, StoryNames.Length).Select(id => Texture(device, Ff7MenuPortrait.Decode(menu.GetPortrait(id)))).ToArray();
        appearancePortraits = Enumerable.Range(0, AppearanceNames.Length).Select(id => Texture(device, artwork.GetCharacterPortrait(id))).ToArray();
        this.text = text;
        this.canvas = canvas;
        this.theme = theme;
        this.destinationGame = destinationGame;
        this.installation = installation;
        this.ff8Directory = ff8Directory;
        this.mappings = mappings.ToArray();
        CharacterMappingAssociations.Validate(this.mappings);
        previousMouse = mouse;
    }

    public event Action<CharacterAppearanceMapping[]>? Confirmed;
    public event Action? Cancelled;

    public void SetNotice(string value)
    {
        notice = value;
    }

    public void Apply(MenuInput input)
    {
        if (showingMissing)
        {
            if (input is MenuInput.Cancel or MenuInput.Alternate or MenuInput.Confirm)
            {
                showingMissing = false;
            }
            else if (input is MenuInput.Down or MenuInput.Right or MenuInput.PageDown)
            {
                missingPage++;
            }
            else if (input is MenuInput.Up or MenuInput.Left or MenuInput.PageUp)
            {
                missingPage = Math.Max(0, missingPage - 1);
            }
            return;
        }
        int count = choosingAppearance ? AppearanceNames.Length : StoryNames.Length + 2;
        switch (input)
        {
            case MenuInput.Up:
                selection = Math.Max(0, selection - 3);
                break;
            case MenuInput.Down:
                selection = Math.Min(count - 1, selection + 3);
                break;
            case MenuInput.Left:
                selection = (selection + count - 1) % count;
                break;
            case MenuInput.Right:
            case MenuInput.Tab:
                selection = (selection + 1) % count;
                break;
            case MenuInput.Confirm:
                Activate(selection);
                break;
            case MenuInput.Cancel:
                if (choosingAppearance)
                {
                    choosingAppearance = false;
                    selection = selectedStory;
                }
                else
                {
                    Cancelled?.Invoke();
                }
                break;
            case MenuInput.Alternate:
                if (choosingAppearance)
                {
                    showingMissing = true;
                    missingPage = 0;
                }
                else if (selection < StoryNames.Length)
                {
                    mappings = mappings.Where(mapping => mapping.StoryCharacter != selection).ToArray();
                    notice = StoryNames[selection] + " uses native appearance.";
                }
                break;
            case MenuInput.PageUp:
            case MenuInput.PageDown:
                if (choosingAppearance)
                {
                    showingMissing = true;
                    missingPage = 0;
                }
                break;
        }
    }

    public void Update(InputFrame input, MouseState mouse, Rectangle viewport)
    {
        if (input.MenuVertical < 0)
        {
            Apply(MenuInput.Up);
        }
        if (input.MenuVertical > 0)
        {
            Apply(MenuInput.Down);
        }
        if (input.MenuHorizontal < 0)
        {
            Apply(MenuInput.Left);
        }
        if (input.MenuHorizontal > 0)
        {
            Apply(MenuInput.Right);
        }
        if (input.MenuPage != 0)
        {
            Apply(input.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
        }
        if (input.CardChallenge)
        {
            Apply(MenuInput.Alternate);
        }
        if (input.Confirm)
        {
            Apply(MenuInput.Confirm);
        }
        if (input.Cancel)
        {
            Apply(MenuInput.Cancel);
        }
        viewport = DesignViewport(viewport);
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        if (clicked && !showingMissing && viewport.Contains(mouse.X, mouse.Y))
        {
            Point point = new((mouse.X - viewport.X) * 640 / Math.Max(1, viewport.Width), (mouse.Y - viewport.Y) * 480 / Math.Max(1, viewport.Height));
            int count = choosingAppearance ? AppearanceNames.Length : StoryNames.Length + 2;
            int selected = Enumerable.Range(0, count).FirstOrDefault(id => Cell(id, choosingAppearance).Contains(point), -1);
            if (selected >= 0)
            {
                selection = selected;
                Activate(selected);
            }
        }
        previousMouse = mouse;
    }

    private void Activate(int selected)
    {
        if (choosingAppearance)
        {
            mappings = CharacterMappingAssociations.Assign(mappings, selectedStory, selected);
            choosingAppearance = false;
            selection = selectedStory;
            notice = StoryNames[selectedStory] + " -> " + AppearanceNames[selected] + ". Native game progress stays independent.";
        }
        else if (selected == StoryNames.Length)
        {
            Confirmed?.Invoke(mappings.ToArray());
        }
        else if (selected == StoryNames.Length + 1)
        {
            Cancelled?.Invoke();
        }
        else if ((uint)selected < StoryNames.Length)
        {
            selectedStory = selected;
            choosingAppearance = true;
            selection = mappings.SingleOrDefault(mapping => mapping.StoryCharacter == selected)?.AppearanceCharacter ?? 0;
            notice = null;
            RequestCoverage(selectedStory);
        }
    }

    private void RequestCoverage(int story)
    {
        if (!requestedReports.Add(story))
        {
            return;
        }
        CancellationToken cancellation = lifetime.Token;
        _ = Task.Run(async () =>
        {
            bool entered = false;
            try
            {
                await coverageGate.WaitAsync(cancellation);
                entered = true;
                using GameAssets assets = new(ff8Directory);
                CrossoverCharacterCatalog catalog = new(installation, assets, cancellation);
                for (int appearance = 0; appearance < AppearanceNames.Length; appearance++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    reports[(story, appearance)] = catalog.GetCompatibility(story, appearance);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException)
            {
                for (int appearance = 0; appearance < AppearanceNames.Length; appearance++)
                {
                    reports.TryAdd((story, appearance), new(0, 0, [new("Catalog", exception.Message)]));
                }
            }
            finally
            {
                if (entered)
                {
                    coverageGate.Release();
                }
            }
        }, cancellation);
    }

    public void Draw(SpriteBatch batch, Rectangle viewport, string fontFamily)
    {
        Rectangle outputViewport = viewport;
        viewport = DesignViewport(viewport);
        text.FontFamily = fontFamily;
        text.BeginFrame();
        Rectangle Scale(Rectangle value) => new(viewport.X + value.X * viewport.Width / 640, viewport.Y + value.Y * viewport.Height / 480, value.Width * viewport.Width / 640, value.Height * viewport.Height / 480);
        void Label(string value, int x, int y, Color color, int size = 16) => text.Draw(batch, value, size * viewport.Height / 480f, new Vector2(viewport.X + x * viewport.Width / 640f, viewport.Y + y * viewport.Height / 480f), color);
        batch.Begin(samplerState: SamplerState.PointClamp);
        UiTheme currentTheme = theme();
        currentTheme.Panel(canvas, new UiRect(outputViewport.X, outputViewport.Y, outputViewport.Width, outputViewport.Height));
        if (showingMissing)
        {
            Label(StoryNames[selectedStory] + " -> " + AppearanceNames[selection] + ": animation coverage", 24, 18, Color.White, 20);
            if (reports.TryGetValue((selectedStory, selection), out CharacterCompatibility? report))
            {
                Label(report.Summary + $"; {report.ExcludedSources.Count} unreadable field members excluded", 24, 54, CoverageColor(report), 13);
                CharacterAnimationGap[] gaps = report.Missing.Concat(report.ExcludedSources).ToArray();
                int pages = Math.Max(1, (gaps.Length + 5) / 6);
                missingPage = Math.Min(missingPage, pages - 1);
                if (report.Missing.Count == 0)
                {
                    Label("No missing native integer clip samples in the named field catalog.", 24, 92, Color.White, 14);
                }
                int row = 0;
                foreach (CharacterAnimationGap gap in gaps.Skip(missingPage * 6).Take(6))
                {
                    Label(gap.Resource, 24, 92 + row * 48, Color.White, 14);
                    Label(gap.Reason.Length > 88 ? gap.Reason[..85] + "..." : gap.Reason, 24, 111 + row * 48, Color.LightGray, 12);
                    row++;
                }
                Label("Partial part masks need unambiguous face owners; color effects are unsupported.", 24, 384, Color.LightGray, 12);
                Label($"Page {missingPage + 1}/{pages}. Direction/page controls browse; Confirm/Cancel returns.", 24, 405, Color.LightGray, 13);
            }
            else
            {
                Label("Checking original field clips...", 24, 64, Color.LightGray);
            }
            Label("Body clip execution is not artistic certification. Accessories keep local bind pose.", 24, 438, Color.LightGray, 12);
            Label("Native waits, actor anchors, progress and inventory stay with their own games.", 24, 456, Color.LightGray, 12);
            batch.End();
            return;
        }
        string destination = destinationGame == SessionGame.Ff7 ? "FINAL FANTASY VII" : "FINAL FANTASY VIII";
        Label(choosingAppearance ? "Appearance for " + StoryNames[selectedStory] : "World switch: " + destination, 24, 16, Color.White, 22);
        int count = choosingAppearance ? AppearanceNames.Length : StoryNames.Length + 2;
        for (int id = 0; id < count; id++)
        {
            Rectangle cell = Cell(id, choosingAppearance);
            Rectangle panel = Scale(cell);
            currentTheme.Panel(canvas, new UiRect(panel.X, panel.Y, panel.Width, panel.Height));
            if (selection == id)
            {
                canvas.MenuPointer(new UiRect(panel.X, panel.Y + panel.Height / 3, panel.Width / 10, panel.Height / 3));
            }
            if (!choosingAppearance && id >= StoryNames.Length)
            {
                Label(id == StoryNames.Length ? "Continue in " + (destinationGame == SessionGame.Ff7 ? "FFVII" : "FFVIII") : "Cancel", cell.X + 12, cell.Y + 9, Color.White);
                continue;
            }
            Texture2D portrait = choosingAppearance ? appearancePortraits[id] : storyPortraits[id];
            Rectangle portraitBounds = FitPortrait(portrait, Scale(new Rectangle(cell.X + 8, cell.Y + 8, 48, 48)));
            batch.Draw(portrait, portraitBounds, Color.White);
            if (!choosingAppearance && mappings.SingleOrDefault(mapping => mapping.StoryCharacter == id) is { } mapping)
            {
                Texture2D mappedPortrait = appearancePortraits[mapping.AppearanceCharacter];
                Rectangle overlay = FitPortrait(mappedPortrait, new Rectangle(portraitBounds.X + portraitBounds.Width * 45 / 100, portraitBounds.Y + portraitBounds.Height / 5, portraitBounds.Width, portraitBounds.Height));
                batch.Draw(mappedPortrait, overlay, Color.White * 0.9f);
            }
            Label(choosingAppearance ? AppearanceNames[id] : StoryNames[id], cell.X + 70, cell.Y + 12, Color.White, 15);
            if (choosingAppearance)
            {
                if (reports.TryGetValue((selectedStory, id), out CharacterCompatibility? report))
                {
                    Label($"{report.Percentage:0.#}%  {report.SupportedClips}/{report.TotalClips}", cell.X + 70, cell.Y + 37, CoverageColor(report), 13);
                }
                else
                {
                    Label("Checking...", cell.X + 70, cell.Y + 37, Color.LightGray, 12);
                }
            }
        }
        if (choosingAppearance)
        {
            Label("Percent = executable clips / readable named HRC+clip pairs; exclusions in details.", 24, 405, Color.LightGray, 12);
            Label("Page/Alternate: missing actions. Cloth, hair, weapons use target local bind pose.", 24, 423, Color.LightGray, 12);
        }
        else
        {
            Label("Associations affect FFVII appearance. FFVIII keeps its native characters.", 24, 445, Color.LightGray, 12);
        }
        Label(notice ?? "Confirm selects; Cancel returns; Alternate clears a selected association.", 24, 462, Color.LightGray, 12);
        batch.End();
    }

    private static Color CoverageColor(CharacterCompatibility report) => CharacterCompatibility.ColorName(report.Percentage) switch
    {
        "Cyan" => Color.Cyan,
        "Green" => Color.Green,
        "Yellow" => Color.Yellow,
        "Orange" => Color.Orange,
        _ => Color.Red
    };

    private static Rectangle DesignViewport(Rectangle viewport)
    {
        PixelBounds fit = ViewportLayout.Fit(640, 480, viewport.Width, viewport.Height);
        return new(viewport.X + fit.X, viewport.Y + fit.Y, fit.Width, fit.Height);
    }

    private static Rectangle Cell(int id, bool candidates)
    {
        if (!candidates && id >= StoryNames.Length)
        {
            return new Rectangle(24 + (id - StoryNames.Length) * 304, 405, 288, 38);
        }
        return new Rectangle(24 + id % 3 * 202, 58 + id / 3 * (candidates ? 82 : 108), 188, candidates ? 74 : 96);
    }

    private static Texture2D Texture(GraphicsDevice device, PixelImage image)
    {
        Texture2D result = new(device, image.Width, image.Height);
        result.SetData(image.Pixels.Select(pixel => new Color(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray());
        return result;
    }

    private static Rectangle FitPortrait(Texture2D portrait, Rectangle bounds)
    {
        Memoria.FF8.Runtime.PixelBounds fit = Memoria.FF8.Runtime.ViewportLayout.Fit(portrait.Width, portrait.Height, bounds.Width, bounds.Height);
        return new Rectangle(bounds.X + fit.X, bounds.Y + fit.Y, fit.Width, fit.Height);
    }

    private static Texture2D Texture(GraphicsDevice device, Ff7MenuPortrait image)
    {
        Color[] colors = new Color[image.Width * image.Height];
        for (int index = 0; index < colors.Length; index++)
        {
            uint pixel = image.Pixels.Span[index];
            colors[index] = new Color((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel, (byte)(pixel >> 24));
        }
        Texture2D result = new(device, image.Width, image.Height);
        result.SetData(colors);
        return result;
    }

    public void Dispose()
    {
        lifetime.Cancel();
        foreach (Texture2D portrait in storyPortraits.Concat(appearancePortraits))
        {
            portrait.Dispose();
        }
    }
}
