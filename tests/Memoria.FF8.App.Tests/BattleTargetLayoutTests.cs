using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTargetLayoutTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void EveryTargetRemainsVisibleInFourRowsAndSecondColumnCanBeSubmitted(int count)
    {
        foreach (float textScale in new[] { 0.25f, 1f, 1.5f, 3f })
        {
            foreach ((int width, int height) in new[] { (960, 672), (640, 448) })
            {
                List<BattleInput> inputs = [];
                BattleHudController hud = Create(count, inputs);
                hud.TextScale = textScale;
                UiView view = hud.BuildView();
                view.Resize(width, height);
                Canvas canvas = new();
                view.Draw(canvas, new UiTheme());
                var names = canvas.Labels.Where(label => label.Text.StartsWith("Enemy ", StringComparison.Ordinal)).ToArray();
                Assert.Equal(count, names.Length);
                Assert.All(names, label => Assert.InRange(label.Bounds.Y, 0, height - label.Bounds.Height));
                Assert.All(names, label => Assert.True(label.Bounds.Right <= 496 * width / 960f + 0.01f));
                Assert.All(names, label => Assert.True(label.Clip.X <= label.Bounds.X && label.Clip.Y <= label.Bounds.Y && label.Clip.Right >= label.Bounds.Right - 0.01f && label.Clip.Bottom >= label.Bounds.Bottom - 0.01f));
                Assert.Equal(Math.Min(count, 4), names.Select(label => label.Bounds.Y).Distinct().Count());
                if (count <= 4)
                {
                    continue;
                }
                Assert.Equal(names[0].Bounds.Y, names[4].Bounds.Y);
                Assert.True(names[4].Bounds.X > names[0].Bounds.Right);
                view.Key(UiKey.Right);
                Assert.Equal("enemy-4", hud.SelectedTarget!.Id);
                view.Key(UiKey.Confirm);
                Assert.Equal("enemy-4", Assert.Single(inputs).TargetId);
                inputs.Clear();
                hud.Navigate(MenuInput.Confirm);
                var last = names[^1].Bounds;
                view.Pointer(last.X + last.Width / 2, last.Y + last.Height / 2, false);
                Assert.Equal($"enemy-{count - 1}", hud.SelectedTarget!.Id);
                view.Pointer(last.X + last.Width / 2, last.Y + last.Height / 2, true);
                Assert.Equal($"enemy-{count - 1}", Assert.Single(inputs).TargetId);
            }
        }
    }

    [Fact]
    public void SyntheticAllTargetActionKeepsGroupPointersAndIgnoresSingleTargetNavigation()
    {
        BattleActionDefinition action = new(BattleAbility.Magic, 1, "Group spell", BattleActionEffect.Damage, 10, false, AllTargets: true);
        VerifyGroup(Create(8, [], action));
    }

    [Fact]
    public void InstalledShivaUsesNativeGroupFlagsAndOptionallyRendersTargetPreviews()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition shiva = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == 1);
        Assert.Equal((byte?)0x48, shiva.NativeAction!.TargetFlags);
        VerifyGroup(Create(8, [], shiva));
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_UI_OUTPUT");
        if (string.IsNullOrEmpty(output))
        {
            return;
        }
        Directory.CreateDirectory(output);
        foreach (int count in new[] { 4, 8 })
        {
            foreach ((int width, int height) in new[] { (960, 672), (640, 448) })
            {
                BattleHudController hud = Create(count, [], shiva);
                hud.TextScale = 1.5f;
                using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
                using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
                UiView view = hud.BuildView();
                view.Resize(width, height);
                view.Draw(canvas, new UiTheme());
                bitmap.Save(Path.Combine(output, $"shiva-targets-{count}-scale150-{width}x{height}.png"), ImageFormat.Png);
            }
        }
    }

    private static void VerifyGroup(BattleHudController hud)
    {
        Assert.True(hud.SelectingTargetGroup);
        string selected = hud.SelectedTarget!.Id;
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        Canvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Equal(8, canvas.Pointers.Count);
        Assert.Contains(canvas.Labels, label => label.Text == "All enemies");
        view.Key(UiKey.Right);
        view.Key(UiKey.Down);
        var last = canvas.Labels.Last(label => label.Text == "Enemy 7").Bounds;
        view.Pointer(last.X + 10, last.Y + 10, false);
        Assert.Equal(selected, hud.SelectedTarget!.Id);
    }

    private static BattleHudController Create(int count, List<BattleInput> inputs, BattleActionDefinition? action = null)
    {
        BattleCombatantDefinition player = new() { Id = "party", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [action?.Ability ?? BattleAbility.Attack], MagicStocks = [new(1, 10)], EquipmentIds = [1] };
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [player, .. Enumerable.Range(0, count).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = $"Enemy {index}" })],
            ActionDefinitions = action is null ? [] : [action]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: inputs.Add);
        hud.Navigate(MenuInput.Confirm);
        if (action is not null)
        {
            hud.Navigate(MenuInput.Confirm);
        }
        Assert.True(hud.SelectingTarget);
        return hud;
    }

    private sealed class Canvas : IUiCanvas
    {
        private readonly Stack<UiRect> clips = new();
        public List<(string Text, UiRect Bounds, UiRect Clip)> Labels { get; } = [];
        public List<UiRect> Pointers { get; } = [];
        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.5f), fontSize);
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Labels.Add((text, bounds, clips.Count > 0 ? clips.Peek() : bounds));
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }
        public void PushClip(UiRect bounds)
        {
            clips.Push(clips.Count > 0 ? clips.Peek().Intersect(bounds) : bounds);
        }
        public void PopClip()
        {
            clips.Pop();
        }
        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
        }
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
    }
}
