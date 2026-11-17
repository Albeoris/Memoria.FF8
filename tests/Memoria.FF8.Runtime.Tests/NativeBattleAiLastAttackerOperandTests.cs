namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiLastAttackerOperandTests
{
    [Theory]
    [InlineData(14, 0, 0u)]
    [InlineData(14, 1, 1u)]
    [InlineData(14, 6, 6u)]
    [InlineData(18, 0, 255u)]
    [InlineData(18, 1, 256u)]
    [InlineData(18, 6, 261u)]
    public void Numeric_operand203_preserves_the_actual_original_attacker_byte(byte opcode, byte original, uint expected)
    {
        var (battle, enemy) = Create(opcode);
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.NativeActorIndex == original);
        enemy.LastNativeAction = new(null, null, null, null, null, original);
        enemy.LastAttackerId = attacker.Id;

        DetachedExpandedAiFixture.InterpretScalarCallback(battle, enemy.Id, 0);

        NativeBattleParticipantSnapshot saved = battle.Capture().Combatants.Single(actor => actor.Id == enemy.Id).NativeParticipant!;
        if (opcode == 14)
        {
            Assert.Equal(new NativeBattleAiActorValue(attacker.Id, original), saved.ActorReferences![0]);
            Assert.Equal(0u, saved.Variables[0]);
        }
        else
        {
            Assert.Equal(expected, saved.Variables[0]);
            Assert.Null(saved.ActorReferences);
        }
        Assert.Equal(original, saved.LastAction!.AttackerIndex);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(18)]
    public void An_unaliased_attacker_can_supply_identity_without_a_numeric_original_operand(byte opcode)
    {
        var (battle, enemy) = Create(opcode);
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == "party-3");
        Assert.Null(attacker.NativeActorIndex);
        enemy.LastAttackerId = attacker.Id;
        enemy.LastNativeAction = new(null, null, null, null, null, null);

        if (opcode == 14)
        {
            DetachedExpandedAiFixture.InterpretScalarCallback(battle, enemy.Id, 0);
            Assert.Equal(new NativeBattleAiActorValue(attacker.Id, null), enemy.AiActorReferences[0]);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => DetachedExpandedAiFixture.InterpretScalarCallback(battle, enemy.Id, 0));
            Assert.Equal(255u, enemy.AiVariables[0]);
            Assert.Empty(enemy.AiActorReferences);
        }
        Assert.Null(enemy.LastNativeAction.AttackerIndex);
    }

    [Fact]
    public void Numeric_read_after_restore_uses_the_captured_byte_then_replaces_the_actor_value()
    {
        var (battle, enemy) = Create(14, turn: [18, 220, 1, 0]);
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == "party-1");
        enemy.LastAttackerId = attacker.Id;
        enemy.LastNativeAction = new(null, null, null, null, null, 1);
        DetachedExpandedAiFixture.InterpretScalarCallback(battle, enemy.Id, 0);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        BattleCombatant continued = Assert.Single(restored.Combatants, actor => actor.Id == enemy.Id);
        Assert.Equal(new NativeBattleAiActorValue(attacker.Id, 1), continued.AiActorReferences[0]);

        DetachedExpandedAiFixture.InterpretScalarCallback(restored, continued.Id, 1);

        Assert.Equal(2u, continued.AiVariables[0]);
        Assert.Empty(continued.AiActorReferences);
    }

    private static (BattleSession Battle, BattleCombatant Enemy) Create(byte opcode, byte[]? turn = null)
    {
        byte[] script = opcode == 14 ? [14, 220, 203, 0] : [14, 220, 255, 18, 220, 203, 0];
        NativeBattleAiDefinition ai = new([script, turn ?? [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                .. Enumerable.Range(0, 4).Select(row => new BattleCombatantDefinition { Id = $"party-{row}", Name = $"Party {row}", IsPlayer = true }),
                .. Enumerable.Range(0, 4).Select(row => new BattleCombatantDefinition { Id = $"enemy-{row}", Name = $"Enemy {row}", ModelId = row + 1, NativeAi = row == 0 ? ai : null })
            ]
        }, BattleAtbMode.Normal);
        for (int row = 0; row < 4; row++)
        {
            battle.Combatants[row].BindRosterSlot(BattleRosterSlot.Ally(row));
            battle.Combatants[4 + row].BindRosterSlot(BattleRosterSlot.Enemy(row));
        }
        return (battle, battle.Combatants[4]);
    }
}
