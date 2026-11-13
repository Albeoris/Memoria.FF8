using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSharedStatusHostTests
{
    [Fact]
    public void Older_guest_snapshot_recovers_new_counterparts_without_replaying_progress_or_resurrecting_cured_statuses()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new();
        Ff7BattleSnapshot before = fixture.Host.Capture();
        const uint origin = 0x5C90;
        Ff7BattleSnapshot legacy = before with { GuestProjectedBattleMask = null, GuestOriginStatuses = [new(0, origin)] };

        fixture.RestoreQueuedBoundary(legacy);

        Ff7BattleSnapshot restored = fixture.Host.Capture();
        uint expected = Ff7GuestBattleStatuses.ToHostStatuses(0, origin);
        Assert.Equal(expected, fixture.Host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & expected);
        Assert.Equal(origin, fixture.Host.GetGuestOriginBattleStatuses(0) & origin);
        Assert.Equal(before.Tick, restored.Tick);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Random), System.Text.Json.JsonSerializer.Serialize(restored.Random));
        Assert.Equal(before.Inventory, restored.Inventory);
        Assert.Equal(before.Actors.Select(actor => actor.Actor.HitPoints), restored.Actors.Select(actor => actor.Actor.HitPoints));
        Assert.Equal(Ff7GuestBattleStatuses.SupportedOriginBattle, restored.GuestProjectedBattleMask);

        Ff7BattleSnapshot cured = restored with { Actors = restored.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = actor.Actor.Statuses & ~expected } } : actor).ToArray() };
        fixture.RestoreQueuedBoundary(cured);
        Assert.Equal(0u, fixture.Host.GetGuestOriginBattleStatuses(0) & origin);
    }

    [Theory]
    [InlineData("Regen", 0x8000u, 1)]
    [InlineData("Reflect", 0x40000u, 2)]
    [InlineData("Confuse", 0x40u, 2)]
    public void Original_magic_applies_its_counterpart_through_the_host_status_owner(string name, uint status, int guestCount)
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(guestCount: guestCount, additionalSpells: [name]);
        ushort spell = fixture.Spell(name);
        int stock = fixture.Stock(spell);

        fixture.Cast(0, name, 2);

        Assert.Equal(guestCount == 2, fixture.Host.GuestVitalsBindings.Any(binding => binding.HostSlot == 1));
        Assert.Equal(status, fixture.Host.Actors.Single(actor => actor.Actor.Slot == 1).Actor.Statuses & status);
        Assert.Equal(stock - 1, fixture.Stock(spell));
        Assert.Null(fixture.Host.Action);
        Assert.Null(fixture.Providers[0].Invocation);
    }

    [Theory]
    [InlineData("Reflect")]
    [InlineData("Confuse")]
    public void A_missing_foreign_model_consumer_remains_red_after_its_status_counterpart_is_supported(string name)
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(guestCount: 1, additionalSpells: [name]);
        fixture.Ready(0);
        ushort spell = fixture.Spell(name);
        int stock = fixture.Stock(spell);
        string before = System.Text.Json.JsonSerializer.Serialize(fixture.Host.Capture());

        string reason = Assert.IsType<string>(fixture.Host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, spell, 1));

        Assert.Contains("skeleton", reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(fixture.Host.Submit(0, Ff7BattleCommand.Magic, spell, 2));
        Assert.Equal(stock, fixture.Stock(spell));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(fixture.Host.Capture()));
    }

    [Fact]
    public void Reflected_origin_magic_uses_the_host_recipient_and_charges_its_source_once()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalSpells: ["Reflect", "Fire"]);
        fixture.Cast(0, "Reflect", 2);
        uint targetHp = fixture.Hp(1);
        uint sourceHp = fixture.Hp(0);
        ushort spell = fixture.Spell("Fire");
        int stock = fixture.Stock(spell);
        uint enemyHp = fixture.Host.Actors.Where(actor => actor.Actor.Slot >= 4).Aggregate(0u, (total, actor) => total + actor.Actor.HitPoints);

        fixture.Cast(0, "Fire", 2);

        Assert.Equal(targetHp, fixture.Hp(1));
        Assert.Equal(sourceHp, fixture.Hp(0));
        Assert.True(fixture.Host.Actors.Where(actor => actor.Actor.Slot >= 4).Aggregate(0u, (total, actor) => total + actor.Actor.HitPoints) < enemyHp);
        Assert.Equal(stock - 1, fixture.Stock(spell));
        Assert.Contains(fixture.Host.LastHit!.Target, new[] { 4, 5 });
    }
}
