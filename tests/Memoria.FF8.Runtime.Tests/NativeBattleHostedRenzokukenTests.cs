using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedRenzokukenTests
{
    [Fact]
    public void Pending_finisher_contact_replacement_uses_its_exact_header_extent_and_receipt_once()
    {
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants =
            [
                new() { Id = "squall", Name = "Squall", IsPlayer = true },
                new() { Id = "target", Name = "Target" }
            ]
        };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        BattlePartyModelOwner caster = Assert.IsAssignableFrom<BattlePartyModelOwner>(logical.ReadPresentationModelOwner("squall"));
        BattleModelOwner target = logical.ReadPresentationModelOwner("target");
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0) { ModelId4 = 0 }) with { EntryReady = true, EntryLifecycle = 4 };
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        BattlePresentationProcess child = NativeBattleHostedTestFixture.SeedProcess(logical, graphics, definition with { NativeGraphics = new(rules, [new("squall", 0, new([0, 0]))], []) });
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, 0, 0, 0), null);
        (NativeBattleActionPacketsState packets, BattleActionHeaderReference initial) = child.NativeActionPackets.Prepare(new(caster, 5, 21, -1, 65534, 160, null));
        packets = packets.AppendContacts([contact]).Publish(1).State.WritePlayerRepeatMode(initial, 1);
        BattleActionHeaderReference finisher;
        (packets, finisher) = packets.Prepare(new(caster, 250, 0, -1, ushort.MaxValue, 165, new(0x47E5F0, 3)));
        packets = packets.AppendContacts([contact, contact, contact]).Publish(3).State.WritePlayerRepeatMode(finisher, 1);
        Field("nativeActionPackets").SetValue(child, packets);
        HostedRenzokukenOwner owner = new(caster, "squall", 160, 3);
        NativeBattleRenzokukenFinisherReached receipt = new(owner, finisher, 1, 2);
        Field("pendingHostedRenzokukenFinisher").SetValue(child, receipt);
        NativeBattleContactPacket[] replacements = Enumerable.Range(1, 3).Select(amount => contact with { Primary = contact.Primary with { Amount6 = checked((ushort)amount) } }).ToArray();
        var update = new BattlePresentationProcess.HostedRenzokukenFinisherReplacement(receipt, packets, packets.ReadHeader(finisher).FirstContact, replacements.ToImmutableArray(), []);

        Assert.Throws<InvalidOperationException>(() => child.PrepareHostedDispatch([], finisher, renzokukenFinisher: update with { Receipt = receipt with { Serial = 3 } }));
        Assert.Throws<InvalidDataException>(() => child.PrepareHostedDispatch([], finisher, renzokukenFinisher: update with { Contacts = [contact] }));
        Assert.Throws<InvalidDataException>(() => child.PrepareHostedDispatch([], finisher, renzokukenFinisher: update with { FirstContact = update.FirstContact.Advance(1) }));
        Assert.Same(receipt, child.PendingHostedRenzokukenFinisher);
        Assert.Same(packets, child.NativeActionPackets);
        Assert.Empty(child.Graphics.Descriptors.Queue);

        BattlePresentationProcess.PreparedHostedDispatch prepared = child.PrepareHostedDispatch([], finisher, renzokukenFinisher: update);
        Assert.Same(receipt, child.PendingHostedRenzokukenFinisher);
        child.ValidatePreparedHostedDispatch(prepared);
        child.CommitHostedDispatch(prepared);

        Assert.Null(child.PendingHostedRenzokukenFinisher);
        Assert.Equal(initial, Assert.Single(child.NativeActionPackets.PublishedHeaders(), entry => entry.Slot.Command == 5).Reference);
        Assert.Equal(finisher, prepared.Action!.Invocation.Header);
        Assert.Equal(new ushort[] { 1, 2, 3 }, child.NativeActionPackets.ReadHeaderContacts(finisher).Select(packet => packet.Primary.Amount6));
        Assert.All(packets.ReadHeaderContacts(finisher), packet => Assert.Equal((ushort)0, packet.Primary.Amount6));
        Assert.Throws<InvalidOperationException>(() => child.ValidatePreparedHostedDispatch(prepared));
    }

    private static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
