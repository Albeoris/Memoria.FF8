namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterDamageRoutingTests
{
    [Theory]
    [InlineData(100, 50)]
    [InlineData(101, 50)]
    [InlineData(1, 0)]
    public void Shares_discard_the_remainder_and_leave_original_undamaged(int damage, int share)
    {
        var result = EncounterDamageRouting.Plan([new(0, "caster", "original", damage, EncounterHpCause.Damage)], Actors(), [Route()]);
        Assert.Equal(0, result.Single(receipt => receipt.RecipientId == "original").AppliedLoss);
        Assert.Equal(share, result.Single(receipt => receipt.RecipientId == "copy-a").AssignedDamage);
        Assert.Equal(share, result.Single(receipt => receipt.RecipientId == "copy-b").AssignedDamage);
    }

    [Theory]
    [InlineData(0, 1000, "copy-b")]
    [InlineData(0, 0, "original")]
    public void Each_sequential_group_rechecks_living_recipients(int firstHp, int secondHp, string recipient)
    {
        EncounterDamageActor[] actors = [new("original", 1000, true), new("copy-a", firstHp, true), new("copy-b", secondHp, true)];
        var result = EncounterDamageRouting.Plan([new(0, "caster", "original", 101, EncounterHpCause.Damage)], actors, [Route()]);
        Assert.Equal(101, result.Single(receipt => receipt.RecipientId == recipient).AppliedLoss);
    }

    [Fact]
    public void Simultaneous_direct_contacts_and_routed_shares_are_independent_of_actor_iteration_order()
    {
        EncounterHpIntent[] contacts = [new(0, "caster", "original", 100, EncounterHpCause.Damage), new(1, "caster", "copy-a", 100, EncounterHpCause.Damage), new(2, "caster", "copy-b", 100, EncounterHpCause.Damage)];
        var first = EncounterDamageRouting.Plan(contacts, Actors(), [Route()]);
        var reordered = EncounterDamageRouting.Plan(contacts.Reverse(), Actors().Reverse(), [Route() with { RecipientActorIds = ["copy-b", "copy-a"] }]);
        Assert.Equal(first, reordered);
        Assert.Equal(0, first.Where(receipt => receipt.RecipientId == "original").Sum(receipt => receipt.AppliedLoss));
        Assert.Equal(150, first.Where(receipt => receipt.RecipientId == "copy-a").Sum(receipt => receipt.AppliedLoss));
        Assert.Equal(150, first.Where(receipt => receipt.RecipientId == "copy-b").Sum(receipt => receipt.AppliedLoss));
    }

    [Fact]
    public void Overkill_is_discarded_and_routed_intents_do_not_route_again()
    {
        EncounterDamageActor[] actors = [new("original", 1000, true), new("copy-a", 10, true), new("copy-b", 1000, true)];
        EncounterDamageRoute secondRoute = new("other-route", "copy-a", ["original"]);
        var result = EncounterDamageRouting.Plan([new(0, "caster", "original", 100, EncounterHpCause.Damage)], actors, [Route(), secondRoute]);
        Assert.Equal(10, result.Single(receipt => receipt.RecipientId == "copy-a").AppliedLoss);
        Assert.Equal(50, result.Single(receipt => receipt.RecipientId == "copy-b").AppliedLoss);
        Assert.Equal(0, result.Single(receipt => receipt.RecipientId == "original").AppliedLoss);
    }

    [Theory]
    [InlineData(EncounterHpCause.Healing)]
    [InlineData(EncounterHpCause.HpCost)]
    [InlineData(EncounterHpCause.ScriptAssignment)]
    [InlineData(EncounterHpCause.DeathStatus)]
    public void Non_damage_causes_are_left_to_the_native_owner(EncounterHpCause cause)
    {
        Assert.Empty(EncounterDamageRouting.Plan([new(0, "caster", "original", 100, cause)], Actors(), [Route()]));
    }

    private static EncounterDamageActor[] Actors()
    {
        return [new("original", 1000, true), new("copy-a", 1000, true), new("copy-b", 1000, true)];
    }

    private static EncounterDamageRoute Route()
    {
        return new("mirror-route", "original", ["copy-a", "copy-b"]);
    }
}
