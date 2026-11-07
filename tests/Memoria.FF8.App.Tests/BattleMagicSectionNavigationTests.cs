using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleMagicSectionNavigationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(25)]
    public void HorizontalArrowsMoveWithinColumnsAndSwitchSectionsOnlyAtTheirEdges(int count)
    {
        BattleHudController hud = CreateColumnSections(count);
        BattleDrawLayout layout = BattleDrawLayout.Calculate(count);
        for (int column = 0; column < layout.Columns; column++)
        {
            for (int row = 0; row < layout.ColumnRows(column); row++)
            {
                int index = layout.GetIndex(column, row);
                hud.HoverMagicChoice(index);
                hud.Navigate(MenuInput.Left);
                if (column == 0)
                {
                    Assert.Equal("Multi-target attack", hud.MagicSectionCaption);
                    hud.Navigate(MenuInput.PageRight);
                }
                else
                {
                    Assert.Equal("Single-target attack", hud.MagicSectionCaption);
                    Assert.Equal(layout.GetIndex(column - 1, Math.Min(row, layout.ColumnRows(column - 1) - 1)), hud.ChoiceIndex);
                }
                hud.HoverMagicChoice(index);
                hud.Navigate(MenuInput.Right);
                if (column == layout.Columns - 1)
                {
                    Assert.Equal("Recovery", hud.MagicSectionCaption);
                    hud.Navigate(MenuInput.PageLeft);
                }
                else
                {
                    Assert.Equal("Single-target attack", hud.MagicSectionCaption);
                    Assert.Equal(layout.GetIndex(column + 1, Math.Min(row, layout.ColumnRows(column + 1) - 1)), hud.ChoiceIndex);
                }
            }
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(25)]
    public void CharacterSwitchButtonsChangeSectionsFromEveryCell(int count)
    {
        BattleHudController hud = CreateColumnSections(count);
        for (int index = 0; index < count; index++)
        {
            hud.HoverMagicChoice(index);
            hud.Navigate(MenuInput.PageLeft);
            Assert.Equal("Multi-target attack", hud.MagicSectionCaption);
            hud.Navigate(MenuInput.PageRight);
            hud.HoverMagicChoice(index);
            hud.Navigate(MenuInput.PageRight);
            Assert.Equal("Recovery", hud.MagicSectionCaption);
            Assert.Equal(0, hud.ChoiceIndex);
            Assert.Equal(1, hud.MagicCastCount);
            hud.Navigate(MenuInput.PageRight);
            Assert.Equal("Recovery", hud.MagicSectionCaption);
            hud.Navigate(MenuInput.PageLeft);
        }
    }

    [Fact]
    public void VerticalArrowsWrapWithinTheCurrentColumn()
    {
        BattleHudController hud = CreateColumnSections(5);
        hud.HoverMagicChoice(3);
        hud.Navigate(MenuInput.Up);
        Assert.Equal(4, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(3, hud.ChoiceIndex);
        Assert.Equal("Single-target attack", hud.MagicSectionCaption);
    }

    private static BattleHudController CreateColumnSections(int count)
    {
        BattleHudController hud = Create([
            Spell(1, BattleActionEffect.Damage, false) with { AllTargets = true },
            .. Enumerable.Range(2, count).Select(id => Spell(id, BattleActionEffect.Damage, false)),
            Spell(count + 2, BattleActionEffect.Heal, true)
        ]);
        hud.Choose(0);
        hud.Navigate(MenuInput.PageRight);
        return hud;
    }

    [Theory]
    [InlineData(8, 4, 1, 0x50, 0, "Single-target attack")]
    [InlineData(2, 20, 10, 0x62, 0, "Multi-target attack")]
    [InlineData(3, 0, 1, 0x50, 0x172, "Debuffs")]
    [InlineData(3, 0, 1, 0x10, 0x40560D, "Recovery")]
    public void NativeSpecialDamageAndStatusRemovalUseTheirActionContracts(byte type, byte power, byte hits, byte targets, uint statuses, string caption)
    {
        BattleActionEffect effect = type == 2 ? BattleActionEffect.Damage : type == 3 ? BattleActionEffect.RemoveStatus : BattleActionEffect.Native;
        BattleNativeAction native = new(2, 1, 1, 1)
        {
            MagicId = 1,
            AttackType = type,
            Power = power,
            HitCount = hits,
            TargetFlags = targets,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = 0,
            StatusAttack = 255,
            Statuses0 = 0,
            Statuses1 = statuses,
            Hit = 255,
            Critical = 0,
            PowerModifier = 0,
            LevelModifier = 0,
            RawRecord = [],
            SupplementalRecord = []
        };
        BattleHudController hud = Create([Spell(1, effect, (targets & 0x40) == 0) with { NativeAction = native }]);
        hud.Choose(0);
        Assert.Equal(caption, hud.MagicSectionCaption);
    }

    [Fact]
    public void SectionsFollowRequestedOrderAndKeepAllSpellsReachable()
    {
        BattleActionDefinition[] actions =
        [
            Spell(6, BattleActionEffect.Scan, false),
            Spell(5, BattleActionEffect.Status, false),
            Spell(4, BattleActionEffect.Heal, true),
            Spell(3, BattleActionEffect.Status, true),
            Spell(2, BattleActionEffect.Damage, false),
            Spell(1, BattleActionEffect.Damage, false) with { AllTargets = true },
            Spell(7, BattleActionEffect.Damage, false)
        ];
        BattleHudController hud = Create(actions);
        hud.Choose(0);
        string[] captions = ["Multi-target attack", "Single-target attack", "Buffs", "Recovery", "Debuffs", "Other"];
        for (int section = 0; section < captions.Length; section++)
        {
            Assert.Equal(captions[section], hud.MagicSectionCaption);
            Assert.Equal(section > 0, hud.MagicPreviousSectionAvailable);
            Assert.Equal(section < captions.Length - 1, hud.MagicNextSectionAvailable);
            Assert.Equal(section + 1, hud.SelectedAction!.Id);
            if (section == 1)
            {
                hud.Navigate(MenuInput.Down);
                Assert.Equal(7, hud.SelectedAction!.Id);
                hud.Navigate(MenuInput.Down);
                Assert.Equal(2, hud.SelectedAction!.Id);
            }
            hud.Navigate(MenuInput.Right);
        }
        Assert.Equal("Other", hud.MagicSectionCaption);
        Assert.False(hud.SelectingTarget);
    }

    [Fact]
    public void RecoveryShortcutMovesThenConfirmsWithoutQueuingAndChoosesLargestDeficit()
    {
        BattleHudController hud = Create([Spell(1, BattleActionEffect.Damage, false), Spell(2, BattleActionEffect.Heal, true) with { Power = 40 }, Spell(3, BattleActionEffect.Heal, true) with { Power = 100 }], injured: true);
        hud.Choose(0);
        hud.Navigate(MenuInput.Alternate);
        Assert.True(hud.ShowingMagicList);
        Assert.Equal("Recovery", hud.MagicSectionCaption);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Alternate);
        Assert.True(hud.SelectingTarget);
        Assert.Equal("injured", hud.SelectedTarget!.Id);
        Assert.Equal(1, hud.MagicCastCount);
        Assert.Empty(hud.Session.QueuedActions);
        hud.Navigate(MenuInput.Cancel);
        Assert.True(hud.ShowingMagicList);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void ShortcutWithoutUsefulRecoveryLeavesSelectionAndSectionUntouched()
    {
        BattleHudController hud = Create([Spell(1, BattleActionEffect.Damage, false), Spell(2, BattleActionEffect.Heal, true)]);
        hud.Choose(0);
        hud.Navigate(MenuInput.Alternate);
        Assert.Equal(1, hud.SelectedAction!.Id);
        Assert.True(hud.ShowingMagicList);
        Assert.Empty(hud.Session.QueuedActions);
    }

    [Fact]
    public void CursorMemoryRestoresAcceptedSpellAcrossSectionBoundaries()
    {
        List<BattleInput> inputs = [];
        BattleHudController hud = Create([Spell(1, BattleActionEffect.Damage, false), Spell(2, BattleActionEffect.Heal, true)], injured: true, execute: inputs.Add);
        hud.CursorMemory = true;
        hud.Choose(0);
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        hud.SelectTarget("injured", true);
        Assert.Single(inputs);
        hud.Choose(0);
        Assert.True(hud.ShowingMagicList);
        Assert.Equal("Recovery", hud.MagicSectionCaption);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    private static BattleActionDefinition Spell(int id, BattleActionEffect effect, bool allies)
    {
        return new(BattleAbility.Magic, id, $"Spell {id}", effect, 20, allies);
    }

    private static BattleHudController Create(BattleActionDefinition[] actions, bool injured = false, Action<BattleInput>? execute = null)
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, MaximumHp = 1000, CurrentHp = 1000, Magic = 20, Abilities = [BattleAbility.Magic], MagicStocks = actions.Select(action => new BattleStock(action.Id, 10)).ToArray() },
                new() { Id = "injured", Name = "Ally", IsPlayer = true, MaximumHp = 1000, CurrentHp = injured ? 400 : 1000 },
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = actions
        }, BattleAtbMode.TurnBased);
        return new(session, (_, _) => { }, () => { }, () => { }, execute: execute);
    }
}
