using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectIoTests
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void StreamCompletionReadsOnlyItsLeadingDWORDAndPreservesUnrelatedUnknownBytes(short type, byte delay)
    {
        const int sourceOffset = 512;
        const uint pointer = 0x20DFAB8 + sourceOffset;
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(116), 64);
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
        memory = NativeBattleEffectIo.Queue5341D0(memory, 520, pointer, type);
        memory = NativeBattleEffectIo.Advance534210(memory).Memory;
        memory = memory with { Arena = memory.Arena!.Write(sourceOffset, [119, 0, 0, 0]).MarkUnknown(sourceOffset + 4, NativeBattleEffectArena.Length - sourceOffset - 4) };
        NativeBattleEffectMemory pending = NativeBattleStorageSnapshots.Capture(memory);
        List<(uint Address, int? Length)> reads = [];
        NativeBattleEffectIoCompletion completed = NativeBattleEffectIo.Complete6C3580(memory, (address, length) =>
        {
            reads.Add((address, length));
            Assert.Equal(4, length);
            return new("actual mutable source", memory.Arena!.Read(checked((int)(address - 0x20DFAB8)), length!.Value).ToImmutableArray());
        });
        Assert.Equal(new[] { (pointer, (int?)4) }, reads);
        Assert.Equal(new NativeBattleSummonStreamRequest(pointer, 690119), completed.Stream);
        Assert.Equal(new NativeBattleSummonStreamState(690119, pointer), completed.Memory.SummonStream);
        Assert.Equal(new byte[] { 0, delay, 1, 1, 1 }, completed.Memory.Arena!.Read(64, 5));
        Assert.Throws<NotSupportedException>(() => completed.Memory.Arena.Read(sourceOffset + 4, 1));
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(completed.Memory)))!);
        Assert.Equal(completed.Memory.SummonStream, restored.SummonStream);
        Assert.Throws<NotSupportedException>(() => restored.Arena!.Read(sourceOffset + 4, 1));
        memory = NativeBattleStorageSnapshots.Restore(pending);
        memory = memory with { Arena = memory.Arena!.MarkUnknown(sourceOffset, 1) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectIo.Complete6C3580(memory, (address, length) => new("unknown leading DWORD", memory.Arena!.Read(checked((int)(address - 0x20DFAB8)), length!.Value).ToImmutableArray())));
    }

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
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(completed.Memory)))!);
        Assert.Equal(new uint[] { 0, 0, 0, 0, 0 }, restored.BeginLoad(1).QuezacotlSecondaryHeads!.Value.ToArray());
        Assert.Null((restored with { QuezacotlSecondaryHeads = null }).BeginLoad(1).QuezacotlSecondaryHeads);
    }
}
