using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudPartyLayoutTests
{
    [Theory]
    [InlineData(1, 960, 672, 1f)]
    [InlineData(2, 960, 672, 1f)]
    [InlineData(3, 960, 672, 1f)]
    [InlineData(4, 960, 672, 1.5f)]
    public void PartyRowsDrawAndSelectTheSameActorAtScaledBounds(int count, int width, int height, float textScale)
    {
        BattleHudController hud = Create(count);
        hud.TextScale = textScale;
        UiView view = hud.BuildView();
        view.Resize(width, height);
        UiTheme theme = new();
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, theme);

        BattleHudLayout layout = new(new(0, 0, width, height));
        UiRect[] rows = new UiRect[count];
        for (int index = 0; index < count; index++)
        {
            BattleHudPartyRow row = BattleHudLayout.PartyRow(index, count, textScale);
            rows[index] = layout.PartyRowBounds(row);
            Assert.True(rows[index].Y >= layout.MessagePanel.Bottom);
            Assert.True(rows[index].Bottom <= layout.Rect(0, 661, 0, 0).Y);
            var name = Assert.Single(canvas.TextDraws, draw => draw.Text == $"Ally {index + 1}");
            Assert.Equal(layout.Rect(520, row.Top, 150, row.VitalHeight), name.Bounds);
            Assert.Contains(canvas.Fills, fill => fill.Bounds == layout.AtbGauge(row.Top, row.GaugeOffset));
            if (count <= 3)
            {
                Assert.Equal(layout.Rect(496, 528 + 38 * index, 438, 38), rows[index]);
            }
            else if (count == 4)
            {
                Assert.Equal(layout.Rect(496, 528 + 28.5f * index, 438, 28.5f), rows[index]);
            }
        }

        for (int index = 0; index < count; index++)
        {
            if (index > 0)
            {
                hud.CycleCommandActor();
                Assert.Equal(hud.Party[index].Id, hud.SelectedActor?.Id);
                hud.SelectParty(hud.Party[0].Id);
            }
            Click(view, rows[index].X + 5, rows[index].Bottom - 2);
            Assert.Equal(hud.Party[index].Id, hud.SelectedActor?.Id);
        }

        Click(view, rows[0].X - 2, rows[0].Y + 5);
        Click(view, rows[0].X + 5, rows[0].Y - 2);
        Click(view, rows[^1].Right + 2, rows[^1].Y + 5);
        Click(view, rows[^1].X + 5, rows[^1].Bottom + 2);
        Assert.Equal(hud.Party[^1].Id, hud.SelectedActor?.Id);
    }

    [Theory]
    [InlineData(5, 640, 480, 1f)]
    [InlineData(6, 1280, 720, 1f)]
    [InlineData(7, 640, 480, 1f)]
    [InlineData(11, 1280, 720, 1f)]
    [InlineData(6, 640, 480, 1.65f)]
    [InlineData(11, 1280, 720, 1.65f)]
    [InlineData(11, 640, 480, 3f)]
    public void ExpandedPartyRowsFitAndCanBeReachedAtEachScale(int count, int width, int height, float textScale)
    {
        BattleHudController hud = Create(count);
        hud.TextScale = textScale;
        UiView view = hud.BuildView();
        view.Resize(width, height);
        UiTheme theme = new();
        BattleUiTests.TestCanvas canvas = new();
        BattleHudLayout viewport = new(new(0, 0, width, height));
        BattleHudPartyContent[] content = hud.Party.Select(actor => new BattleHudPartyContent(actor.Name, "99999", "99999", 0)).ToArray();
        BattleHudPartyList layout = BattleHudPartyList.Measure(canvas, viewport, content, textScale, 0, 0);
        view.Draw(canvas, theme);

        Assert.Equal(count > 6, layout.TwoColumns);
        Assert.Equal(textScale > 1f, layout.Scrolls);
        UiRect panel = Scale(layout.Panel);
        Assert.DoesNotContain(canvas.Fills, fill => fill.Bounds == panel);
        Assert.Equal(width - 26 * viewport.Scale, panel.Right, 2);
        Assert.Equal(660, layout.Panel.Bottom, 3);
        UiRect command = Assert.Single(canvas.TextDraws, draw => draw.Text == "Attack").Bounds;
        Assert.True(command.Right < panel.X);
        int[] visible = layout.VisibleIndices().ToArray();
        foreach (int index in visible)
        {
            UiRect row = Scale(layout.Row(index));
            var name = Assert.Single(canvas.TextDraws, draw => draw.Text == $"Ally {index + 1}");
            Assert.Equal(row.Y, name.Bounds.Y);
            Assert.InRange(name.Bounds.Right, row.X, row.Right);
            float vitalY = row.Y + (layout.StackedVitals ? layout.VitalLineHeight * viewport.Scale : 0);
            var hp = canvas.TextDraws.Where(draw => draw.Text == "99999" && Math.Abs(draw.Bounds.Y - vitalY) < 0.01f && row.Contains(draw.Bounds.X, draw.Bounds.Y)).OrderBy(draw => draw.Bounds.X).ToArray();
            Assert.Equal(2, hp.Length);
            Assert.All(hp, number => Assert.True(canvas.MeasureText(number.Text, layout.FontSize * viewport.Scale, float.PositiveInfinity).Width < number.Bounds.Width));
            Assert.True(hp[0].Bounds.Right < hp[1].Bounds.X && hp[1].Bounds.Right < row.Right);
            Assert.Contains(canvas.Fills, fill => fill.Color == new UiColor(22, 22, 27) && row.Contains(fill.Bounds.X, fill.Bounds.Y));
        }
        if (count > 6)
        {
            Assert.Equal(layout.Row(0).Y, layout.Row(6).Y);
            Assert.True(layout.Row(0).Right < layout.Row(6).X);
        }

        if (layout.Scrolls)
        {
            Point(Scale(layout.NextPage));
            BattleUiTests.TestCanvas paged = new();
            view.Draw(paged, theme);
            Assert.DoesNotContain("Ally 1", paged.Texts);
            Assert.Equal(hud.Party[0].Id, hud.SelectedActor?.Id);
            Point(Scale(layout.PreviousPage));
            for (int index = 1; index < count; index++)
            {
                hud.CycleCommandActor();
                Assert.Equal(hud.Party[index].Id, hud.SelectedActor?.Id);
                paged = new();
                view.Draw(paged, theme);
                Assert.Contains($"Ally {index + 1}", paged.Texts);
            }
        }
        else
        {
            Assert.Equal(count, visible.Length);
            for (int index = 0; index < count; index++)
            {
                Point(Scale(layout.Row(index)));
                Assert.Equal(hud.Party[index].Id, hud.SelectedActor?.Id);
            }
        }

        UiRect Scale(UiRect rectangle) => viewport.Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        void Point(UiRect rectangle) => Click(view, rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
    }

    [Theory]
    [InlineData(960, 672)]
    [InlineData(1440, 720)]
    [InlineData(800, 600)]
    public void TwelveSyntheticRowsKeepStableColumnsWithMeasuredNamesAndStatusWidths(int width, int height)
    {
        BattleUiTests.TestCanvas canvas = new();
        BattleHudLayout viewport = new(new(0, 0, width, height));
        BattleHudPartyContent[] content = Enumerable.Range(1, 12).Select(index => new BattleHudPartyContent($"Ally {index} with a long localized name", "99999", "99999", 270)).ToArray();
        BattleHudPartyList layout = BattleHudPartyList.Measure(canvas, viewport, content, 1, 0, 11);
        Assert.Equal(Enumerable.Range(0, 12), layout.VisibleIndices());
        Assert.False(layout.Scrolls);
        Assert.Equal(layout.Row(0).Y, layout.Row(6).Y);
        Assert.Equal(layout.Row(5).Y, layout.Row(11).Y);
        Assert.True(layout.Row(5).Bottom < layout.Panel.Bottom);
        Assert.Equal(width - 26 * viewport.Scale, viewport.Rect(layout.Panel.X, 0, layout.Panel.Width, 0).Right, 2);
    }

    [Fact]
    public void HiddenPartyIgnoresClicksAtItsLastDrawnRow()
    {
        BattleHudController hud = Create(5);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        UiRect row = Assert.Single(canvas.TextDraws, draw => draw.Text == "Ally 5").Bounds;

        hud.SetNativeCommandButtons(0x100);
        Click(view, row.X + 2, row.Y + 2);
        Assert.Equal(hud.Party[0].Id, hud.SelectedActor?.Id);

        hud.SetNativeCommandButtons(0);
        Click(view, row.X + 2, row.Y + 2);
        Assert.Equal(hud.Party[^1].Id, hud.SelectedActor?.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExpandedPartyPointerRequiresRedrawAfterViewportOrTextScaleChanges(bool resize)
    {
        BattleHudController hud = Create(7);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        UiRect oldRow = DrawSecondMember();
        if (resize)
        {
            view.Resize(1280, 720);
            oldRow = new BattleHudLayout(new UiRect(0, 0, 1280, 720)).Rect(oldRow.X, oldRow.Y, oldRow.Width, oldRow.Height);
        }
        else
        {
            hud.TextScale = 1.65f;
        }
        Click(view, oldRow.X + 2, oldRow.Y + 2);
        Assert.Equal(hud.Party[0].Id, hud.SelectedActor?.Id);

        UiRect currentRow = DrawSecondMember();
        Click(view, currentRow.X + 2, currentRow.Y + 2);
        Assert.Equal(hud.Party[1].Id, hud.SelectedActor?.Id);

        UiRect DrawSecondMember()
        {
            BattleUiTests.TestCanvas canvas = new();
            view.Draw(canvas, new UiTheme());
            return Assert.Single(canvas.TextDraws, draw => draw.Text == "Ally 2").Bounds;
        }
    }

    [Fact]
    public void MagicOverlayKeepsPointerOwnershipOverTheParty()
    {
        BattleHudController hud = Create(4, magic: true);
        hud.Choose(0);
        Assert.True(hud.ShowingMagicList);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        BattleHudLayout layout = new(new(0, 0, 960, 672));
        UiRect secondPartyRow = layout.PartyRowBounds(BattleHudLayout.PartyRow(1, 4, 1));
        float clickY = secondPartyRow.Y + 12;
        var spell = Assert.Single(canvas.TextDraws, draw => draw.Text.StartsWith("Spell ", StringComparison.Ordinal) && draw.Bounds.X > secondPartyRow.X && draw.Bounds.Contains(draw.Bounds.X + 1, clickY));
        Click(view, spell.Bounds.X + 1, clickY);

        Assert.Equal(hud.Party[0].Id, hud.SelectedActor?.Id);
        Assert.True(hud.SelectingTarget);
        Assert.Equal(spell.Text, hud.SelectedAction?.Name);
    }

    private static BattleHudController Create(int count, bool magic = false)
    {
        BattleActionDefinition[] spells = magic ? Enumerable.Range(1, 8).Select(Spell).ToArray() : [];
        BattleSession session = new(new BattleDefinition
        {
            Combatants =
            [
                .. Enumerable.Range(0, count).Select(index => new BattleCombatantDefinition
                {
                    Id = $"character-{2 * index + 1}",
                    Name = $"Ally {index + 1}",
                    IsPlayer = true,
                    MaximumHp = 99999,
                    CurrentHp = 99999,
                    InitialAtb = BattleSession.AtbMaximum,
                    Abilities = magic && index == 0 ? [BattleAbility.Magic] : [BattleAbility.Attack],
                    MagicStocks = magic && index == 0 ? spells.Select(spell => new BattleStock(spell.Id, 10)).ToArray() : []
                }),
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = spells
        }, BattleAtbMode.TurnBased);
        return new(session, (_, _) => { }, () => { }, () => { });
    }

    private static BattleActionDefinition Spell(int id)
    {
        string name = $"Spell {id} with a deliberately long localized display name";
        BattleNativeAction native = new(2, (ushort)id, 1, id)
        {
            Name = name,
            MagicId = (ushort)id,
            AttackType = 2,
            Power = 10,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        return new(BattleAbility.Magic, id, name, BattleActionEffect.Damage, 10, false) { NativeAction = native };
    }

    private static void Click(UiView view, float x, float y)
    {
        view.Pointer(x, y, false);
        view.Pointer(x, y, true);
    }
}
