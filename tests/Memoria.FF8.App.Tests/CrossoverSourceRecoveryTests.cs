using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSourceRecoveryTests
{
    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void Original_source_recovery_captures_missing_hp_once_for_the_caster_and_foreign_ally_after_queued_restore(byte crisis)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("White Wind", 3, crisis, damagedAllies: true, sourceHP: 1250);
        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true);
        Assert.Equal(new[] { 0, 1 }, result.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot));
        Ff7BattleGuestPreparedContact first = result.Contacts[0].HitDescription.PreparedContact!;
        Assert.Equal(1250u, first.SourceBefore.Hp);
        Assert.True(first.SourceBefore.MaxHp > 2 * first.SourceBefore.Hp);
        int recovery = checked((int)(first.SourceBefore.MaxHp - first.SourceBefore.Hp));
        Assert.Equal(first.SourceBefore.MaxHp, first.Receipt.Target.Hp);
        Assert.All(result.Contacts, contact =>
        {
            Ff7BattleGuestPreparedContact prepared = contact.HitDescription.PreparedContact!;
            Assert.False(prepared.Receipt.Damage.Missed);
            Assert.Equal(-recovery, prepared.Receipt.Damage.Damage);
            Assert.Equal(recovery, contact.HitDescription.Attack.FixedDamage);
            Assert.False(contact.HitDescription.Attack.SourceMissingHitPoints);
            Assert.Equal(Math.Min(prepared.TargetBefore.MaxHp, prepared.TargetBefore.Hp + (uint)recovery), prepared.Receipt.Target.Hp);
        });
        Ff7BattleGuestPreparedContact ally = result.Contacts[1].HitDescription.PreparedContact!;
        Assert.Equal(first.SourceBefore.MaxHp, ally.SourceBefore.Hp);
        Assert.Equal(30000u, ally.TargetBefore.Hp);
        Assert.True(ally.Receipt.Target.Hp > ally.TargetBefore.Hp);
        fixture.FollowWithFire();
    }
}
