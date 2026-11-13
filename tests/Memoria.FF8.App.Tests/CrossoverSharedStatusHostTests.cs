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
    [InlineData("Reflect", 0x40000u, 1)]
    [InlineData("Confuse", 0x40u, 1)]
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

    [Fact]
    public void A_confused_guest_executes_its_original_attack_against_the_source_side_without_manual_selection()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalSpells: ["Confuse"]);
        fixture.Cast(0, "Confuse", 2);
        int stock = fixture.Stock(fixture.Spell("Cure"));
        Ff7BattleHit? contact = null;
        for (int tick = 0; tick < 20000 && contact is null; tick++)
        {
            if (fixture.Host.ReadyPlayer is int ready)
            {
                Assert.True(fixture.Host.Submit(ready, Ff7BattleCommand.Defend, 0, (ushort)(1 << ready)));
            }
            fixture.Host.Advance(waitForCommand: false);
            if (fixture.Host.LastHit is { Source: 1 } hit && hit.Tick == fixture.Host.Tick)
            {
                contact = hit;
            }
        }
        Assert.NotNull(contact);
        Assert.Contains(contact.Target, new[] { 0, 1 });
        Assert.Equal(stock, fixture.Stock(fixture.Spell("Cure")));
    }

    [Fact]
    public void Reflected_origin_magic_uses_the_host_recipient_and_charges_its_source_once()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalSpells: ["Reflect", "Fire"]);
        fixture.Cast(0, "Reflect", 2);
        fixture.IsolateReadyPlayer(0);
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
