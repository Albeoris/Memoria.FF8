using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianHeaderContactTests
{
    [Theory]
    [InlineData(0, 14, 75)]
    [InlineData(1, 23, 100)]
    [InlineData(1, 255, 250)]
    public void HeaderDrivenBoostUsesPhysicalActorTargetsSequenceAndFormulaWithoutAnActiveAction(byte actor, byte sequence, byte percent)
    {
        BattleSession battle = Create(actor, sequence);
        BattleSnapshot before = battle.Capture();
        Assert.Null(before.ActiveAction);
        NativeBattleRandomState expectedRandom = before.Random!;
        expectedRandom = expectedRandom.Draw(out byte first);
        expectedRandom = expectedRandom.Draw(out byte second);
        int firstDamage = NativeBattleDamage.GuardianForce(5, 44, 12, 60, 0, 20, first, percent);
        int secondDamage = NativeBattleDamage.GuardianForce(35, 44, 12, 60, 0, 20, second, percent);
        BattleSession restored = BattleSession.Restore(before);

        Recompute(battle, percent);
        Recompute(restored, percent);

        BattleSnapshot after = battle.Capture();
        Assert.Null(after.ActiveAction);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(after.Random));
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal((byte)2, battle.Presentation!.NativeActionPackets.ContactCount);
        Assert.Equal(new BattleEnemyActorModel(new("target-first")), battle.Presentation!.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary.Recipient);
        Assert.Equal(new BattleEnemyActorModel(new("target-second")), battle.Presentation!.NativeActionPackets.ReadContact(new BattleContactOccurrence(1)).Primary.Recipient);
        Assert.Equal((ushort)firstDamage, battle.Presentation!.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary.Amount6);
        Assert.Equal((ushort)secondDamage, battle.Presentation!.NativeActionPackets.ReadContact(new BattleContactOccurrence(1)).Primary.Amount6);
        Assert.Equal(10000 - firstDamage, battle.Combatants.Single(item => item.Id == "target-first").CurrentHp);
        Assert.Equal(10000 - secondDamage, battle.Combatants.Single(item => item.Id == "target-second").CurrentHp);
        Assert.All(battle.Events.Where(item => item.Kind == "Damage"), item =>
        {
            Assert.Equal(actor == 0 ? "party-0" : "party-1", item.ActorId);
            Assert.Equal(BattleAbility.GuardianForce, item.Ability);
            Assert.Equal(sequence, item.NativeSequence);
        });
        Assert.True(after.NativeActionScratch!.GuardianFormulaE1E);
        Assert.False(after.NativeActionScratch.DeferredEffectsE00);
        Assert.Equal(percent, after.NativeActionScratch.GuardianBoostPercent);
        Assert.Equal(actor, after.NativeActionScratch.ActorIndex);
        Assert.Equal((ushort)64, after.NativeActionScratch.Action);
    }

    [Theory]
    [InlineData(2, 64)]
    [InlineData(254, 63)]
    [InlineData(254, 80)]
    public void InvalidHeaderCommandOrActionIsRejectedWithoutAnActiveAction(byte command, ushort action)
    {
        BattleSession battle = Create(0, 14, command, action);
        Assert.Throws<InvalidDataException>(() => Recompute(battle, 100));
        Assert.Null(battle.ActiveAction);
    }

    [Fact]
    public void MissingCatalogAndUnpublishedHeaderRemainStrict()
    {
        BattleSession battle = Create(0, 14);
        BattleSnapshot saved = battle.Capture();
        BattleSession missing = BattleSession.Restore(saved with { Definition = saved.Definition with { NativeActions = [] } });
        Assert.Throws<NotSupportedException>(() => Recompute(missing, 100));
        NativeBattleActionPacketsState packets = saved.ReadFixturePresentation().NativeActionPackets!;
        NativeBattleActionPacketsState unprepared = NativeBattleActionPacketsState.Create(packets.Roster, packets.ExportRetained(), packets.ModelAliases);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, unprepared);
        Assert.Throws<InvalidDataException>(() => Recompute(battle, 100));
    }

    [Fact]
    public void LethalHeaderRecomputationDoesNotInventAnUnobservedRemainingHitByte()
    {
        BattleSnapshot saved = Create(0, 14).Capture();
        BattleSnapshot lethal = saved with { Combatants = saved.Combatants.Select(actor => actor.Id == "target-first" ? actor with { CurrentHp = 1 } : actor).ToArray() };
        BattleSession battle = BattleSession.Restore(lethal);
        Assert.Null(battle.Capture().Combatants.Single(actor => actor.Id == "target-first").NativeParticipant!.RemainingHits);
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Recompute(battle, 100));
        Assert.Contains("48EB40 remaining-hit", exception.Message);
    }

    private static BattleSession Create(byte actor, byte sequence, byte headerCommand = 254, ushort headerAction = 64)
    {
        BattleNativeAction native = new(254, 64, 2, 0) { MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 72, AttackFlags = 33, DamagePresentation = 5, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = 0, LevelModifier = 60, RawRecord = ImmutableArray.CreateRange(new byte[132]), SupplementalRecord = [] };
        BattleActionDefinition action = new(BattleAbility.GuardianForce, 0, "Synthetic kernel-layout GF", BattleActionEffect.Native, 44, false, Level: 12, DamagePercent: 20) { NativeAction = native, NativeGuardianBoostAvailable = false };
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = [native], ActionDefinitions = [action], Combatants = [new() { Id = "party-0", Name = "First party actor", IsPlayer = true }, new() { Id = "party-1", Name = "Second party actor", IsPlayer = true }, new() { Id = "target-second", Name = "Second contact", MaximumHp = 10000, CurrentHp = 10000, Spirit = 35 }, new() { Id = "target-first", Name = "First contact", MaximumHp = 10000, CurrentHp = 10000, Spirit = 5 }] }, BattleAtbMode.TurnBased);
        BattleSnapshot saved = battle.Capture();
        NativeBattleActionPacketsState packets = saved.ReadFixturePresentation().NativeActionPackets!;
        (packets, _) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(actor), headerCommand, sequence, -1, headerAction, 116, null));
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(4), 5, 1, 0, 0, 0, 0), null));
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(3), 5, 1, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(2);
        return BattleSession.Restore(saved.WithFixturePresentation(facts => facts with { NativeActionPackets = packets }));
    }

    private static void Recompute(BattleSession battle, byte percent)
    {
        try
        {
            typeof(BattleSession).GetMethod("RecomputeNativeGuardianContacts", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [percent]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}
