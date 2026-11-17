using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgameshQueuedContractTests
{
    [Theory]
    [InlineData((ushort)0, 1)]
    [InlineData((ushort)1, 0)]
    [InlineData((ushort)0x1000, 1)]
    [InlineData((ushort)0x1001, 1)]
    public void Contact_text_publication_oracle_preserves_native_suppression_and_its_explicit_override(ushort flags, int publications)
    {
        NativeBattleContactPacket contact = new(new(OriginalOwner(3), 4, unchecked((byte)flags), unchecked((byte)(flags >> 8)), 0, 7, 0), null);
        NativeBattleEffectsState effects = OriginalEffects().PrependDamageText(contact.Primary.Recipient, flags, 7, OriginalRoster);
        NativeBattleEffectTask[] text = effects.Slots.Where(task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0).ToArray();
        Assert.Equal(publications, text.Length);
        NativeBattleGilgameshCaptureAssertions.AssertContactTextPublications(contact, publications);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertContactTextPublications(contact, 1 - publications));
        Assert.Equal(flags, contact.Primary.ContactFlags);
        Assert.All(text, task =>
        {
            Assert.Equal(contact.Primary.Recipient, task.ModelOwner);
            Assert.Equal((flags & 0x1000) != 0 ? new byte[] { 69, 0 } : new byte[] { 63, 0 }, task.Glyphs);
            Assert.Equal(0x808080u, task.Color);
        });
    }

    [Theory]
    [InlineData((ushort)327, (ushort)10)]
    [InlineData((ushort)328, (ushort)9)]
    [InlineData((ushort)329, (ushort)7)]
    [InlineData((ushort)330, (ushort)8)]
    public void Direct245_queued_contract_requires_native_sequence_zero_enemy_contact_flag_and_exact_alias(ushort effect, ushort action)
    {
        byte sequence = NativeBattleComponentInputs.SelectSequence([], 245, action);
        Assert.Equal((byte)0, sequence);
        (NativeBattleActionPacketsState packets, BattleActionHeaderReference index) = OriginalPackets().Prepare(new(OriginalOwner(0), 245, sequence, -1, action, effect, null));
        NativeBattleActionHeaderSlot header = packets.ReadHeader(index);
        NativeBattleContactPacket contact = new(new(OriginalOwner(3), 4, 1, 0, 0, 7, 0), null);
        byte[] before = packets.ExportRetained().Headers[index.Ordinal].Payload.ToArray();
        NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header, contact, OriginalAliases);
        Assert.Equal(before, packets.ExportRetained().Headers[index.Ordinal].Payload);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header with { Sequence = 9 }, contact, OriginalAliases));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header, contact with { Primary = contact.Primary with { Flags2 = 0 } }, OriginalAliases));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header with { Command = 2 }, contact, OriginalAliases));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header with { Action = checked((ushort)(action + 1)) }, contact, OriginalAliases));
    }
}
