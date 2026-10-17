using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlFirstWindowIoTests
{
    [Theory]
    [InlineData(30, new short[] { 508, 509, 510 }, new short[] { 2, 1, 3 }, new uint[] { 0x2108954, 0x2108954, 0x2108954 })]
    [InlineData(60, new short[] { 511, 512 }, new short[] { 1, 5 }, new uint[] { 0x2108954, 0x2111154 })]
    [InlineData(76, new short[] { 513 }, new short[] { 5 }, new uint[] { 0x1298C68 })]
    [InlineData(85, new short[] { 514, 515 }, new short[] { 0, 5 }, new uint[] { 0x2108954, 0x12A1164 })]
    [InlineData(161, new short[] { 516, 517, 518, 519, 520 }, new short[] { 0, 5, 1, 1, 3 }, new uint[] { 0x2108954, 0x12A9664, 0x2108954, 0x2108954, 0x2108954 })]
    [InlineData(211, new short[] { 521, 522 }, new short[] { 5, 1 }, new uint[] { 0x1298C68, 0x2108954 })]
    public void NativeBatchWaitsForPollThenPreservesOriginalOrderWidthsTypesAndDestinations(ushort phase, short[] files, short[] types, uint[] destinations)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleQuezacotlFirstWindowIoStep step = NativeBattleQuezacotlFirstWindowIo.Advance(memory, phase);
        Assert.True(step.AdvancePhase);
        Assert.Equal(new NativeBattleEffectFileRequest(files[0], destinations[0], 0x6C3580), step.Request);
        byte[] header = step.Memory.Arena!.Read(64, 5);
        Assert.Equal(new byte[] { 1, 0, 1, checked((byte)files.Length), 0 }, header);
        for (int index = 0; index < files.Length; index++)
        {
            byte[] descriptor = step.Memory.Arena.Read(72 + index * 8, 8);
            Assert.Equal(types[index], BinaryPrimitives.ReadInt16LittleEndian(descriptor));
            Assert.Equal(files[index], BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(2)));
            Assert.Equal(destinations[index], BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4)));
        }
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(step.Memory))!;
        NativeBattleQuezacotlFirstWindowIoStep busy = NativeBattleQuezacotlFirstWindowIo.Advance(restored, phase);
        Assert.False(busy.AdvancePhase);
        Assert.Null(busy.Request);
        Assert.Equal(header, busy.Memory.Arena!.Read(64, 5));
    }

    [Fact]
    public void BusyPollConsumesByteDelayWithoutAppendingOrAdvancingButDefaultPhaseDoesNotPoll()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(64, [0, 1, 1, 0, 0]) };
        NativeBattleQuezacotlFirstWindowIoStep waiting = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 30);
        Assert.False(waiting.AdvancePhase);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, waiting.Memory.Arena!.Read(64, 5));
        NativeBattleQuezacotlFirstWindowIoStep ready = NativeBattleQuezacotlFirstWindowIo.Advance(waiting.Memory, 30);
        Assert.True(ready.AdvancePhase);
        Assert.Equal((short)508, ready.Request!.FileId);
        NativeBattleQuezacotlFirstWindowIoStep defaultBranch = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 31);
        Assert.True(defaultBranch.AdvancePhase);
        Assert.Null(defaultBranch.Request);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, defaultBranch.Memory.Arena!.Read(64, 5));
    }

    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(116), 64);
        return memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
    }
}
