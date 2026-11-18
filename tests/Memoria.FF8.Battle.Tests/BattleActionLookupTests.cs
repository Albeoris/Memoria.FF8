using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleActionLookupTests
{
    [Fact]
    public void SelectedActionMatchesBothKeysAndRetainsDefensiveStatusOwnership()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Magic", BattleActionEffect.Damage, 10, false, Statuses: [BattleStatusKind.Poison]), new(BattleAbility.Item, 1, "Item", BattleActionEffect.Heal, 10, true)]
        }, BattleAtbMode.TurnBased);
        BattleActionDefinition selected = Assert.IsType<BattleActionDefinition>(session.GetActionDefinition(BattleAbility.Magic, 1));
        Assert.Equal("Magic", selected.Name);
        Assert.Equal("Item", session.GetActionDefinition(BattleAbility.Item, 1)!.Name);
        Assert.Null(session.GetActionDefinition(BattleAbility.Magic, 2));
        selected.Statuses![0] = BattleStatusKind.Haste;
        Assert.Equal(BattleStatusKind.Poison, session.GetActionDefinition(BattleAbility.Magic, 1)!.Statuses![0]);
        Assert.Equal(BattleStatusKind.Poison, session.ActionDefinitions[0].Statuses![0]);
    }

    [Fact]
    public void SelectingOneActionDoesNotCloneTheWholeCatalog()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            ActionDefinitions = Enumerable.Range(0, 512).Select(id => new BattleActionDefinition(BattleAbility.Magic, id, "Magic", BattleActionEffect.Damage, 10, false)).ToArray()
        }, BattleAtbMode.TurnBased);
        _ = session.GetActionDefinition(BattleAbility.Magic, 511);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 100; index++)
        {
            _ = session.GetActionDefinition(BattleAbility.Magic, 511);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 64 * 1024, $"Single-action lookup allocated {allocated} bytes.");
    }
}
