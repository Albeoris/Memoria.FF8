using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectIoSelectorTests
{
    [Theory]
    [InlineData(0, 1, 0, 0, true)]
    [InlineData(1, 1, 0, 0, false)]
    [InlineData(1, 0, 0, 0, true)]
    [InlineData(2, 0, 255, 0, false)]
    [InlineData(1, 1, 0, 1, true)]
    [InlineData(2, 1, 0, 1, false)]
    [InlineData(0, 0, 0, -2, false)]
    public void Completed_selector_acknowledgment_delay_and_signed_operand_match_native_branches(byte completed, byte acknowledged, byte delay, int index, bool waiting)
    {
        NativeBattleEffectMemory source = Memory([1, delay, acknowledged, 3, completed]);
        NativeBattleEffectIoPoll selected = NativeBattleEffectIo.Select5342C0(source, index);
        Assert.Equal(waiting, selected.Busy);
        Assert.Null(selected.Request);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(selected.Memory));
    }

    [Fact]
    public void Delay_advances_once_then_next_callback_observes_ready_without_advancing_again()
    {
        NativeBattleEffectMemory source = Memory([0, 1, 1, 1, 1]);
        NativeBattleEffectIoPoll first = NativeBattleEffectIo.Select5342C0(source, 0);
        Assert.True(first.Busy);
        Assert.Equal((byte)0, first.Memory.Arena!.Read(65, 1)[0]);
        NativeBattleEffectIoPoll second = NativeBattleEffectIo.Select5342C0(first.Memory, 0);
        Assert.False(second.Busy);
        Assert.Same(first.Memory, second.Memory);
        Assert.Equal((byte)1, source.Arena!.Read(65, 1)[0]);
    }

    [Fact]
    public void Not_ready_starts_one_actual_queued_file_and_ready_path_never_reads_unrelated_header_bytes()
    {
        NativeBattleEffectMemory source = Memory([0, 0, 1, 0, 0]);
        source = NativeBattleEffectIo.Queue5341D0(source, 727, 0x2100000, 3);
        NativeBattleEffectIoPoll pending = NativeBattleEffectIo.Select5342C0(source, 0);
        Assert.True(pending.Busy);
        Assert.Equal(new NativeBattleEffectFileRequest(727, 0x2100000, 0x534150), pending.Request);
        NativeBattleEffectMemory ready = Memory([0, 0, 1, 2, 1]);
        ready = ready with { Arena = ready.Arena!.MarkUnknown(64, 1).MarkUnknown(67, 1) };
        Assert.False(NativeBattleEffectIo.Select5342C0(ready, 0).Busy);
    }

    private static NativeBattleEffectMemory Memory(byte[] header)
    {
        NativeBattleEffectMemory source = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(325), 64);
        return source with { EffectIo = source.EffectIo! with { Callback = 0x534150 }, Arena = source.Arena!.Write(64, header) };
    }
}
