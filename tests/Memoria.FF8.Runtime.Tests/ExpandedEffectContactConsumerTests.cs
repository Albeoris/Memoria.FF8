using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedEffectContactConsumerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void SingleTargetHitPassesReuseCanonicalOwnersAcrossTheFullRoster()
    {
        BattleSession battle = Create();
        BattleCombatant source = battle.Combatants[0];
        BattleCombatant[] targets = battle.Combatants.Skip(1).ToArray();
        Assert.Equal(new BattleModelOwner[] { Enemy(0), Enemy(1), Enemy(0), Enemy(2), Enemy(3), Enemy(4) }, ResolveOwners(battle, source, [targets[0], targets[1], targets[0], targets[2], targets[3], targets[4]]));
        Assert.Equal(Enumerable.Range(0, 6).Select(Enemy), ResolveOwners(battle, source, targets));
        Assert.Equal(new BattleModelOwner[] { new BattlePartyActorModel(new("caster")), Enemy(0), new BattlePartyActorModel(new("caster")), Enemy(0) }, ResolveOwners(battle, source, [source, targets[0], source, targets[0]]));
    }

    private static BattleModelOwner[] ResolveOwners(BattleSession battle, BattleCombatant source, BattleCombatant[] singleTargetPasses)
    {
        BattleNativeAction native = new(2, 1, 1, 1)
        {
            AttackType = 27, Power = 1, Hit = 0, HitCount = checked((byte)singleTargetPasses.Length), TargetFlags = 0x52, AttackFlags = 3,
            MagicId = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleActionPresentation command = Command() with { Ability = BattleAbility.Magic, ActionId = 1, NativeAction = native };
        BattleActionDefinition action = new(BattleAbility.Magic, 1, "Contact test", BattleActionEffect.Native, 1, false) { NativeAction = native };
        var plan = singleTargetPasses.Select(recipient => (new[] { recipient }, (BattleModelOwner?)null)).ToArray();
        typeof(BattleSession).GetField("nativeActionScratch", PrivateInstance)!.SetValue(battle, new NativeBattleActionScratch { ActorIndex = 0, Command = 2, Action = 1, EventActorB0F = 0, EventLane = 2, DeferredEffectsE00 = false });
        MethodInfo execute = typeof(BattleSession).GetMethod("ExecuteHostedContactPlan", PrivateInstance)!;
        object produced = execute.Invoke(battle, [source, command, action, native, plan, false, null])!;
        var contacts = Assert.IsType<ImmutableArray<NativeBattleContactPacket>>(produced.GetType().GetProperty("Contacts")!.GetValue(produced));
        return contacts.Select(contact => contact.Primary.Recipient).ToArray();
    }

    private static BattleSession Create()
    {
        return new(new()
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, MaximumHp = 2000, CurrentHp = 2000 },
                .. Enumerable.Range(0, 6).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = $"Enemy {index}", MaximumHp = 2000, CurrentHp = 2000 })
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleActionPresentation Command()
    {
        BattleTargetRequest request = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy-0")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
        return new("caster", "enemy-0", BattleActionPhase.Recovery, request);
    }

    private static BattleEnemyActorModel Enemy(int index)
    {
        return new(new($"enemy-{index}"));
    }

}
