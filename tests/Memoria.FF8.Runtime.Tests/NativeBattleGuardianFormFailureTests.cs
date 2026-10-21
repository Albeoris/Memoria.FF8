using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianFormFailureTests
{
    [Fact]
    public void FailedActionReleasesItsTaskButLeavesPriorityCompletionToThePumpAfterRestore()
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear().Enqueue(104, 0xF0, 0x1D280C4).State;
        state = state.Advance((current, packet) => new(current.AppendTask(NativeBattleDescriptorTaskKind.Action104, packet).State, 8), (current, _) => new(current, false));
        Assert.Equal((byte)1, state.Depth);
        int task = Enumerable.Range(0, state.Tasks.Length).Single(index => state.Tasks[index] is { Flags: not 0, Kind: NativeBattleDescriptorTaskKind.Action104 });
        state = state.CompleteFailedGuardianFormAction(task);
        Assert.Equal((byte)255, state.Packets[Assert.Single(state.Queue)].PriorityState);
        Assert.Equal((byte)1, state.Depth);
        Assert.Equal(state.TaskHead, state.TaskTail);
        state = JsonSerializer.Deserialize<NativeBattleDescriptorState>(JsonSerializer.Serialize(state))!;
        state.Validate();
        state = state.Advance((_, _) => throw new InvalidOperationException("Completed action must not be admitted again."), (_, _) => throw new InvalidOperationException("Released action callback must not run again."));
        Assert.Empty(state.Queue);
        Assert.Equal((byte)0, state.Depth);
        state.Validate();
    }

    [Fact]
    public void FailureCannotReleaseTheDescriptorPump()
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        Assert.Throws<InvalidDataException>(() => state.CompleteFailedGuardianFormAction(state.TaskHead!.Value));
        state.Validate();
    }
}
