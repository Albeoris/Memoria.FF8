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
    [InlineData(4, 1, 640, 480, 1f)]
    [InlineData(5, 2, 1280, 720, 1f)]
    [InlineData(8, 2, 640, 480, 1.65f)]
    [InlineData(9, 2, 1280, 720, 1.65f)]
    [InlineData(16, 2, 800, 600, 1f)]
    [InlineData(17, 3, 1280, 720, 1f)]
    [InlineData(25, 3, 800, 600, 3f)]
    public void GridCellsMatchDrawOrderHitTestingAndSelectedVisibility(int count, int columns, int width, int height, float textScale)
    {
        BattleUiTests.TestCanvas canvas = new();
        BattleHudLayout viewport = new(new(0, 0, width, height));
        string[] names = Enumerable.Range(0, count).Select(index => TargetName(index) + " with a long localized name").ToArray();
        BattleHudTargetList list = viewport.MeasureTargetList(canvas, new UiTheme(), names, textScale, 95 * textScale, viewport.ViewportRight - 80, 0, count - 1);
        BattleDrawLayout grid = BattleDrawLayout.Calculate(count);
        Assert.Equal(columns, grid.Columns);
        Assert.Equal(grid, list.Grid);
        Assert.Contains(count - 1, list.VisibleIndices());
        if (textScale == 1f)
        {
            Assert.Equal(Enumerable.Range(0, count), list.VisibleIndices());
        }
        foreach (int index in list.VisibleIndices())
        {
            UiRect cell = list.Cell(index);
            UiRect screen = viewport.Rect(cell.X, cell.Y, cell.Width, cell.Height);
            Assert.InRange(screen.X, 0, width);
            Assert.InRange(screen.Right, 0, width);
            Assert.InRange(screen.Bottom, 0, height);
            Assert.Equal(index, list.HitIndex(viewport, screen.X + screen.Width / 2, screen.Y + screen.Height / 2));
        }
        for (int column = 0; column < columns; column++)
        {
            int last = grid.ColumnRows(column);
            if (last >= list.StartRow && last < list.EndRow)
            {
                UiRect gap = viewport.Rect(list.Panel.X + (column + 0.5f) * list.ColumnWidth, list.Panel.Y + list.ContentOffset + (last - list.StartRow + 0.5f) * list.RowHeight, 0, 0);
                Assert.Equal(-1, list.HitIndex(viewport, gap.X, gap.Y));
            }
        }
    }

    [Theory]
    [InlineData(5, 640, 480)]
    [InlineData(8, 1280, 720)]
    [InlineData(9, 800, 600)]
    public void ArrowsAndPointerFollowTheSameColumnMajorTargets(int count, int width, int height)
    {
        List<string> confirmed = [];
        BattleHudController hud = Create(count, confirmed);
        UiView view = hud.BuildView();
        view.Resize(width, height);
        BattleUiTests.TestCanvas first = Draw(view);
        Assert.Equal(0, first.ClipDepth);
        var rows = TargetRows(first);
        BattleDrawLayout grid = BattleDrawLayout.Calculate(count);
        Assert.Equal(count, rows.Length);
        Assert.Equal(grid.Columns, rows.Select(row => row.Bounds.X).Distinct().Count());
        view.Key(UiKey.Up);
        int lastFirstColumn = grid.ColumnRows(0) - 1;
        Assert.Equal(TargetId(lastFirstColumn), hud.SelectedTarget?.Id);
        view.Key(UiKey.Right);
        int lastSecondColumn = grid.GetIndex(1, grid.ColumnRows(1) - 1);
        Assert.Equal(TargetId(lastSecondColumn), hud.SelectedTarget?.Id);

        if (grid.ColumnRows(1) < grid.ColumnRows(0))
        {
            UiRect gap = rows[lastSecondColumn].Bounds with { Y = rows[lastFirstColumn].Bounds.Y };
            Click(view, gap);
            Assert.Empty(confirmed);
            Assert.Equal(TargetId(lastSecondColumn), hud.SelectedTarget?.Id);
        }

        view.Key(UiKey.Right);
        Assert.True(hud.SelectedTarget?.IsPlayer);
        Draw(view);
        view.Key(UiKey.Left);
        Assert.Equal(TargetId(0), hud.SelectedTarget?.Id);
        rows = TargetRows(Draw(view));
        Click(view, rows[^1].Bounds);
        Assert.Equal(TargetId(count - 1), Assert.Single(confirmed));
    }

    [Fact]
    public void LargeTargetPopupDrawsOverNativePartyWithMeasuredHelpText()
    {
        BattleHudController hud = Create(14);
        hud.TextScale = 3;
        hud.PhysicalOverrideBindingHint = "Left";
        UiView view = hud.BuildView();
        view.Resize(800, 600);
        BattleHudLayout viewport = new(new(0, 0, 800, 600));
        BattleUiTests.TestCanvas canvas = Draw(view);
        var rows = TargetRows(canvas);
        Assert.NotEmpty(rows);
        Assert.InRange(canvas.Texts.IndexOf("Ally 1"), 0, canvas.Texts.IndexOf(rows[0].Text) - 1);
        var help = Assert.Single(canvas.TextDraws, draw => draw.Text == hud.PhysicalOverrideCaption);
        UiSize expected = canvas.MeasureText(help.Text, 15 * hud.TextScale * viewport.Scale, float.PositiveInfinity);
        Assert.InRange(help.Bounds.Width, Math.Min(expected.Width, (viewport.ViewportRight - 108) * viewport.Scale) - 0.01f, 800);
        Assert.True(help.Bounds.Bottom < rows.Min(row => row.Bounds.Y));
    }

    [Fact]
    public void LargeTargetNamesWrapWithoutLosingTheirIdentifyingSuffixes()
    {
        BattleHudController hud = Create(14);
        hud.TextScale = 3;
        hud.Navigate(MenuInput.Up);
        UiView view = hud.BuildView();
        view.Resize(800, 600);
        BattleHudLayout viewport = new(new(0, 0, 800, 600));
        BattleUiTests.TestCanvas canvas = new() { WrapMeasuredText = true };
        view.Draw(canvas, new UiTheme());

        string selected = Assert.IsType<BattleCombatant>(hud.SelectedTarget).Name;
        var rows = TargetRows(canvas);
        Assert.Contains(rows, row => row.Text == selected);
        Assert.Contains(rows, row => row.Text == TargetName(13));
        Assert.InRange(rows.Length, 2, 13);
        float font = 24 * hud.TextScale * viewport.Scale;
        Assert.All(rows, row =>
        {
            Assert.Contains(row.Text, Enumerable.Range(0, 14).Select(TargetName));
            UiSize measured = canvas.MeasureText(row.Text, font, row.Bounds.Width);
            Assert.True(measured.Height > font);
            Assert.InRange(measured.Height, font, row.Bounds.Height + 0.01f);
            Assert.InRange(row.Bounds.X, 0, 800 - row.Bounds.Width + 0.01f);
            Assert.InRange(row.Bounds.Y, 0, 600 - row.Bounds.Height + 0.01f);
        });
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void KeyboardAndFooterRevealTheSameTargetWithoutConfirming()
    {
        List<string> confirmed = [];
        BattleHudController hud = Create(14, confirmed);
        hud.TextScale = 3;
        UiView view = hud.BuildView();
        view.Resize(1280, 720);
        BattleUiTests.TestCanvas first = Draw(view);
        var firstRows = TargetRows(first);
        int rowsPerPage = firstRows.Count(row => row.Bounds.X == firstRows[0].Bounds.X);
        int nextRow = Math.Min(rowsPerPage, BattleDrawLayout.Calculate(14).ColumnRows(0) - rowsPerPage);
        Click(view, Assert.Single(first.TextDraws, draw => draw.Text == "▼").Bounds);
        Assert.Equal(TargetId(nextRow), hud.SelectedTarget?.Id);
        Draw(view);
        view.Key(UiKey.PageUp);
        Assert.Equal(TargetId(0), hud.SelectedTarget?.Id);
        Assert.Empty(confirmed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TargetPointerRequiresRedrawAfterViewportOrTextScaleChanges(bool resize)
    {
        List<string> confirmed = [];
        BattleHudController hud = Create(9, confirmed);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        UiRect oldRow = TargetRows(Draw(view))[1].Bounds;
        if (resize)
        {
            view.Resize(1280, 720);
            oldRow = new BattleHudLayout(new UiRect(0, 0, 1280, 720)).Rect(oldRow.X, oldRow.Y, oldRow.Width, oldRow.Height);
        }
        else
        {
            hud.TextScale = 1.65f;
        }
        Click(view, oldRow);
        Assert.Equal(TargetId(0), hud.SelectedTarget?.Id);
        Assert.Empty(confirmed);

        Click(view, TargetRows(Draw(view))[1].Bounds);
        Assert.Equal(TargetId(1), Assert.Single(confirmed));
    }

    [Fact]
    public void GroupTargetPagingChangesVisibleCellsWithoutRetargetingEitherSide()
    {
        BattleHudController hud = CreateGroup(14, 11);
        hud.TextScale = 3;
        UiView view = hud.BuildView();
        view.Resize(1280, 720);
        BattleUiTests.TestCanvas first = Draw(view);
        string[] targetIds = hud.Targets.Select(actor => actor.Id).ToArray();
        Assert.Equal(25, targetIds.Length);
        string? selectedId = hud.SelectedTarget?.Id;
        string firstRange = Assert.Single(first.Texts, text => text.EndsWith(" / 9", StringComparison.Ordinal));
        Click(view, first.TextDraws.Last(draw => draw.Text == "▼").Bounds);
        string secondRange = Assert.Single(Draw(view).Texts, text => text.EndsWith(" / 9", StringComparison.Ordinal));
        Assert.NotEqual(firstRange, secondRange);
        Assert.Equal(targetIds, hud.Targets.Select(actor => actor.Id));
        Assert.Equal(selectedId, hud.SelectedTarget?.Id);
    }

    [Fact]
    public void RenderLargeTargetListsWhenOutputIsRequested()
    {
        RenderPreview(Create(4, allies: 6, selectingTarget: false), "party-6", 1280, 720);
        RenderPreview(CreateGroup(14, 11), "party-11-target-25", 1280, 720);
    }

    [Fact]
    public void RenderWrappedLargeTargetNamesWhenOutputIsRequested()
    {
        BattleHudController hud = Create(14);
        hud.TextScale = 3;
        hud.Navigate(MenuInput.Up);
        RenderPreview(hud, "targets-large-text-last-page", 800, 600);
    }

    private static void RenderPreview(BattleHudController hud, string name, int width, int height)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_LIST_PREVIEW_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_TARGET_LIST_PREVIEW_OUTPUT to a scratch directory for the target previews.");
        Directory.CreateDirectory(output!);
        UiView view = hud.BuildView();
        using SkiaSharp.SKBitmap bitmap = new(width, height);
        using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
        view.Resize(width, height);
        view.Draw(canvas, new UiTheme());
        bitmap.SavePng(Path.Combine(output!, name + ".png"));
    }

    private static BattleHudController Create(int count, List<string>? confirmed = null, int allies = 1, bool selectingTarget = true)
    {
        BattleSession battle = new(CreateDefinition(count, allies), BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, target) => confirmed?.Add(target), () => { }, () => { });
        if (selectingTarget)
        {
            hud.Navigate(MenuInput.Confirm);
            Assert.True(hud.SelectingTarget);
        }
        return hud;
    }

    private static BattleHudController CreateGroup(int enemies, int allies)
    {
        BattleDefinition definition = CreateDefinition(enemies, allies);
        definition.Combatants[0] = definition.Combatants[0] with { Abilities = [BattleAbility.Item] };
        BattleNativeAction native = new(4, 1, 7, 1) { MagicId = 0, AttackType = 2, Power = 10, HitCount = 1, TargetFlags = 0x20, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession battle = new(definition with { Items = [new(1, 1)], ActionDefinitions = [new(BattleAbility.Item, 1, "Group item", BattleActionEffect.Damage, 10, false, AllTargets: true) { NativeAction = native }] }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: _ => throw new Xunit.Sdk.XunitException("Browsing should not confirm the action."));
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTargetGroup);
        return hud;
    }

    private static BattleDefinition CreateDefinition(int enemies, int allies = 1)
    {
        return new()
        {
            Combatants =
            [
                .. Enumerable.Range(1, allies).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack] }),
                .. Enumerable.Range(0, enemies).Select(index => new BattleCombatantDefinition { Id = TargetId(index), Name = TargetName(index) })
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
        return canvas.TextDraws.Where(draw => draw.Text.StartsWith("Garden", StringComparison.Ordinal)).ToArray();
    }

    private static void Click(UiView view, UiRect bounds)
    {
        view.Pointer(bounds.X + 1, bounds.Y + 1, false);
        view.Pointer(bounds.X + 1, bounds.Y + 1, true);
    }

    private static string TargetId(int index) => $"enemy-{13 - index}";
    private static string TargetName(int index) => $"Garden Guard {index + 1:00}";
}
