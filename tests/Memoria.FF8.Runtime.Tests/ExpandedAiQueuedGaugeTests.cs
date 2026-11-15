using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedAiQueuedGaugeTests
{
    [Theory]
    [InlineData(3, BattleSession.AtbMaximum)]
    [InlineData(1, BattleSession.AtbMaximum)]
    [InlineData(1, 127)]
    [InlineData(4, BattleSession.AtbMaximum)]
    public void AuthoredAiRetainsItsGaugeInTheQueueAndWindup(byte stage, int gauge)
    {
        BattleSession live = StartAi(stage);
        BattleSnapshot queued = live.Capture();
        BattleAttackCommand command = Assert.Single(queued.QueuedActions);
        Assert.Equal(stage, command.NativeAiCallbackStage);
        Assert.Equal(stage == 3 ? "enemy" : null, command.TargetRequest.DeathCallbackSelf?.ActorId);
        Assert.Equal(BattleSession.AtbMaximum, Enemy(queued).Atb);
        Assert.Null(Enemy(queued).NativeAtb);
        if (gauge != BattleSession.AtbMaximum)
        {
            queued = WithGauge(queued, "enemy", gauge);
            live = BattleSession.Restore(queued);
        }

        BattleSession restored = RestoreAndVerify(queued);
        Assert.Equal(gauge, Enemy(restored.Capture()).Atb);
        live.Tick();
        restored.Tick();
        BattleSnapshot windup = live.Capture();
        Assert.Equal(BattleActionPhase.Windup, windup.ActiveAction?.Phase);
        Assert.Equal(stage, windup.ActiveAction?.NativeAiCallbackStage);
        Assert.Equal(gauge, Enemy(windup).Atb);
        Assert.Equal(JsonSerializer.Serialize(windup), JsonSerializer.Serialize(restored.Capture()));

        restored = RestoreAndVerify(windup);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames && live.ActiveAction is not null; frame++)
        {
            live.Tick();
            restored.Tick();
        }
        Assert.Null(live.ActiveAction);
        Assert.Single(live.Events, item => item.Kind == "ActionComplete" && item.ActorId == "enemy");
        Assert.Equal(JsonSerializer.Serialize(live.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void GaugedEnemyCommandStillRequiresAValidOwningProgramAndStage()
    {
        BattleSnapshot queued = StartAi(1).Capture();
        BattleAttackCommand command = Assert.Single(queued.QueuedActions);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(queued with { QueuedActions = [command with { NativeAiCallbackStage = 5 }] }));
        BattleCombatantDefinition[] definitions = queued.Definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeAi = null }).ToArray();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(queued with { Definition = queued.Definition with { Combatants = definitions } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(WithGauge(queued, "enemy", BattleSession.AtbMaximum + 1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryAndCompelledPlayerCommandsDoNotOwnANonzeroGauge(bool compelled)
    {
        BattleSession battle = Create(compelled: compelled);
        if (compelled)
        {
            battle.Tick();
            Assert.Equal(BattleTargetEligibility.CompelledAction, battle.ActiveAction?.TargetRequest.Eligibility);
        }
        else
        {
            battle.QueueAttack("party", "enemy");
            Assert.Single(battle.QueuedActions);
        }
        BattleSnapshot saved = battle.Capture();
        RestoreAndVerify(saved);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(WithGauge(saved, "party", 127)));
    }

    private static BattleSession StartAi(byte stage)
    {
        BattleSession battle = Create(ai: true);
        BattleCombatant enemy = Assert.Single(battle.Combatants, actor => !actor.IsPlayer);
        if (stage == 3)
        {
            enemy.CurrentHp = 0;
            enemy.NativeStatus0 |= 1;
            Invoke("RunExpandedEnemyDeathAi", enemy);
        }
        else
        {
            Invoke("RunExpandedNativeAi", enemy, (int)stage);
        }
        return battle;

        void Invoke(string method, params object[] arguments)
        {
            typeof(BattleSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(battle, arguments);
        }
    }

    private static BattleSession Create(bool ai = false, bool compelled = false)
    {
        BattleNativeAction zeroContact = new(8, 1, 3, 0)
        {
            Name = "Zero contact", MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3,
            DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleNativeAction attack = zeroContact with { Command = 1, ActionId = 0, AttackType = 1, Hit = 255, TargetFlags = 0x50 };
        byte[] queueAndRefill = [4, 0, 12, 0, 36, 0];
        NativeBattleAiDefinition program = new([[0], queueAndRefill, [0], [22, 4, 200, 12, 0, 36, 0], queueAndRefill],
            Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
        return new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            NativeFieldBattleRules = 0,
            NativeCommandSequences = ImmutableArray.CreateRange(new byte[39]),
            NativeActions = [zeroContact],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack], NativeAttack = attack, NativeCommandSlots = [1, 1, 1, 1], NativeStatus0 = compelled ? (ushort)0x20 : (ushort)0 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 68, NativeAi = ai ? program : null, NativeCombatantFlags = 0x20 }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleSnapshot WithGauge(BattleSnapshot snapshot, string id, int gauge)
    {
        return snapshot with { Combatants = snapshot.Combatants.Select(actor => actor.Id == id ? actor with { Atb = gauge } : actor).ToArray() };
    }

    private static BattleCombatantSnapshot Enemy(BattleSnapshot snapshot) => Assert.Single(snapshot.Combatants, actor => actor.Id == "enemy");

    private static BattleSession RestoreAndVerify(BattleSnapshot snapshot)
    {
        string json = JsonSerializer.Serialize(snapshot);
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        return restored;
    }
}
