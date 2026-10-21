using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageSequenceTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(255)]
    public void StageControlConsumesOneUnsignedByteThenResumesTheCurrentSequence(byte command)
    {
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(1, 0, 4, 0, 0x83, command, 0xAA, 0xA1)) { AnimationFrames = [1] };
        NativeBattleGraphicsModel model = new(3, 3) { Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = 1, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
        List<byte> commands = [];
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null) { StageControl = commands.Add });
        Assert.Equal(command, Assert.Single(commands));
        Assert.Single(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
    }

    [Fact]
    public void StageControlRequiresTheInitializerOwnerWhenReached()
    {
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(1, 0, 4, 0, 0x83, 100, 0xAA, 0xA1)) { AnimationFrames = [1] };
        NativeBattleGraphicsModel model = new(3, 3) { Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = 1, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(model, resource, new(null, null)));
    }
}
