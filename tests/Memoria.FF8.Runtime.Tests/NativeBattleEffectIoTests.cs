using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectIoTests
{
    [Fact]
    public void PollHonorsAcknowledgmentBusyAndByteDelayWithoutInventingACompletion()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = NativeBattleEffectIo.Initialize534110(memory, 64);
        NativeBattleEffectIoPoll empty = NativeBattleEffectIo.Poll534270(memory);
        Assert.False(empty.Busy);
        Assert.Null(empty.Request);
        memory = NativeBattleEffectIo.Queue5341D0(empty.Memory, 505, 0x2108954, 1);
        NativeBattleEffectIoPoll started = NativeBattleEffectIo.Poll534270(memory);
        Assert.True(started.Busy);
        Assert.Equal((short)505, started.Request!.FileId);
        NativeBattleEffectIoPoll waiting = NativeBattleEffectIo.Poll534270(started.Memory);
        Assert.True(waiting.Busy);
        Assert.Null(waiting.Request);
        memory = waiting.Memory with { Arena = waiting.Memory.Arena!.Write(64, [0, 2, 1, 1, 1]) };
        NativeBattleEffectIoPoll delay = NativeBattleEffectIo.Poll534270(memory);
        Assert.True(delay.Busy);
        Assert.Equal(1, delay.Memory.Arena!.Read(65, 1)[0]);
        delay = NativeBattleEffectIo.Poll534270(delay.Memory);
        Assert.True(delay.Busy);
        NativeBattleEffectIoPoll completed = NativeBattleEffectIo.Poll534270(delay.Memory);
        Assert.False(completed.Busy);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, completed.Memory.Arena!.Read(64, 5));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(completed.Memory))!;
        Assert.Equal(new uint[] { 0, 0, 0, 0, 0 }, restored.BeginLoad(1).QuezacotlSecondaryHeads!.Value.ToArray());
        Assert.Null((restored with { QuezacotlSecondaryHeads = null }).BeginLoad(1).QuezacotlSecondaryHeads);
    }
}
