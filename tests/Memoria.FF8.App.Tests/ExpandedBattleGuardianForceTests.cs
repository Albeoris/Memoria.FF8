using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleGuardianForceTests
{
    [Fact]
    public void EveryAdmittedOriginalGuardianActionPublishesOneContactPerLogicalTarget()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition[] catalog = assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce).ToArray();
        Assert.Equal(16, catalog.Length);
        foreach (BattleActionDefinition action in catalog)
        {
            Assert.NotNull(RunGuardianForce(assets, action));
        }
    }

    [Fact]
    public void IncomingDamageUsesTheChargingGuardiansHpAndCancelsItsCast()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 1);
        Assert.Equal(0, RunGuardianForce(assets, action, shieldInterruption: true));
    }

    internal static int? RunGuardianForce(BattleAssets assets, BattleActionDefinition action, bool shieldInterruption = false)
    {
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(action.NativeAction);
        ushort guardianHp = shieldInterruption ? (ushort)1 : (ushort)300;
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(row => new BattleCombatantDefinition
        {
            Id = $"party-{row}",
            Name = $"Ally {row}",
            IsPlayer = true,
            ModelId = row,
            CurrentHp = 4000,
            MaximumHp = 8000,
            NativeDeathCount = 0,
            NativeStatusResistances = new byte[40],
            Speed = 1
        }).ToArray();
        party[^1] = party[^1] with
        {
            Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce],
            EquipmentIds = [action.Id],
            GuardianForceCompatibility = Enumerable.Repeat((ushort)1000, 16).ToArray(),
            InitialAtb = BattleSession.AtbMaximum
        };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(row => new BattleCombatantDefinition
        {
            Id = $"enemy-{row}",
            Name = $"Enemy {row}",
            ModelId = 2,
            CurrentHp = 4000,
            MaximumHp = 8000,
            NativeStatusResistances = new byte[40],
            Speed = 1
        }).ToArray();
        if (shieldInterruption)
        {
            party[0] = party[0] with { InitialAtb = BattleSession.AtbMaximum };
        }
        BattleSession battle = new(new()
        {
            Combatants = [.. party, .. enemies],
            ActionDefinitions = [action],
            NativeActions = [native],
            GuardianForces = [new(action.Id, guardianHp, 300) { KillCount = 0, DeathCount = 0 }],
            NativeBattleSpeed = 0,
            NativeFieldBattleRules = 0,
            NativeStatusRules = assets.ReadNativeStatusRules(0)
        }, BattleAtbMode.Normal);
        if (!battle.SupportsExpandedAction(action))
        {
            return null;
        }
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == party[^1].Id);
        BattleCombatant anchor = battle.Combatants.First(target => BattleSession.IsValidSelectedActionTarget(caster, action, target));
        battle.QueueAction(caster.Id, anchor.Id, BattleAbility.GuardianForce, action.Id);
        if (shieldInterruption)
        {
            battle.QueueAttack(party[0].Id, caster.Id);
        }
        for (int tick = 0; tick < 240 && caster.IsBusy; tick++)
        {
            battle.Tick();
        }
        Assert.False(caster.IsBusy, action.Name);
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.GuardianForce).ToArray();
        if (shieldInterruption)
        {
            Assert.Empty(contacts);
            Assert.Equal(party[^1].CurrentHp, caster.CurrentHp);
            Assert.Single(battle.Events, entry => entry.Kind == "GuardianForceCancelled" && entry.ActorId == caster.Id);
            BattleGuardianForceVital guardian = Assert.Single(battle.GuardianForces);
            Assert.Equal((ushort)0, guardian.HitPoints);
            Assert.Equal((ushort)1, guardian.DeathCount);
            return 0;
        }
        Assert.NotEmpty(contacts);
        Assert.Equal(contacts.Length, contacts.Select(contact => contact.TargetId).Distinct().Count());
        if (action.AllTargets)
        {
            IEnumerable<string> expected = (action.Allies ? party : enemies).Select(actor => actor.Id);
            IEnumerable<string?> actual = contacts.Select(contact => contact.TargetId);
            Assert.Equal(expected, actual);
        }
        else
        {
            Assert.Single(contacts);
        }
        return contacts.Length;
    }

}
