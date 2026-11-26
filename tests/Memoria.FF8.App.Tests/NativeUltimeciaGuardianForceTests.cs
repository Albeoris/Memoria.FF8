using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeUltimeciaGuardianForceTests
{
    [Theory]
    [InlineData(124, 4, 23, TargetPhase.Transformed)]
    [InlineData(126, 5, 19, TargetPhase.Transformed)]
    [InlineData(124, 4, 23, TargetPhase.Charging)]
    [InlineData(126, 5, 19, TargetPhase.Charging)]
    [InlineData(124, 4, 23, TargetPhase.Human)]
    [InlineData(124, 4, 23, TargetPhase.Entering)]
    [InlineData(126, 5, 19, TargetPhase.KnockedOut)]
    [InlineData(126, 5, 19, TargetPhase.InvincibleForm)]
    public void OriginalTurnSelectsAndDefeatsOnlyAnExposedGuardian(int model, int slot, byte sequence, TargetPhase phase)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original Ultimecia AI and attack record.");
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(511);
        BattleEnemyPlacement phasePlacement = Assert.Single(formation.Slots, opponent => opponent.ModelId == model);
        Assert.False(phasePlacement.Enabled);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(model, 10);
        for (int band = 0; band < 3; band++)
        {
            NativeBattleAiAbility ability = ai.Abilities[band * 16 + slot];
            Assert.Equal((8, 335, (int)sequence), ((int)ability.Command, (int)ability.Id, (int)ability.Sequence));
        }
        BattleNativeAction antiGuardian = assets.NativeActions.Single(action => action is { Command: 8, ActionId: 335 });
        Assert.Equal((81, 27, 100, 0), ((int)antiGuardian.MagicId, (int)antiGuardian.AttackType, (int)antiGuardian.Power, (int)antiGuardian.Hit!.Value));

        BattleCombatantDefinition human = new() { Id = "human", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = 1000, CurrentHp = 719, NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition caster = human with
        {
            Id = "caster", Name = "Zell", ModelId = 1, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [2], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce],
            GuardianForceCompatibility = Enumerable.Repeat((ushort)350, 16).ToArray(), NativeStatus1 = phase == TargetPhase.InvincibleForm ? 1u << 11 : 0,
            CurrentHp = phase == TargetPhase.KnockedOut ? 0 : human.CurrentHp, NativeStatus0 = phase == TargetPhase.KnockedOut ? (ushort)1 : (ushort)0
        };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Ultimecia", ModelId = phasePlacement.ModelId, SceneSlot = 0, MaximumHp = 30000, CurrentHp = 30000, NativeStatusResistances = assets.ReadEnemyNativeStatusData(model).Resistances.ToArray(), NativeAi = ai };
        BattleActionDefinition summon = assets.ActionDefinitions.Single(action => action is { Ability: BattleAbility.GuardianForce, Id: 2 }) with { Level = 10, Available = true };
        BattleDefinition definition = new()
        {
            Combatants = [human, caster, enemy], ActionDefinitions = [summon], NativeActions = assets.NativeActions.ToArray(), EncounterId = 511,
            GuardianFormsEnabled = true, GuardianFormResources = [assets.ReadIfritGuardianFormResources()], GuardianForces = [new(2, 800, 1000) { DeathCount = 0 }],
            NativeBattleSpeed = 0, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Tuning = new() { MaximumDamage = phase == TargetPhase.Transformed ? 50 : 9999 }
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        if (phase is TargetPhase.Transformed or TargetPhase.InvincibleForm or TargetPhase.Entering)
        {
            bool landed = phase != TargetPhase.Entering;
            BattleGuardianFormState form = new("caster", 2, "ifrit-v1", landed ? BattleGuardianFormPhase.Active : BattleGuardianFormPhase.Entering, 1, landed ? 73 : 0) { EntryCompleted = landed };
            battle = BattleSession.Restore(battle.Capture() with { GuardianForms = new(true, 1, [form]) });
        }
        else if (phase == TargetPhase.Charging)
        {
            battle.QueueAction("caster", "enemy", BattleAbility.GuardianForce, 2);
            Assert.True(Assert.Single(battle.GuardianForceCasts).RemainingTicks > 0);
        }

        BattleCombatant source = battle.Combatants.Single(combatant => combatant.Id == "enemy");
        Invoke(battle, "InterpretNativeAi", source, 1);
        if (model == 126)
        {
            // The final form's first turn arms its anti-GF branch for the next turn.
            Assert.DoesNotContain(battle.QueuedActions, command => command.NativeQueuedCommand == 8 && command.ActionId == 335);
            Assert.Equal(1u, source.AiVariables[1]);
            Invoke(battle, "InterpretNativeAi", source, 1);
        }
        BattleAttackCommand[] strikes = battle.QueuedActions.Where(command => command.NativeQueuedCommand == 8 && command.ActionId == 335).ToArray();
        if (phase is not (TargetPhase.Transformed or TargetPhase.Charging))
        {
            Assert.Empty(strikes);
            Assert.Equal((ushort)800, Assert.Single(battle.GuardianForces).HitPoints);
            return;
        }

        BattleAttackCommand strike = Assert.Single(strikes);
        Assert.Equal("caster", strike.TargetId);
        Assert.Equal(sequence, strike.NativeSequence);
        BattleCombatant recipient = battle.Combatants.Single(combatant => combatant.Id == "caster");
        BattleActionPresentation presentation = new(strike.ActorId, strike.TargetId, BattleActionPhase.Windup, strike.TargetRequest, strike.Ability, strike.ActionId) { NativeAction = strike.NativeAction, NativeQueuedCommand = strike.NativeQueuedCommand, NativeSequence = strike.NativeSequence };
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, 335, "Destroy GF", BattleActionEffect.Native, antiGuardian.Power, false) { NativeAction = antiGuardian };
        NativeBattleContactPacket packet = Assert.IsType<NativeBattleContactPacket>(Invoke(battle, "ResolveInstalledNativeHitWithContact", source, recipient, presentation, action, antiGuardian, null, false));
        Assert.Equal(phase == TargetPhase.Transformed ? 50 : 9999, packet.Primary.PresentedAmount);
        BattleGuardianForceVital guardian = Assert.Single(battle.GuardianForces);
        Assert.Equal((ushort)0, guardian.HitPoints);
        Assert.Equal((ushort)1, guardian.DeathCount);
        Assert.Equal(719, recipient.CurrentHp);
        Assert.True(recipient.IsAlive);
        Assert.Null(battle.GetGuardianForm("caster"));
        Assert.Equal(719, battle.Combatants.Single(combatant => combatant.Id == "human").CurrentHp);
        if (phase == TargetPhase.Charging)
        {
            Invoke(battle, "DispatchGuardianForceCasts");
            Invoke(battle, "DispatchExpandedGuardianForceCharges");
            Assert.Empty(battle.GuardianForceCasts);
            Assert.Contains(battle.Events, entry => entry is { Kind: "GuardianForceCancelled", ActorId: "caster" });
        }
    }

    private static object? Invoke(BattleSession battle, string method, params object?[] arguments)
    {
        return typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
    }

    public enum TargetPhase
    {
        Transformed,
        Charging,
        Human,
        Entering,
        KnockedOut,
        InvincibleForm
    }
}
