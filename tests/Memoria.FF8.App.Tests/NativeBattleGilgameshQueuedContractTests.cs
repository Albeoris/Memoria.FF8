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
        NativeBattleContactPacket contact = new(new(3, 4, unchecked((byte)flags), unchecked((byte)(flags >> 8)), 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0));
        NativeBattleEffectsState effects = NativeBattleEffectsState.Clear().PrependDamageText(3, flags, 7);
        NativeBattleEffectTask[] text = effects.Slots.Where(task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0).ToArray();
        Assert.Equal(publications, text.Length);
        NativeBattleGilgameshCaptureAssertions.AssertContactTextPublications(contact, publications);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertContactTextPublications(contact, 1 - publications));
        Assert.Equal(flags, contact.Primary.ContactFlags);
        Assert.All(text, task =>
        {
            Assert.Equal((byte)3, task.ModelIndex);
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
        byte sequence = NativeBattleEffectFixtureExporter.SelectSequence([], 245, action);
        Assert.Equal((byte)0, sequence);
        (NativeBattleActionPacketsState packets, int index) = NativeBattleActionPacketsState.Fresh().Prepare(new(0, 245, sequence, -1, action, effect, null));
        NativeBattleActionHeaderSlot header = packets.Headers[index];
        NativeBattleContactPacket contact = new(new(3, 4, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0));
        byte[] before = header.Payload.ToArray();
        NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header, contact);
        Assert.Equal(before, header.Payload);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header with { Payload = header.Payload.SetItem(2, 9) }, contact));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header, contact with { Primary = contact.Primary with { Flags2 = 0 } }));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header with { Payload = header.Payload.SetItem(1, 2) }, contact));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleGilgameshCaptureAssertions.AssertQueuedInvocation(effect, header with { Payload = header.Payload.SetItem(4, checked((byte)(action + 1))) }, contact));
    }
}
