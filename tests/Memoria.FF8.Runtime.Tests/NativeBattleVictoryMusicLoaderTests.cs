using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVictoryMusicLoaderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Caller_completion_is_the_physical_retained_task_byte_and_preserves_every_other_byte(int slot)
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        state = state with { Tasks = state.Tasks.Select((task, index) => task with { Payload = Enumerable.Range(0, 8).Select(value => (byte)(index * 8 + value)).ToImmutableArray() }).ToImmutableArray() };
        uint address = NativeBattleDescriptorState.CompletionAddress503040(slot);
        Assert.Equal(0x1D96D26u + (uint)slot * 20, address);
        NativeBattleDescriptorState cleared = state.WriteCompletion501A70(address, 0);
        NativeBattleDescriptorState independent = JsonSerializer.Deserialize<NativeBattleDescriptorState>(JsonSerializer.Serialize(cleared))!;
        NativeBattleDescriptorState completed = independent.WriteCompletion501A70(address, 1);
        for (int task = 0; task < 4; task++)
        {
            for (int offset = 0; offset < 8; offset++)
            {
                Assert.Equal(task == slot && offset == 2 ? (byte)1 : state.Tasks[task].Payload[offset], completed.Tasks[task].Payload[offset]);
            }
        }
        Assert.Equal((byte)(slot * 8 + 2), state.Tasks[slot].Payload[2]);
        Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(cleared.WriteCompletion501A70(address, 1)));
        Assert.Throws<NotSupportedException>(() => state.WriteCompletion501A70(address - 1, 1));
        Assert.Throws<NotSupportedException>(() => state.WriteCompletion501A70(address + 1, 1));
        Assert.Throws<NotSupportedException>(() => state.WriteCompletion501A70(0x1D96D26 + 80, 1));
    }

    [Theory]
    [InlineData(byte.MaxValue)]
    [InlineData((byte)4)]
    [InlineData((byte)3)]
    public void Unreached_or_waiting_phases_do_not_read_missing_resource_and_command_owners(byte phase)
    {
        NativeBattleVictoryMusicStep step = NativeBattleVictoryMusicLoader.Advance503040(phase, null, 0, null, null);
        Assert.Equal(phase, step.Phase);
        Assert.Null(step.Music);
        Assert.Null(step.FileRequest);
        Assert.Null(step.ResourceRows);
        Assert.False(step.StartWorker);
    }

    [Fact]
    public void Native_request_and_signed_result_gate_preserve_lazy_operand_reads()
    {
        NativeBattleVictoryMusicStep request = NativeBattleVictoryMusicLoader.Advance503040(0, null, 0, null, null);
        Assert.Equal(new NativeBattleVictoryFileRequest(766, 0x199B018, 0x508470), request.FileRequest);
        Assert.Equal((byte)1, request.Phase);
        NativeBattleVictoryMusicStep waiting = NativeBattleVictoryMusicLoader.Advance503040(1, int.MinValue, 0, null, null);
        Assert.Equal((byte)1, waiting.Phase);
        Assert.Null(waiting.Music);
        Assert.Throws<NotSupportedException>(() => NativeBattleVictoryMusicLoader.Advance503040(1, null, 0, null, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleVictoryMusicLoader.Advance503040(1, 0, 0, null, null));
        NativeBattleMusicProcessState process = new(new(1, null, null, null, null, null), null, null);
        NativeBattleVictoryMusicStep ready = NativeBattleVictoryMusicLoader.Advance503040(1, int.MaxValue, 0, null, process);
        Assert.Equal((byte)2, ready.Phase);
        Assert.True(ready.Music!.Commands!.Pending);
        Assert.Equal((byte)4, ready.Music.Commands.Command);
        Assert.Equal(3, ready.Music.Commands.Duration);
        Assert.Null(ready.Music.Commands.TargetGain);
    }
}
