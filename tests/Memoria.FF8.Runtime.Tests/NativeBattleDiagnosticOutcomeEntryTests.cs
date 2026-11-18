using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiagnosticOutcomeEntryTests
{
    [Fact]
    public void Effect77_process_entry_accepts_only_its_current_zero_amount_diagnostic_owner()
    {
        var fixture = Create();
        fixture.Receipt.Validate(fixture.Invocation, fixture.Packet, 5, fixture.Header, fixture.Contacts);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("token")]
    [InlineData("gameplay")]
    [InlineData("slot")]
    [InlineData("serial")]
    [InlineData("header")]
    [InlineData("generation")]
    [InlineData("missing-generation")]
    [InlineData("zero-generation")]
    [InlineData("source")]
    [InlineData("empty")]
    [InlineData("unknown-count")]
    [InlineData("damage")]
    [InlineData("secondary")]
    [InlineData("not-issued")]
    [InlineData("replay")]
    [InlineData("retired")]
    [InlineData("status-mask")]
    [InlineData("status-rows")]
    [InlineData("status-write")]
    [InlineData("status-missing")]
    public void Effect77_process_entry_rejects_stale_foreign_or_non_diagnostic_receipts(string fault)
    {
        var fixture = Create();
        NativeBattleDiagnosticOutcomeEntry receipt = fixture.Receipt;
        BattlePresentationProcess.HostedPresentationInvocation? current = fixture.Invocation;
        NativeBattleDescriptorPacket packet = fixture.Packet;
        NativeBattleActionHeaderSlot header = fixture.Header;
        ImmutableArray<NativeBattleContactPacket> contacts = fixture.Contacts;
        long? generation = 5;
        switch (fault)
        {
            case "missing":
                receipt = receipt with { Invocation = null! };
                break;
            case "foreign":
                current = current with { };
                break;
            case "token":
                receipt = receipt with { Invocation = fixture.Invocation with { Token = 2 } };
                break;
            case "gameplay":
                current = fixture.Invocation with { Provenance = BattlePresentationInvocationProvenance.Gameplay };
                receipt = receipt with { Invocation = current };
                break;
            case "slot":
                current = fixture.Invocation with { DescriptorSlot = -1 };
                receipt = receipt with { Invocation = current };
                break;
            case "serial":
                packet = packet with { Bytes = packet.Bytes.SetItem(0, 8) };
                break;
            case "header":
                packet = packet with { Header = new(1) };
                break;
            case "generation":
                generation = 6;
                break;
            case "missing-generation":
                generation = null;
                break;
            case "zero-generation":
                receipt = receipt with { HeaderGeneration = 0 };
                break;
            case "source":
                receipt = receipt with { Source = OriginalOwner(0) };
                break;
            case "empty":
                contacts = [];
                break;
            case "unknown-count":
                header = header with { ContactCount = null };
                break;
            case "damage":
                contacts = [contacts[0] with { Primary = contacts[0].Primary with { Amount6 = 1 } }];
                break;
            case "secondary":
                contacts = [contacts[0] with { Secondary = contacts[0].Primary }];
                break;
            case "not-issued":
                current = fixture.Invocation with { DiagnosticOutcomeEntryIssued = false };
                receipt = receipt with { Invocation = current };
                break;
            case "replay":
                current = fixture.Invocation with { DiagnosticOutcomeEntryAcknowledged = true };
                receipt = receipt with { Invocation = current };
                break;
            case "retired":
                packet = packet with { Bytes = packet.Bytes.SetItem(1, 255) };
                break;
            case "status-mask":
                receipt = receipt with { BeforeStatuses = new([new(0, 1), new(0, 0), new(0, 0)]) };
                break;
            case "status-rows":
                receipt = receipt with { BeforeStatuses = new([]) };
                break;
            case "status-write":
                receipt = receipt with { AfterStatuses = NativeBattlePhysicalPartyStatuses.Unknown() };
                break;
            case "status-missing":
                receipt = receipt with { BeforeStatuses = null! };
                break;
        }
        Assert.Throws<InvalidDataException>(() => receipt.Validate(current, packet, generation, header, contacts));
    }

    private static (BattlePresentationProcess.HostedPresentationInvocation Invocation, NativeBattleDiagnosticOutcomeEntry Receipt, NativeBattleDescriptorPacket Packet, NativeBattleActionHeaderSlot Header, ImmutableArray<NativeBattleContactPacket> Contacts) Create()
    {
        BattleModelOwner source = OriginalOwner(3);
        BattlePresentationProcess.HostedPresentationInvocation invocation = new(1, 2, 7, new(0)) { Provenance = BattlePresentationInvocationProvenance.ExternalDiagnostic, DiagnosticOutcomeEntryIssued = true };
        NativeBattleDescriptorPacket packet = new([7, 128, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0]) { Header = invocation.Header };
        NativeBattleActionHeaderSlot header = new(source, 8, 18, -1, 356, 77, new(0), null, 1, 0);
        ImmutableArray<NativeBattleContactPacket> contacts = [new(new(source, 0, 1, 0, 0, 0, 0), null)];
        NativeBattlePhysicalPartyStatuses before = NativeBattlePhysicalPartyStatuses.Unknown();
        return (invocation, new(invocation, 5, source) { BeforeStatuses = before, AfterStatuses = before.ClearHighBits() }, packet, header, contacts);
    }
}
