using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleResistanceHintLayoutTests
{
    [Fact]
    public void SupplementalStatusSymbolsUseTheSameArtworkForScanAndTargetPercentages()
    {
        BattleUiTests.TestCanvas canvas = new();
        BattleResistanceBadge[] badges = new[] { NativeStatusId.Doom, NativeStatusId.Petrifying, NativeStatusId.Vit0 }.Select(status =>
        {
            TargetScanResistance scan = TargetScanScreen.Status(status, 20, null);
            return new BattleResistanceBadge(scan.Icon, scan.Name, "20%", BattleResistanceTone.Favorable);
        }).ToArray();
        BattleResistanceGrid grid = BattleResistanceGrid.Measure(canvas, 1, 1, badges);
        BattleHudElement.DrawResistanceBadges(canvas, new UiTheme(), 1, grid, badges, new(0, 0, grid.Width, grid.Height));
        Assert.Equal(badges.Select(badge => badge.Icon!.Value), canvas.Images.Select(image => image.Source));
        Assert.Equal(3, canvas.Images.Select(image => image.Source).Distinct().Count());
        Assert.All(canvas.Texts, text => Assert.Equal("20%", text));
        TargetScanResistance fallback = TargetScanScreen.Status(NativeStatusId.Haste, 0, (_, _) => "Vitesse");
        Assert.Null(fallback.Icon);
        Assert.Equal("Vitesse", fallback.Name);
    }

    [Fact]
    public void ScanIconsAndFallbackNamesKeepTheirOwnValuesAndColors()
    {
        UiTheme theme = new();
        BattleUiTests.TestCanvas canvas = new();
        BattleResistanceBadge[] badges =
        [
            new(new UiImageSource(UiImageKind.Icon, 288), "Fire", "-50%", BattleResistanceTone.Favorable),
            new(new UiImageSource(UiImageKind.Icon, 279), "Sleep", "??", BattleResistanceTone.Normal),
            new(null, "Haste", "100%", BattleResistanceTone.Immune)
        ];
        BattleResistanceGrid grid = BattleResistanceGrid.Measure(canvas, 1, 1, badges).Fit(82, 120);
        Assert.Equal(2, grid.Columns);
        UiRect area = new(30, 40, grid.Width, grid.Height);
        BattleHudElement.DrawResistanceBadges(canvas, theme, 1, grid, badges, area);
        Assert.Collection(canvas.Images,
            fire => AssertBadge(fire, 288, "-50%", BattleResistanceTone.Favorable),
            sleep => AssertBadge(sleep, 279, "??", BattleResistanceTone.Normal));
        var label = Assert.Single(canvas.TextDraws, item => item.Text == "Haste");
        var percent = Assert.Single(canvas.TextDraws, item => item.Text == "100%");
        Assert.True(label.Bounds.Y > canvas.Images[0].Bounds.Bottom);
        Assert.Equal(label.Bounds.Bottom, percent.Bounds.Y);
        Assert.Equal(theme.Muted, percent.Color);
        Assert.All(canvas.TextDraws, draw => Assert.Equal(draw.Bounds, draw.Bounds.Intersect(area)));

        void AssertBadge((UiImageSource Source, UiRect Bounds) image, int icon, string value, BattleResistanceTone tone)
        {
            Assert.Equal(new UiImageSource(UiImageKind.Icon, icon), image.Source);
            var draw = Assert.Single(canvas.TextDraws, item => item.Text == value);
            Assert.Equal(image.Bounds.Bottom, draw.Bounds.Y);
            Assert.InRange(image.Bounds.X + image.Bounds.Width / 2, draw.Bounds.X, draw.Bounds.Right);
            Assert.Equal(tone.Color(theme), draw.Color);
        }
    }

    [Theory]
    [InlineData(3, 442, 1)]
    [InlineData(3, 442, 2)]
    [InlineData(8, 400, 1)]
    [InlineData(8, 400, 2)]
    public void TargetHintsFitBesideNamesWithoutChangingTargetRows(int targets, int maximumWidth, float textScale)
    {
        BattleUiTests.TestCanvas canvas = new() { WrapMeasuredText = true };
        BattleHudLayout viewport = new(new UiRect(0, 0, 960, 672));
        string[] names = Enumerable.Range(1, targets).Select(index => $"Garden Guard {index}").ToArray();
        BattleResistanceBadge[] badges = [new(new UiImageSource(UiImageKind.Icon, 288), "Fire", "-100%", BattleResistanceTone.Favorable), new(new UiImageSource(UiImageKind.Icon, 279), "Sleep", "100%", BattleResistanceTone.Immune)];
        BattleTargetResistanceRow[] hints = new BattleTargetResistanceRow[targets];
        hints[^1] = new() { Badges = badges, Grid = BattleResistanceGrid.Measure(canvas, 1, textScale, badges) };
        BattleHudTargetList plain = viewport.MeasureTargetList(canvas, new(), names, textScale, 90, maximumWidth, 0, targets - 1);
        BattleHudTargetList list = viewport.MeasureTargetList(canvas, new(), names, textScale, 90, maximumWidth, 0, targets - 1, hints);
        Assert.Equal(plain, list);
        Assert.Contains(targets - 1, list.VisibleIndices());
        foreach (int index in list.VisibleIndices())
        {
            UiRect cell = list.Cell(index);
            BattleTargetResistanceRow row = hints[index];
            if (index == targets - 1)
            {
                UiRect strip = new(cell.X + 30 + row.BadgeX, cell.Y, row.Grid.Width, row.Grid.Height);
                Assert.True(row.BadgeX > BattleTargetResistanceRow.Gap);
                Assert.True(strip.Right <= cell.Right - 12 + 0.01f);
                Assert.True(strip.Bottom <= cell.Y + (list.Expanded ? cell.Height - list.StatusHeight : 25) + 0.01f);
                Assert.Equal(index, list.HitIndex(viewport, strip.Right - 1, strip.Bottom - 1));
            }
            else
            {
                Assert.Equal(default, row);
            }
        }
    }

    [Theory]
    [InlineData(680)]
    [InlineData(442)]
    [InlineData(260)]
    public void TwelveKnownAndUnknownResistancesShareColumnsAcrossTargetNames(int maximumWidth)
    {
        BattleUiTests.TestCanvas canvas = new() { WrapMeasuredText = true };
        BattleHudLayout viewport = new(new UiRect(0, 0, 960, 672));
        string[] names = ["A", "Caller"];
        BattleTargetResistanceRow[] rows = new BattleTargetResistanceRow[names.Length];
        for (int index = 0; index < rows.Length; index++)
        {
            string value = index == 0 ? "-100%" : "??";
            BattleResistanceBadge[] badges = Enumerable.Range(272, 12).Select(icon => new BattleResistanceBadge(new UiImageSource(UiImageKind.Icon, icon), "", value, BattleResistanceTone.Normal)).ToArray();
            rows[index] = new() { Badges = badges, Grid = BattleResistanceGrid.Measure(canvas, 1, 1, badges) };
        }
        BattleHudTargetList list = viewport.MeasureTargetList(canvas, new(), names, 1, 90, maximumWidth, 0, 0, rows);
        Assert.Equal(rows[0].BadgeX, rows[1].BadgeX);
        Assert.Equal(rows[0].Grid, rows[1].Grid);
        Assert.InRange(rows[0].Grid.Height, 1, 25.01f);
        for (int index = 0; index < rows.Length; index++)
        {
            UiRect cell = list.Cell(index);
            BattleTargetResistanceRow row = rows[index];
            BattleHudElement.DrawResistanceBadges(canvas, new(), 1, row.Grid, row.Badges!, new(cell.X + 30 + row.BadgeX, cell.Y, row.Grid.Width, row.Grid.Height));
        }
        Assert.Equal(24, canvas.Images.Count);
        for (int icon = 0; icon < 12; icon++)
        {
            var known = canvas.Images[icon];
            var unknown = canvas.Images[icon + 12];
            Assert.Equal(known.Source, unknown.Source);
            Assert.Equal(known.Bounds.X, unknown.Bounds.X);
            Assert.InRange(Math.Abs(known.Bounds.Y + list.RowHeight - unknown.Bounds.Y), 0, 0.001f);
            Assert.Equal("-100%", canvas.TextDraws[icon].Text);
            Assert.Equal("??", canvas.TextDraws[icon + 12].Text);
            Assert.Equal(known.Bounds.Bottom, canvas.TextDraws[icon].Bounds.Y);
            Assert.Equal(unknown.Bounds.Bottom, canvas.TextDraws[icon + 12].Bounds.Y);
        }
    }

    [Fact]
    public void CoincidentModelAnchorsKeepVisibleBadgesSeparateAtTheViewportEdge()
    {
        UiRect viewport = new(16, 12, 640, 480);
        UiRect model = new(12, 5, 130, 120);
        List<UiRect> placed = [];
        for (int index = 0; index < 3; index++)
        {
            UiRect panel = BattleHudElement.PlaceModelHint(viewport, model, new(105, 44), 4, placed);
            Assert.Equal(panel, panel.Intersect(viewport));
            Assert.True(panel.Intersect(model).IsEmpty);
            Assert.All(placed, other => Assert.True(panel.Intersect(other).IsEmpty));
            placed.Add(panel);
        }
    }
}
