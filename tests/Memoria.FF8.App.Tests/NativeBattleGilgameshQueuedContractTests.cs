using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgameshQueuedContractTests
{
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
