using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class CrossoverMappingMenu : IDisposable
{
    private static readonly string[] StoryNames = ["Cloud", "Barret", "Tifa", "Aerith", "Red XIII", "Yuffie", "Cait Sith", "Vincent", "Cid"];
    private static readonly string[] AppearanceNames = ["Squall", "Zell", "Irvine", "Quistis", "Rinoa", "Selphie", "Seifer", "Edea", "Laguna", "Kiros", "Ward"];
    private readonly Texture2D[] storyPortraits;
    private readonly Texture2D[] appearancePortraits;
    private readonly SystemTextRenderer text;
    private readonly MonoGameUiCanvas canvas;
    private readonly Func<UiTheme> theme;
    private KeyboardState previousKeys;
    private MouseState previousMouse;
    private int selection;
    private bool choosingAppearance;
    private bool mapped;
    private string? notice;

    public CrossoverMappingMenu(GraphicsDevice device, Ff7Installation installation, string ff8Directory, IEnumerable<CharacterAppearanceMapping> mappings, KeyboardState keys, MouseState mouse, SystemTextRenderer text, MonoGameUiCanvas canvas, Func<UiTheme> theme)
    {
        Ff7MenuResources menu = Ff7MenuResources.LoadPortraits(installation);
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(ff8Directory);
        storyPortraits = Enumerable.Range(0, StoryNames.Length).Select(id => Texture(device, Ff7MenuPortrait.Decode(menu.GetPortrait(id)))).ToArray();
        appearancePortraits = Enumerable.Range(0, AppearanceNames.Length).Select(id => Texture(device, artwork.GetCharacterPortrait(id))).ToArray();
        this.text = text;
        this.canvas = canvas;
        this.theme = theme;
        mapped = mappings.Any(mapping => mapping.StoryCharacter == 0 && mapping.AppearanceCharacter == 0);
        previousKeys = keys;
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
                if (selection >= 0)
                {
                    Activate(selection);
                }
                break;
            case MenuInput.Cancel:
                if (choosingAppearance)
                {
                    choosingAppearance = false;
                    selection = 0;
                }
                else
                {
                    Cancelled?.Invoke();
                }
                break;
            case MenuInput.Alternate:
                mapped = false;
                notice = "Cloud uses his original model.";
                break;
        }
    }

    public void Update(KeyboardState keys, MouseState mouse, Rectangle viewport)
    {
        bool Pressed(Keys key) => keys.IsKeyDown(key) && previousKeys.IsKeyUp(key);
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        Point point = new((mouse.X - viewport.X) * 640 / Math.Max(1, viewport.Width), (mouse.Y - viewport.Y) * 480 / Math.Max(1, viewport.Height));
        if (Pressed(Keys.Escape))
        {
            Apply(MenuInput.Cancel);
        }
        int count = choosingAppearance ? AppearanceNames.Length : StoryNames.Length + 2;
        if (Pressed(Keys.Left))
        {
            Apply(MenuInput.Left);
        }
        if (Pressed(Keys.Right) || Pressed(Keys.Tab))
        {
            Apply(MenuInput.Right);
        }
        if (Pressed(Keys.Up))
        {
            Apply(MenuInput.Up);
        }
        if (Pressed(Keys.Down))
        {
            Apply(MenuInput.Down);
        }
        if (Pressed(Keys.Delete) || Pressed(Keys.Back))
        {
            Apply(MenuInput.Alternate);
        }
        if (clicked)
        {
            selection = Enumerable.Range(0, count).FirstOrDefault(id => Cell(id, choosingAppearance).Contains(point), -1);
        }
        if ((clicked || Pressed(Keys.Enter) || Pressed(Keys.Z)) && selection >= 0)
        {
            Activate(selection);
        }
        previousKeys = keys;
        previousMouse = mouse;
    }

    private void Activate(int selected)
    {
        if (choosingAppearance)
        {
            if (selected == 0)
            {
                mapped = true;
                choosingAppearance = false;
                selection = 0;
                notice = "Cloud -> Squall. Delete clears the mapping.";
            }
            else
            {
                notice = "Only Squall is available in this experiment.";
            }
        }
        else if (selected == StoryNames.Length)
        {
            CharacterAppearanceMapping[] mappings = mapped ? [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.StandardFieldSquallProfileVersion)] : [];
            Confirmed?.Invoke(mappings);
        }
        else if (selected == StoryNames.Length + 1)
        {
            Cancelled?.Invoke();
        }
        else if (selected == 0)
        {
            choosingAppearance = true;
            selection = 0;
            notice = null;
        }
        else
        {
            notice = "Only Cloud can be mapped in this experiment.";
        }
    }

    public void Draw(SpriteBatch batch, Rectangle viewport, string fontFamily)
    {
        text.FontFamily = fontFamily;
        text.BeginFrame();
        Rectangle Scale(Rectangle value) => new(viewport.X + value.X * viewport.Width / 640, viewport.Y + value.Y * viewport.Height / 480, value.Width * viewport.Width / 640, value.Height * viewport.Height / 480);
        void Label(string value, int x, int y, Color color, int size = 16) => text.Draw(batch, value, size * viewport.Height / 480f, new Vector2(viewport.X + x * viewport.Width / 640f, viewport.Y + y * viewport.Height / 480f), color);
        batch.Begin(samplerState: SamplerState.PointClamp);
        UiTheme currentTheme = theme();
        currentTheme.Panel(canvas, new UiRect(viewport.X, viewport.Y, viewport.Width, viewport.Height));
        Label(choosingAppearance ? "Choose a FINAL FANTASY VIII character" : "World switch: FINAL FANTASY VII", 24, 16, Color.White, 22);
        int count = choosingAppearance ? AppearanceNames.Length : StoryNames.Length + 2;
        for (int id = 0; id < count; id++)
        {
            Rectangle cell = Cell(id, choosingAppearance);
            bool enabled = choosingAppearance ? id == 0 : id == 0 || id >= StoryNames.Length;
            Rectangle panel = Scale(cell);
            currentTheme.Panel(canvas, new UiRect(panel.X, panel.Y, panel.Width, panel.Height));
            if (selection == id)
            {
                canvas.MenuPointer(new UiRect(panel.X, panel.Y + panel.Height / 3, panel.Width / 10, panel.Height / 3));
            }
            Color tint = enabled ? Color.White : Color.Gray;
            if (!choosingAppearance && id >= StoryNames.Length)
            {
                Label(id == StoryNames.Length ? "Continue in FFVII" : "Cancel", cell.X + 12, cell.Y + 9, tint);
                continue;
            }
            Texture2D portrait = choosingAppearance ? appearancePortraits[id] : storyPortraits[id];
            batch.Draw(portrait, Scale(FitPortrait(portrait, cell.X + 8, cell.Y + 8)), tint);
            if (!choosingAppearance && id == 0 && mapped)
            {
                batch.Draw(appearancePortraits[0], Scale(FitPortrait(appearancePortraits[0], cell.X + 30, cell.Y + 18)), Color.White * 0.9f);
            }
            Label(choosingAppearance ? AppearanceNames[id] : StoryNames[id], cell.X + 82, cell.Y + 20, tint);
        }
        Label(notice ?? "Select a portrait. Enter confirms; Escape returns; Delete clears.", 24, 456, Color.LightGray, 13);
        batch.End();
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

    private static Rectangle FitPortrait(Texture2D portrait, int x, int y)
    {
        float scale = Math.Min(48f / portrait.Width, 48f / portrait.Height);
        int width = Math.Max(1, (int)(portrait.Width * scale));
        int height = Math.Max(1, (int)(portrait.Height * scale));
        return new Rectangle(x, y, width, height);
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
        foreach (Texture2D portrait in storyPortraits.Concat(appearancePortraits))
        {
            portrait.Dispose();
        }
    }
}
