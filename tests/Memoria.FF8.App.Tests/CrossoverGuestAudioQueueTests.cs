using System.Text.Json;
using Memoria.FF8.Runtime;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestAudioQueueTests
{
    [Fact]
    public void Multiple_native_callbacks_remain_pending_until_acknowledged_and_cold_restore_retains_voice_allocation()
    {
        Guid encounter = Guid.NewGuid();
        CrossoverGuestAudioState source = CrossoverGuestAudioState.Create();
        source = source.Append(encounter, 10, CrossoverGuestAudioKind.Module, new(5, 0, null) { Pan = 32 });
        source = source.Append(encounter, 10, CrossoverGuestAudioKind.Module, new(6, 0, null) { Pan = 96 });
        source = source.Append(encounter, 11, CrossoverGuestAudioKind.Fire, new(7, 0x100, null) { Pan = 64 });
        source.Validate(encounter, 11);
        Assert.Equal(new long[] { 1, 2, 3 }, source.Pending.Select(receipt => receipt.Ordinal));
        Assert.All(source.Pending, receipt =>
        {
            Assert.Equal(SessionGame.Ff8, receipt.Origin);
            Assert.NotNull(receipt.Sound.Allocation);
        });
        CrossoverGuestAudioState cold = JsonSerializer.Deserialize<CrossoverGuestAudioState>(JsonSerializer.Serialize(source))!;
        cold.Validate(encounter, 11);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(cold));
        CrossoverGuestAudioState acknowledged = cold.Acknowledge(1);
        Assert.Equal(new long[] { 2, 3 }, acknowledged.Pending.Select(receipt => receipt.Ordinal));
        Assert.Same(acknowledged, acknowledged.Acknowledge(1));
        NativeBattleModuleSound trigger = new(8, 0, null) { Pan = 64 };
        CrossoverGuestAudioState next = acknowledged.Append(encounter, 12, CrossoverGuestAudioKind.Trigger, trigger);
        CrossoverGuestAudioState uninterrupted = source.Acknowledge(1).Append(encounter, 12, CrossoverGuestAudioKind.Trigger, trigger);
        Assert.Equal(JsonSerializer.Serialize(uninterrupted), JsonSerializer.Serialize(next));
        Assert.Equal(new long[] { 2, 3, 4 }, next.Pending.Select(receipt => receipt.Ordinal));
        Assert.Throws<InvalidDataException>(() => next.Acknowledge(5));
        Assert.Throws<InvalidDataException>(() => next.Append(encounter, 13, CrossoverGuestAudioKind.Fire, next.Pending[0].Sound));
        Assert.Throws<InvalidDataException>(() => next.Validate(Guid.NewGuid(), 12));
        Assert.Empty(next.Acknowledge(4).Pending);
    }
}
