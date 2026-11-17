using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayCombatTuningTests
{
    [Fact]
    public void RareItemDefaultMatchesEveryNativeRandomSampleAndOrdinaryDropsIgnoreOverrides()
    {
        RareItemProbabilities fourthOnly = new() { First = 0, Second = 0, Third = 0, Fourth = 100 };
        foreach (byte sample in Enumerable.Range(0, 256).Select(value => (byte)value))
        {
            sbyte original = sample < 128 ? (sbyte)0 : sample < 242 ? (sbyte)1 : (sbyte)2;
            Assert.Equal(original, BattleEnemyRewardDefinition.SelectDropSlot(sample, true));
            Assert.Equal((sbyte)3, BattleEnemyRewardDefinition.SelectDropSlot(sample, true, fourthOnly));
            Assert.Equal(BattleEnemyRewardDefinition.SelectDropSlot(sample, false), BattleEnemyRewardDefinition.SelectDropSlot(sample, false, fourthOnly));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9999)]
    [InlineData(100000)]
    public void PositiveDamageUsesConfiguredCapAfterPrivateRestoreAndHealingIsIndependent(int cap)
    {
        BattleSession battle = Create(cap);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        BattleCombatant target = restored.Combatants[1];
        typeof(BattleSession).GetMethod("ApplyDamage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(restored, [restored.Combatants[0], target, 120000, "Tuned attack", true, true, false, EncounterHpCause.Damage]);
        Assert.Equal(200000 - cap, target.CurrentHp);
        Assert.Equal(cap, Assert.Single(restored.Events, entry => entry.Kind == "Damage").Amount);
        typeof(BattleSession).GetMethod("ApplyHealing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(restored, [target, 100, "Recovery"]);
        Assert.Equal(Math.Min(200000, 200000 - cap + 100), target.CurrentHp);
    }

    [Theory]
    [InlineData(0, (byte)0, 0)]
    [InlineData(9999, (byte)8, 60000)]
    [InlineData(60000, (byte)0, 60000)]
    [InlineData(100000, (byte)0, 100000)]
    public void NativeFixedDamageOverridesOldCapAndRetainsItsExactMagnitudeThroughStorageAndText(int cap, byte flags, int expected)
    {
        BattleSession battle = Create(cap);
        BattleCombatant actor = battle.Combatants[0];
        BattleCombatant target = battle.Combatants[1];
        BattleNativeAction native = new(0, 1, -1, -1) { Name = "Fixed damage", MagicId = 0, AttackType = 28, Power = 0, HitCount = 1, AttackFlags = flags, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = 0, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
        BattleActionPresentation command = new(actor.Id, target.Id, BattleActionPhase.Windup, new BattleTargetRequest(new BattleTargetSelection.Explicit([target.IsPlayer ? new BattlePartyActorReference(target.Id) : new BattleEnemyActorReference(target.Id)]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), BattleAbility.EnemyAbility, 1) { NativeAction = native, NativeQueuedCommand = 0 };
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, 1, "Fixed damage", BattleActionEffect.Native, 0, false) { NativeAction = native };
        NativeBattleContactPacket packet = (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [actor, target, command, action, native, null, false])!;
        Assert.Equal(200000 - expected, target.CurrentHp);
        Assert.Equal(expected, packet.Primary.PresentedAmount);
        (NativeBattleActionPacketsState bank, BattleContactOccurrence occurrence) = battle.NativeActionPackets.AppendContact(packet);
        NativeBattleRawContactPacket raw = Assert.IsType<NativeBattleRawContactPacket>(bank.ExportRetained().Contacts[0]);
        Assert.Equal(unchecked((ushort)expected), NativeBattleContactPacketCodec.Decode(NativeBattleContactPacketCodec.Encode(raw)).Primary.Amount6);
        NativeBattleContactPacket stored = bank.ReadContact(occurrence);
        Assert.Equal(occurrence, stored.Occurrence);
        NativeBattleContactHalf restored = stored.Primary;
        Assert.Equal(new BattleEnemyActorModel(new(target.Id)), restored.Recipient);
        Assert.Equal(expected, restored.PresentedAmount);
        NativeBattleEffectsState effects = OriginalEffects().PrependDamageText(restored.Recipient, restored.ContactFlags, restored.PresentedAmount, bank.Roster);
        BattleActorModels models = new([new(restored.Recipient, new NativeBattleGraphicsModel(0))]);
        effects = effects.Advance(models, _ => new(160, 100)).State;
        Assert.Equal(expected.ToString().Select(digit => (byte)(56 + digit - '0')).Append((byte)0), Assert.Single(effects.TextDraws).Glyphs);
        if (expected > short.MaxValue)
        {
            NativeBattlePartyPresentedVital hp = new(9999, 9999, null, null);
            Assert.Equal((ushort)0, hp.Apply(restored).HitPoints);
            NativeBattlePartyPresentedVital gf = new(9999, 9999, 9999, 9999);
            NativeBattlePartyPresentedVital gfHit = gf.Apply(restored with { HitFlags = 0x20 });
            Assert.Equal((ushort)9999, gfHit.HitPoints);
            Assert.Equal((ushort)0, gfHit.GuardianForceHitPoints);
            Assert.Equal((ushort)0, gf.Apply(restored with { HitFlags = 0x20, SavedStatuses4 = 1 }).HitPoints);
        }
    }

    [Fact]
    public void OldDefinitionWithoutTuningDeserializesNativeDefaults()
    {
        BattleDefinition definition = JsonSerializer.Deserialize<BattleDefinition>("{}")!;
        Assert.Equal(new GameplayTuning(), definition.Tuning);
    }

    private static BattleSession Create(int cap)
    {
        return new(new BattleDefinition
        {
            Tuning = new() { MaximumDamage = cap },
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 200000, CurrentHp = 200000 }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
