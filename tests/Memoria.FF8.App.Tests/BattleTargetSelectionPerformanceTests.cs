using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTargetSelectionPerformanceTests
{
    [Fact]
    public void Attack_target_refresh_cost_is_independent_of_the_magic_catalog()
    {
        BattleHudController small = CreateHud(0);
        BattleHudController full = CreateHud(512);
        Refresh(small);
        Refresh(full);
        long smallBytes = AllocatedForRefreshes(small);
        long fullBytes = AllocatedForRefreshes(full);
        Console.WriteLine($"Attack targets, 20 refreshes: empty catalog {smallBytes:N0} bytes; full catalog {fullBytes:N0} bytes.");
        Assert.True(fullBytes <= smallBytes + 262144, $"Attack target selection allocated {fullBytes:N0} bytes versus {smallBytes:N0} bytes with an empty magic catalog.");
        Assert.Equal("grat-0", full.SelectedTarget!.Id);
        full.Navigate(MenuInput.Down);
        Assert.Equal("grat-1", full.SelectedTarget!.Id);
        full.Navigate(MenuInput.Cancel);
        Assert.False(full.SelectingTarget);
    }

    private static BattleHudController CreateHud(int magicCount)
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "squall", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "grat-0", Name = "Grat" },
                new() { Id = "grat-1", Name = "Grat" }
            ],
            ActionDefinitions = Enumerable.Range(1, magicCount).Select(id => new BattleActionDefinition(BattleAbility.Magic, id, $"Magic {id}", BattleActionEffect.Damage, 1, false, Statuses: [BattleStatusKind.Sleep])).ToArray()
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        return hud;
    }

    private static long AllocatedForRefreshes(BattleHudController hud)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 20; index++)
        {
            Refresh(hud);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void Refresh(BattleHudController hud)
    {
        hud.BuildView();
        Assert.Equal(2, hud.Targets.Count);
        Assert.Equal(2, hud.VisibleTargets.Count);
        Assert.NotNull(hud.SelectedTarget);
    }
}
