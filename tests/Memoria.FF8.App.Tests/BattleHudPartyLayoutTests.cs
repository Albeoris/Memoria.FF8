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
    [InlineData(5, 640, 448, 1f)]
    [InlineData(5, 1280, 720, 1f)]
    [InlineData(6, 640, 448, 1f)]
    [InlineData(6, 1280, 720, 1f)]
    [InlineData(11, 640, 448, 1f)]
    [InlineData(11, 1280, 720, 1f)]
    [InlineData(5, 640, 448, 1.65f)]
    [InlineData(5, 1280, 720, 1.65f)]
    [InlineData(6, 640, 448, 1.65f)]
    [InlineData(6, 1280, 720, 1.65f)]
    [InlineData(11, 640, 448, 1.65f)]
    [InlineData(11, 1280, 720, 1.65f)]
    public void ExpandedPartyRowsFitAndCanBeReachedAtEachScale(int count, int width, int height, float textScale)
    {
        BattleHudController hud = Create(count);
        hud.TextScale = textScale;
        UiView view = hud.BuildView();
        view.Resize(width, height);
        UiTheme theme = new();
        BattleUiTests.TestCanvas canvas = new();
        BattleHudLayout viewport = new(new(0, 0, width, height));
        BattleHudPartyList layout = BattleHudPartyList.Measure(canvas, viewport, count, textScale, 0, 0);
        view.Draw(canvas, theme);

        Assert.Equal(textScale == 1f, layout.TwoColumns);
        Assert.Equal(textScale > 1f, layout.Scrolls);
        Assert.True(layout.Panel.Y >= 420);
        Assert.Equal(660, layout.Panel.Bottom, 3);
        Assert.DoesNotContain(canvas.TextDraws, draw => draw.Text == $"Ally {layout.End + 1}");
        for (int index = layout.Start; index < layout.End; index++)
        {
            UiRect row = layout.Row(index);
            UiRect screen = Scale(row);
            Assert.True(row.X >= 244 && row.Right <= 934 && row.Y >= 420 && row.Bottom <= 660);
            for (int prior = layout.Start; prior < index; prior++)
            {
                UiRect overlap = layout.Row(prior).Intersect(row);
                Assert.True(overlap.IsEmpty || overlap.Height < 0.01f);
            }
            var name = Assert.Single(canvas.TextDraws, draw => draw.Text == $"Ally {index + 1}");
            Assert.Equal(screen.Y, name.Bounds.Y);
            Assert.InRange(name.Bounds.X, screen.X, screen.Right);
            var hp = canvas.TextDraws.Where(draw => draw.Text == "99999" && draw.Bounds.Y == name.Bounds.Y && screen.Contains(draw.Bounds.X, draw.Bounds.Y)).OrderBy(draw => draw.Bounds.X).ToArray();
            Assert.Equal(2, hp.Length);
            Assert.All(hp, number => Assert.True(canvas.MeasureText(number.Text, layout.FontSize * viewport.Scale, float.PositiveInfinity).Width < number.Bounds.Width));
            Assert.True(name.Bounds.Right < hp[0].Bounds.X && hp[0].Bounds.Right < hp[1].Bounds.X && hp[1].Bounds.Right < screen.Right);
        }

        if (layout.Scrolls)
        {
            Point(Scale(layout.NextPage));
            Assert.Equal(hud.Party[0].Id, hud.SelectedActor?.Id);
            int start = Math.Min(count - layout.VisibleCount, layout.VisibleCount);
            BattleUiTests.TestCanvas paged = new();
            view.Draw(paged, theme);
            Assert.DoesNotContain("Ally 1", paged.Texts);
            Assert.Contains($"Ally {start + 1}", paged.Texts);
            Point(Scale(layout.PreviousPage));
            paged = new();
            view.Draw(paged, theme);
            Assert.Contains("Ally 1", paged.Texts);

            for (int index = 1; index < count; index++)
            {
                hud.CycleCommandActor();
                Assert.Equal(hud.Party[index].Id, hud.SelectedActor?.Id);
                paged = new();
                view.Draw(paged, theme);
                Assert.Contains($"Ally {index + 1}", paged.Texts);
            }
            Assert.DoesNotContain("Ally 1", paged.Texts);
            int first = count - layout.VisibleCount;
            Point(Scale(layout.Row(layout.Start)));
            Assert.Equal(hud.Party[first].Id, hud.SelectedActor?.Id);
            Click(view, viewport.Rect(600, 407, 0, 0).X, viewport.Rect(600, 407, 0, 0).Y);
            Assert.Equal(hud.Party[first].Id, hud.SelectedActor?.Id);
        }
        else
        {
            for (int index = 0; index < count; index++)
            {
                Point(Scale(layout.Row(index)));
                Assert.Equal(hud.Party[index].Id, hud.SelectedActor?.Id);
            }
        }

        UiRect Scale(UiRect rectangle) => viewport.Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        void Point(UiRect rectangle) => Click(view, rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
    }

    [Fact]
    public void HiddenPartyIgnoresClicksAtItsLastDrawnRow()
    {
        BattleHudController hud = Create(5);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        view.Draw(new BattleUiTests.TestCanvas(), new UiTheme());

        hud.SetNativeCommandButtons(0x100);
        Click(view, 640, 608);
        Assert.Equal(hud.Party[0].Id, hud.SelectedActor?.Id);

        hud.SetNativeCommandButtons(0);
        Click(view, 640, 608);
        Assert.Equal(hud.Party[^1].Id, hud.SelectedActor?.Id);
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
            UsesExpandedRoster = count >= 4,
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
