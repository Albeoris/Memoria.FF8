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
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);

        Recompute(battle, percent);
        Recompute(restored, percent);

        BattleSnapshot after = battle.Capture();
        Assert.Null(after.ActiveAction);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(after.Random));
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal((byte)2, battle.NativeActionPackets.ContactCount);
        Assert.Equal((byte)4, battle.NativeActionPackets.ReadContact(0).Primary.ActorIndex);
        Assert.Equal((byte)3, battle.NativeActionPackets.ReadContact(1).Primary.ActorIndex);
        Assert.Equal((ushort)firstDamage, battle.NativeActionPackets.ReadContact(0).Primary.Amount6);
        Assert.Equal((ushort)secondDamage, battle.NativeActionPackets.ReadContact(1).Primary.Amount6);
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
    [InlineData(1, 2)]
    [InlineData(4, 63)]
    [InlineData(4, 80)]
    [InlineData(0, 7)]
    public void InvalidHeaderCommandActionOrActorIsNotReplacedByPresentationState(int offset, byte value)
    {
        BattleSession battle = Create(0, 14);
        BattleSnapshot saved = battle.Capture();
        NativeBattleActionPacketsState packets = saved.NativeActionPackets!;
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        header = header with { Payload = header.Payload.SetItem(offset, value) };
        battle = BattleSession.Restore(saved with { NativeActionPackets = packets with { Headers = packets.Headers.SetItem(0, header) } });
        if (offset == 0)
        {
            Assert.Throws<NotSupportedException>(() => Recompute(battle, 100));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => Recompute(battle, 100));
        }
        Assert.Null(battle.ActiveAction);
    }

    [Fact]
    public void MissingCatalogAndUnknownRequiredActionOrSequenceBytesRemainStrict()
    {
        BattleSession battle = Create(0, 14);
        BattleSnapshot saved = battle.Capture();
        NativeBattleActionPacketsState packets = saved.NativeActionPackets!;
        BattleSession missing = BattleSession.Restore(saved with { Definition = saved.Definition with { NativeActions = [] } });
        Assert.Throws<NotSupportedException>(() => Recompute(missing, 100));
        foreach (int offset in new[] { 2, 4, 5 })
        {
            NativeBattleActionHeaderSlot header = packets.Headers[0];
            header = header with { UnknownByteMask = header.UnknownByteMask | 1u << offset };
            //A direct reached-header fault exercises ReadByte without weakening the snapshot's prepared-slot validation.
            typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, packets with { Headers = packets.Headers.SetItem(0, header) });
            Assert.Throws<NotSupportedException>(() => Recompute(battle, 100));
        }
    }

    private static BattleSession Create(byte actor, byte sequence)
    {
        BattleNativeAction native = new(254, 64, 2, 0) { MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 72, AttackFlags = 33, DamagePresentation = 5, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = 0, LevelModifier = 60, RawRecord = ImmutableArray.CreateRange(new byte[132]), SupplementalRecord = [] };
        BattleActionDefinition action = new(BattleAbility.GuardianForce, 0, "Synthetic kernel-layout GF", BattleActionEffect.Native, 44, false, Level: 12, DamagePercent: 20) { NativeAction = native, NativeGuardianBoostAvailable = false };
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = [native], ActionDefinitions = [action], Combatants = [new() { Id = "party-0", IsPlayer = true }, new() { Id = "party-1", IsPlayer = true }, new() { Id = "target-second", MaximumHp = 10000, CurrentHp = 10000, Spirit = 35 }, new() { Id = "target-first", MaximumHp = 10000, CurrentHp = 10000, Spirit = 5 }] }, BattleAtbMode.TurnBased);
        BattleSnapshot saved = battle.Capture();
        NativeBattleActionPacketsState packets = saved.NativeActionPackets!;
        (packets, _) = packets.Prepare(new(actor, 254, sequence, -1, 64, 116, null));
        (packets, _) = packets.AppendContact(new(new(4, 5, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(3, 5, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        return BattleSession.Restore(saved with { NativeActionPackets = packets });
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
