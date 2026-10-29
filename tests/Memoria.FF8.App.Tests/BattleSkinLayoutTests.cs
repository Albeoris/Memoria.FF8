using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSkinLayoutTests
{
    [Theory]
    [InlineData(false, 1f)]
    [InlineData(false, 1.25f)]
    [InlineData(false, 3f)]
    [InlineData(true, 1f)]
    [InlineData(true, 1.25f * 20 / 24)]
    [InlineData(true, 1.25f)]
    [InlineData(true, 3f)]
    public void Command_rows_clear_the_skin_caption_and_keep_pointer_selection(bool vector, float textScale)
    {
        BattleHudController hud = CreateHud(textScale);
        UiTheme theme = vector ? new VectorUiTheme() : new UiTheme();
        UiView view = hud.BuildView();
        view.Resize(1920, 1080);
        LayoutCanvas canvas = new();
        view.Draw(canvas, theme);

        UiRect caption = Assert.Single(canvas.TextDraws, draw => draw.Text == "COMMAND").Bounds;
        UiRect first = Assert.Single(canvas.TextDraws, draw => draw.Text == "Attack").Bounds;
        Assert.True(first.Y + 0.01f >= caption.Bottom, $"First command {first} overlaps caption {caption}.");
        foreach (string label in hud.ChoiceLabels)
        {
            UiRect row = Assert.Single(canvas.TextDraws, draw => draw.Text == label).Bounds;
            Assert.InRange(row.Bottom, 0, 1080);
            Assert.Contains(canvas.Clips, clip => Math.Abs(clip.Y - row.Y) < 0.01f && Math.Abs(clip.Height - row.Height) < 0.01f);
        }

        UiRect magic = Assert.Single(canvas.TextDraws, draw => draw.Text == "Magic").Bounds;
        view.Pointer(magic.X + 2, magic.Y + magic.Height / 2, true);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
    }

    [Theory]
    [InlineData(false, 1f)]
    [InlineData(false, 1.25f)]
    [InlineData(false, 3f)]
    [InlineData(true, 1f)]
    [InlineData(true, 1.25f)]
    [InlineData(true, 3f)]
    public void Target_rows_clear_the_skin_caption_and_keep_pointer_selection(bool vector, float textScale)
    {
        BattleHudController hud = CreateHud(textScale);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        UiView view = hud.BuildView();
        view.Resize(1920, 1080);
        LayoutCanvas canvas = new();
        view.Draw(canvas, vector ? new VectorUiTheme() : new UiTheme());

        UiRect caption = Assert.Single(canvas.TextDraws, draw => draw.Text == "TARGET  < ENEMIES >").Bounds;
        UiRect first = Assert.Single(canvas.TextDraws, draw => draw.Text == "Enemy A").Bounds;
        Assert.True(first.Y + 0.01f >= caption.Bottom, $"First target {first} overlaps caption {caption}.");
        UiRect second = Assert.Single(canvas.TextDraws, draw => draw.Text == "Enemy B").Bounds;
        Assert.InRange(second.Bottom, 0, 1080);
        view.Pointer(second.X + 2, second.Y + second.Height / 2, false);
        Assert.Equal("enemy-b", hud.SelectedTarget!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Long_ally_targets_stay_left_of_party_vitals_and_keep_their_identity(bool vector)
    {
        const string allyName = "A very long guardian force and party member name";
        BattleHudController hud = CreateHud(1.25f, allyName);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Item));
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        UiView view = hud.BuildView();
        view.Resize(1920, 1080);
        LayoutCanvas canvas = new();
        view.Draw(canvas, vector ? new VectorUiTheme() : new UiTheme());

        var names = canvas.TextDraws.Where(draw => draw.Text == allyName).OrderBy(draw => draw.Bounds.X).ToArray();
        Assert.Equal(2, names.Length);
        UiRect target = names[0].Bounds;
        UiRect vital = names[1].Bounds;
        Assert.True(target.Right <= vital.X, $"Ally target {target} overlaps party vitals {vital}.");
        Assert.Contains(canvas.Clips, clip => clip.X == target.X && clip.Y == target.Y && clip.Width == target.Width);
        view.Pointer(target.X + 2, target.Y + target.Height / 2, false);
        Assert.Equal("ally", hud.SelectedTarget!.Id);
    }

    [Fact]
    public void Installed_pointer_and_text_render_command_enemy_and_ally_windows_for_both_skins()
    {
        string gameDirectory = TestResources.GameDirectory;
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RESULT_UI_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        foreach (bool vector in new[] { false, true })
        {
            foreach (float textScale in new[] { 1.25f * 20 / 24, 3f })
            {
                foreach (string screen in new[] { "commands", "enemies", "allies" })
                {
                    BattleHudController hud = CreateHud(textScale, "Quistis");
                    if (screen == "allies")
                    {
                        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Item));
                    }
                    if (screen != "commands")
                    {
                        hud.Navigate(MenuInput.Confirm);
                    }
                    using Bitmap bitmap = new(1920, 1080, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
                    using (BattlePreviewCanvas canvas = new(bitmap, gameDirectory))
                    {
                        UiView view = hud.BuildView();
                        view.Resize(bitmap.Width, bitmap.Height);
                        view.Draw(canvas, vector ? new VectorUiTheme() : new UiTheme());
                    }
                    Assert.Equal(SkiaSharp.SKColors.Black.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
                    if (output is not null)
                    {
                        bitmap.SavePng(Path.Combine(output, $"hud-{(vector ? "vector" : "classic")}-{screen}-{textScale:0.00}.png"));
                    }
                }
            }
        }
    }

    private static BattleHudController CreateHud(float textScale, string? allyName = null)
    {
        BattleDefinition definition = new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Item, BattleAbility.Draw] },
                new() { Id = "enemy-a", Name = "Enemy A" },
                new() { Id = "enemy-b", Name = "Enemy B" },
                .. (allyName is null ? Array.Empty<BattleCombatantDefinition>() : [new BattleCombatantDefinition { Id = "ally", Name = allyName, IsPlayer = true }])
            ],
            Items = [new(1, 5)],
            ActionDefinitions = [new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Heal, 4, true)]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        return new BattleHudController(session, session.QueueAttack, () => { }, () => { }) { TextScale = textScale };
    }

    private sealed class LayoutCanvas : IUiCanvas
    {
        public List<(string Text, UiRect Bounds)> TextDraws { get; } = [];
        public List<UiRect> Clips { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return MeasureText(text, fontSize, maximumWidth, "Georgia");
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? fontFamily)
        {
            var measured = SystemTextLayout.MeasureWrapped(text, fontSize, maximumWidth, fontFamily ?? "Georgia");
            return new UiSize(measured.X, measured.Y);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            TextDraws.Add((text, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
            Clips.Add(bounds);
        }

        public void PopClip()
        {
        }
    }
}
