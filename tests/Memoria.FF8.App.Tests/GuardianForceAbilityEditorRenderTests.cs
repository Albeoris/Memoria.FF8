using System.Collections.Immutable;
using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceAbilityEditorRenderTests
{
    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(2048, 797)]
    public void SyntheticExpandedEditorRendersWithoutInstalledResources(int width, int height)
    {
        MenuController menu = CreateMenu();
        menu.Open("gf-ability-targets");
        menu.Execute(new("gf-editor-target", "new"));
        menu.Navigate(MenuInput.FocusRight);
        menu.Navigate(MenuInput.Alternate);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_EDITOR_RENDER_DIR") ?? Path.Combine(Path.GetTempPath(), "Memoria.FF8.GfEditorPreviews");
        Directory.CreateDirectory(directory);
        foreach (bool scrolled in new[] { false, true })
        {
            if (scrolled)
            {
                menu.Navigate(MenuInput.Actions);
                menu.Execute(new("gf-editor-command", "tool:2"));
                for (int index = 0; index < 32; index++)
                {
                    menu.Navigate(MenuInput.Down);
                }
            }
            using Bitmap bitmap = new(width, height);
            using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
            renderer.BeginFrame(TimeSpan.Zero);
            SyntheticArtworkCanvas canvas = new(renderer);
            UiView view = menu.BuildView();
            view.Resize(width, height);
            view.Draw(canvas, new UiTheme());
            Assert.Contains(canvas.Texts, text => text.Value.Contains("45 entries", StringComparison.Ordinal));
            Assert.Contains(canvas.Texts, text => text.Value.Contains("Unsaved", StringComparison.Ordinal));
            Assert.Contains(canvas.Texts, text => text.Value == "Save mod");
            Assert.All(canvas.Texts, text => Assert.True(text.Bounds.Width >= 0 && text.Bounds.Height >= 0));
            string prefix = Path.Combine(directory, $"gf-editor-{width}x{height}{(scrolled ? "-scrolled" : "")}");
            bitmap.SavePng(prefix + ".png");
            File.WriteAllLines(prefix + ".txt", canvas.Texts.Select(text => $"{text.Bounds.X:0.0},{text.Bounds.Y:0.0},{text.Bounds.Width:0.0},{text.Bounds.Height:0.0}\t{text.Value.Replace("\n", " | ", StringComparison.Ordinal)}"));
            Console.WriteLine("Synthetic GF editor preview: " + Path.GetFullPath(prefix + ".png"));
        }
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void WideEditorShowsTwentyThreeEntriesInTwoColumns(MenuPresentation presentation)
    {
        MenuController menu = CreateMenu(23);
        menu.Presentation = presentation;
        menu.Open("gf-ability-targets");
        menu.Execute(new("gf-editor-target", "new"));
        using Bitmap bitmap = new(2048, 797);
        using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
        renderer.BeginFrame(TimeSpan.Zero);
        SyntheticArtworkCanvas canvas = new(renderer);
        UiView view = menu.BuildView();
        view.Resize(2048, 797);
        view.Draw(canvas, new UiTheme());

        Assert.Contains(canvas.Texts, text => text.Value == "1–23 / 23 · Page 1/1");
        Assert.Equal(2, canvas.Texts.Count(text => text.Value == "Start learned"));
        for (int position = 1; position <= 23; position++)
        {
            Assert.Single(canvas.Texts, text => text.Value == position.ToString());
        }
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_EDITOR_RENDER_DIR") ?? Path.Combine(Path.GetTempPath(), "Memoria.FF8.GfEditorPreviews");
        Directory.CreateDirectory(directory);
        bitmap.SavePng(Path.Combine(directory, $"gf-editor-23-{presentation}-2048x797.png"));
        Assert.All(canvas.Texts.Where(text => text.Value is "Save mod" or "Preview" or "Actions" or "Details"), text =>
        {
            Assert.True(text.Bounds.X >= 0 && text.Bounds.Y >= 0);
            Assert.True(text.Bounds.Right <= 2048 && text.Bounds.Bottom <= 797);
        });
    }

    [Theory]
    [InlineData(1280, 720, 100)]
    [InlineData(1920, 1080, 100)]
    [InlineData(2048, 797, 150)]
    [InlineData(640, 448, 150)]
    public void ResizedEditorKeepsActionsAndRowsWithinViewport(int width, int height, int textScale)
    {
        MenuController menu = CreateMenu(29, textScale);
        menu.Open("gf-ability-targets");
        menu.Execute(new("gf-editor-target", "new"));
        using Bitmap bitmap = new(width, height);
        using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
        renderer.BeginFrame(TimeSpan.Zero);
        SyntheticArtworkCanvas canvas = new(renderer);
        UiView view = menu.BuildView();
        view.Resize(width, height);
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Value.Contains("Page 1/", StringComparison.Ordinal));
        Assert.All(canvas.Texts, text =>
        {
            Assert.True(text.Bounds.Width >= 0 && text.Bounds.Height >= 0);
            Assert.True(text.Bounds.X >= 0 && text.Bounds.Y >= 0);
            Assert.True(text.Bounds.Right <= width + 1 && text.Bounds.Bottom <= height + 1);
        });
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_EDITOR_RENDER_DIR") ?? Path.Combine(Path.GetTempPath(), "Memoria.FF8.GfEditorPreviews");
        Directory.CreateDirectory(directory);
        bitmap.SavePng(Path.Combine(directory, $"gf-editor-resized-{width}x{height}-{textScale}.png"));
    }

    [Fact]
    public void EntryAndPreviewUseDedicatedAuthoringAndReadOnlyContent()
    {
        MenuController menu = CreateMenu(23);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_EDITOR_RENDER_DIR") ?? Path.Combine(Path.GetTempPath(), "Memoria.FF8.GfEditorPreviews");
        Directory.CreateDirectory(directory);
        menu.Open("gf-ability-targets");
        Capture("entry", canvas =>
        {
            Assert.Contains(canvas.Texts, text => text.Value == "Create new mod");
            Assert.DoesNotContain(canvas.Texts, text => text.Value.Contains(" AP", StringComparison.Ordinal));
        });
        menu.Execute(new("gf-editor-target", "new"));
        menu.Navigate(MenuInput.Actions);
        menu.Execute(new("gf-editor-command", "preview"));
        Capture("preview", canvas =>
        {
            Assert.Contains(canvas.Texts, text => text.Value == "Preview · Read-only");
            Assert.Contains(canvas.Texts, text => text.Value == "Back to editor");
            Assert.DoesNotContain(canvas.Texts, text => text.Value is "Add" or "Replace" or "Swap" or "Save mod" or "Apply changes" or "Search abilities…");
        });

        void Capture(string suffix, Action<SyntheticArtworkCanvas> assertions)
        {
            using Bitmap bitmap = new(2048, 797);
            using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
            renderer.BeginFrame(TimeSpan.Zero);
            SyntheticArtworkCanvas canvas = new(renderer);
            UiView view = menu.BuildView();
            view.Resize(2048, 797);
            view.Draw(canvas, new UiTheme());
            assertions(canvas);
            bitmap.SavePng(Path.Combine(directory, $"gf-editor-{suffix}-2048x797.png"));
        }
    }

    private static MenuController CreateMenu(int abilityCount = 45, int textScale = 100)
    {
        NativeCharacterVitals source = Source(abilityCount);
        NativeCharacterVitals current = source.ApplyGuardianForceTemplates(source.ResolveGuardianForceTemplates());
        GameSession session = new(new GameState { CharacterVitals = current });
        MenuData data = new();
        data.Settings["Text scale"] = textScale + "%";
        MenuController menu = new(() => data);
        GuardianForceAbilityPatchDocument inherited = new()
        {
            DocumentId = "synthetic-layer",
            Operations = [new() { Id = "default", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.SetInitialState, Ability = 2, InitialLearned = true }]
        };
        menu.ConnectGuardianForceEditor(() => new(source, session.CharacterVitals, [new("Example gameplay enhancements", inherited)], [], []), id => id == 0 ? "Quezacotl" : "Guardian Force " + id, AbilityName,
            _ => "Allows magic junctioning to improve a character's stats. Current earned AP is separate from this mod's starting state.",
            action => action switch { ControlAction.CameraLeft => "Ctrl+Left", ControlAction.CameraRight => "Ctrl+Right", ControlAction.Card => "A", ControlAction.RotateLeft => "LB", ControlAction.RotateRight => "RB", ControlAction.UiTab => "Tab", _ => action.ToString() },
            (document, reset, resets) => new(session.PreviewGuardianForceAbilities(GuardianForceAbilityComposer.Compose(source.ResolveGuardianForceTemplates(), [new("Selected", document)], source.MenuDefinitions.AbilityPoints).Templates), document, reset),
            _ => throw new InvalidOperationException("Rendering must not apply a draft."),
            (_, _, _) => throw new InvalidOperationException("Rendering must not save a draft."));
        return menu;
    }

    private static string AbilityName(int id)
    {
        string[] names = ["None", "HP-J", "Str-J", "Vit-J", "Mag-J", "Spr-J", "Spd-J", "Eva-J", "Hit-J", "Luck-J", "Elem-Atk-J", "ST-Atk-J", "Elem-Def-J", "ST-Def-J", "Elem-Defx2", "Elem-Defx4", "ST-Defx2", "ST-Defx4", "Abilityx3", "Abilityx4", "Magic", "GF", "Draw", "Item", "Card", "Doom", "Mad Rush", "Treatment", "Defend", "Darkside", "Recover", "Absorb", "Revive", "Level Down", "Level Up", "Kamikaze", "Devour", "MiniMog", "HP+20%", "HP+40%", "HP+80%", "Str+20%", "Str+40%", "Str+60%", "Vit+20%", "Vit+40%"];
        return id >= 0 && id < names.Length ? names[id] : "Catalog ability " + id;
    }

    private static NativeCharacterVitals Source(int abilityCount = 45)
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        var templates = Enumerable.Range(0, 16).Select(_ => Enumerable.Range(1, abilityCount).Select(ability => new GuardianForceAbilityTemplate(ability, ability % 5 == 0, 20, PrerequisiteAbility: ability == 3 ? 1 : null, LearningPriority: ability)).ToImmutableArray()).ToImmutableArray();
        return new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new('0', 64),
            KernelSha256 = new('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new(10, 10), 0, new(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new(0, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), Enumerable.Repeat((byte)20, 116).ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray())
            {
                Templates = templates,
                SourceTemplates = templates
            },
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
    }

    private sealed class SyntheticArtworkCanvas(BattlePreviewCanvas renderer) : IUiCanvas
    {
        public List<(string Value, UiRect Bounds)> Texts { get; } = [];
        public void Fill(UiRect bounds, UiColor color) => renderer.Fill(bounds, color);
        public void Paper(UiRect bounds, UiColor color) => renderer.Paper(bounds, color);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Text(text, fontSize, bounds, color, null, UiAlignment.Start, UiTextFlow.Auto);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? family) => Text(text, fontSize, bounds, color, family, UiAlignment.Start, UiTextFlow.Auto);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment vertical) => Text(text, fontSize, bounds, color, null, vertical, UiTextFlow.Auto);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment vertical, UiTextFlow flow) => Text(text, fontSize, bounds, color, null, vertical, flow);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? family, UiAlignment vertical, UiTextFlow flow)
        {
            Texts.Add((text, bounds));
            renderer.Text(text, fontSize, bounds, color, family, vertical, flow);
        }
        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => renderer.MeasureText(text, fontSize, maximumWidth);
        public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? family) => renderer.MeasureText(text, fontSize, maximumWidth, family);
        public void Image(UiImageSource source, UiRect bounds) => renderer.Fill(bounds, new UiColor(70, 100, 125));
        public void MenuPointer(UiRect bounds) => renderer.Fill(bounds, new UiColor(240, 205, 80));
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness) => renderer.Line(x1, y1, x2, y2, color, thickness);
        public void PushClip(UiRect bounds) => renderer.PushClip(bounds);
        public void PopClip() => renderer.PopClip();
    }
}
