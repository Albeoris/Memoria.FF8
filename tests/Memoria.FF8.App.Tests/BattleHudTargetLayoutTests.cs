using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudTargetLayoutTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void SmallTargetListsKeepTheNativePanelGeometry(int count)
    {
        BattleHudController hud = Create(count);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = Draw(view);
        var rows = TargetRows(canvas);
        Assert.Equal(count, rows.Length);
        float expectedWidth = Math.Clamp(6 * MathF.Floor((TargetName(0).Length * 12 + 54) / 6), 324, 442) - 42;
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(TargetName(index), rows[index].Text);
            Assert.Equal(new UiRect(84, 480 + index * 39, expectedWidth, 39), rows[index].Bounds);
        }
        view.Pointer(rows[^1].Bounds.X + 2, rows[^1].Bounds.Y + 2, false);
        Assert.Equal(TargetId(count - 1), hud.SelectedTarget?.Id);
    }

    [Theory]
    [InlineData(5, 640, 448, 1f)]
    [InlineData(5, 1280, 720, 1.65f)]
    [InlineData(11, 640, 448, 1.65f)]
    [InlineData(11, 1280, 720, 3f)]
    [InlineData(14, 640, 448, 3f)]
    [InlineData(14, 1280, 720, 1f)]
    public void LargeListsRevealStableTargetIdsAndOnlyHitVisibleRows(int count, int width, int height, float textScale)
    {
        List<string> confirmed = [];
        BattleHudController hud = Create(count, confirmed);
        hud.TextScale = textScale;
        UiView view = hud.BuildView();
        view.Resize(width, height);
        BattleUiTests.TestCanvas first = Draw(view);
        var firstRows = TargetRows(first);
        Assert.InRange(firstRows.Length, 1, Math.Min(7, count));
        Assert.Equal(TargetName(0), firstRows[0].Text);
        AssertGeometry(first, firstRows.Select(row => row.Bounds).ToArray(), width, height, textScale);
        Assert.Contains(first.Texts, text => text.EndsWith($" / {count}", StringComparison.Ordinal));

        view.Key(UiKey.Up);
        Assert.Equal(TargetId(count - 1), hud.SelectedTarget?.Id);
        BattleUiTests.TestCanvas last = Draw(view);
        var lastRows = TargetRows(last);
        Assert.Equal(TargetName(count - 1), lastRows[^1].Text);
        AssertGeometry(last, lastRows.Select(row => row.Bounds).ToArray(), width, height, textScale);
        foreach (var row in lastRows)
        {
            int target = Enumerable.Range(0, count).Single(index => TargetName(index) == row.Text);
            view.Pointer(row.Bounds.X + 5, row.Bounds.Y + row.Bounds.Height / 2, false);
            Assert.Equal(TargetId(target), hud.SelectedTarget?.Id);
        }
        var footer = Assert.Single(last.TextDraws, draw => draw.Text.EndsWith($" / {count}", StringComparison.Ordinal));
        view.Pointer(footer.Bounds.X + 2, footer.Bounds.Y + 2, true);
        Assert.Empty(confirmed);
        Assert.Equal(TargetId(count - 1), hud.SelectedTarget?.Id);

        view.Key(UiKey.Down);
        Assert.Equal(TargetId(0), hud.SelectedTarget?.Id);
        var firstAgain = TargetRows(Draw(view));
        Assert.Equal(TargetName(0), firstAgain[0].Text);
        view.Pointer(firstAgain[^1].Bounds.X + 5, firstAgain[^1].Bounds.Y + 2, false);
        view.Pointer(firstAgain[^1].Bounds.X + 5, firstAgain[^1].Bounds.Y + 2, true);
        Assert.Equal(TargetId(firstAgain.Length - 1), Assert.Single(confirmed));
    }

    [Fact]
    public void FooterClickMovesTheViewportAndSelectionWithoutConfirming()
    {
        List<string> confirmed = [];
        BattleHudController hud = Create(14, confirmed);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas first = Draw(view);
        int count = TargetRows(first).Length;
        UiRect next = Assert.Single(first.TextDraws, draw => draw.Text == "▼").Bounds;
        Click(view, next);
        BattleUiTests.TestCanvas second = Draw(view);
        Assert.Equal(TargetId(count), hud.SelectedTarget?.Id);
        Assert.Equal(TargetName(count), TargetRows(second)[0].Text);
        UiRect previous = Assert.Single(second.TextDraws, draw => draw.Text == "▲").Bounds;
        Click(view, previous);
        Assert.Equal(TargetId(0), hud.SelectedTarget?.Id);
        Assert.Equal(TargetName(0), TargetRows(Draw(view))[0].Text);
        Assert.Empty(confirmed);
    }

    [Fact]
    public void GroupTargetPagingOnlyChangesTheVisibleRows()
    {
        BattleDefinition definition = CreateDefinition(14);
        definition.Combatants[0] = definition.Combatants[0] with { Abilities = [BattleAbility.Item] };
        BattleNativeAction native = new(4, 1, 7, 1) { MagicId = 0, AttackType = 2, Power = 10, HitCount = 1, TargetFlags = 0x20, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession battle = new(definition with { Items = [new(1, 1)], ActionDefinitions = [new(BattleAbility.Item, 1, "Group item", BattleActionEffect.Damage, 10, false, AllTargets: true) { NativeAction = native }] }, BattleAtbMode.TurnBased);
        List<BattleInput> commands = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: commands.Add);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTargetGroup);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas first = Draw(view);
        string[] targetIds = hud.Targets.Select(actor => actor.Id).ToArray();
        string? selectedId = hud.SelectedTarget?.Id;
        Click(view, Assert.Single(first.TextDraws, draw => draw.Text == "▼").Bounds);
        var second = TargetRows(Draw(view));
        Assert.NotEqual(TargetRows(first)[0].Text, second[0].Text);
        Assert.Equal(targetIds, hud.Targets.Select(actor => actor.Id));
        Assert.Equal(selectedId, hud.SelectedTarget?.Id);
        Assert.Empty(commands);
    }

    [Fact]
    public void RenderLargeTargetListsWhenOutputIsRequested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_LIST_PREVIEW_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_TARGET_LIST_PREVIEW_OUTPUT to a scratch directory for the target previews.");
        Directory.CreateDirectory(output!);
        string directory = TestResources.GameDirectory;
        foreach (int count in new[] { 5, 14 })
        {
            BattleHudController hud = Create(count);
            if (count == 14)
            {
                hud.TextScale = 1.65f;
                hud.Navigate(MenuInput.Up);
            }
            UiView view = hud.BuildView();
            using SkiaSharp.SKBitmap bitmap = new(1280, 720);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            view.Resize(bitmap.Width, bitmap.Height);
            view.Draw(canvas, new UiTheme());
            bitmap.SavePng(Path.Combine(output!, $"targets-{count}.png"));
        }
    }

    private static BattleHudController Create(int count, List<string>? confirmed = null)
    {
        BattleSession battle = new(CreateDefinition(count), BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, target) => confirmed?.Add(target), () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        return hud;
    }

    private static BattleDefinition CreateDefinition(int count)
    {
        return new()
        {
            UsesExpandedRoster = count > 4,
            Combatants =
            [
                new() { Id = "ally", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack] },
                .. Enumerable.Range(0, count).Select(index => new BattleCombatantDefinition { Id = TargetId(index), Name = TargetName(index) })
            ]
        };
    }

    private static BattleUiTests.TestCanvas Draw(UiView view)
    {
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        return canvas;
    }

    private static (string Text, UiRect Bounds, UiColor Color)[] TargetRows(BattleUiTests.TestCanvas canvas)
    {
        return canvas.TextDraws.Where(draw => draw.Text.StartsWith("Garden Guard", StringComparison.Ordinal)).ToArray();
    }

    private static void AssertGeometry(BattleUiTests.TestCanvas canvas, UiRect[] rows, int width, int height, float textScale)
    {
        BattleHudLayout viewport = new(new UiRect(0, 0, width, height));
        foreach (UiRect row in rows)
        {
            Assert.Equal(400 * viewport.Scale, row.Width, 2);
            Assert.True(row.Height >= 24 * textScale * viewport.Scale - 0.01f);
            Assert.True(row.Right < viewport.Rect(BattleHudLayout.PartyLeft, 0, 0, 0).X);
            Assert.True(row.Bottom <= viewport.Rect(0, 660, 0, 0).Y);
        }
        for (int index = 1; index < rows.Length; index++)
        {
            Assert.Equal(rows[0].X, rows[index].X);
            Assert.True(rows[index - 1].Bottom <= rows[index].Y + 0.01f);
        }
        UiRect action = Assert.Single(canvas.TextDraws, draw => draw.Text == "Attack").Bounds;
        Assert.True(action.Y >= viewport.Rect(0, 108, 0, 0).Y);
        Assert.True(action.Bottom <= rows[0].Y);
    }

    private static void Click(UiView view, UiRect bounds)
    {
        view.Pointer(bounds.X + 1, bounds.Y + 1, false);
        view.Pointer(bounds.X + 1, bounds.Y + 1, true);
    }

    private static string TargetId(int index) => $"enemy-{13 - index}";
    private static string TargetName(int index) => $"Garden Guard {index + 1:00}";
}
