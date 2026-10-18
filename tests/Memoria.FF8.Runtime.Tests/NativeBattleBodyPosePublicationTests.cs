using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBodyPosePublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Successful_advance_and_regular_completion_publish_current_locals_before_sequence_services(bool complete)
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0xAA, 0xA1]) { AnimationFrames = [1], PoseSource = source };
        NativeBattleGraphicsModel model = Model(source, complete ? (byte)1 : (byte)0);
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1000, 2000, 3000, 4096));
        int calls = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null)
        {
            ApplyActionContacts = current =>
            {
                calls++;
                Assert.Equal(-48, NativeBattleBodyBoneMatrices.Require(current, source)[0].Y);
                return current;
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal(-48, NativeBattleBodyBoneMatrices.Require(step.Model, source)[0].Y);
    }

    [Fact]
    public void Initial_header_early_exit_does_not_invent_a_pose_publication()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0xA1]) { AnimationFrames = [1], PoseSource = source };
        NativeBattleGraphicsModel model = Model(source, 1) with { ResourceFlags44 = 3 };
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1000, 2000, 3000, 4096));
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Same(model.CurrentBodyBoneMatrices, step.Model.CurrentBodyBoneMatrices);
    }

    private static NativeBattleGraphicsModel Model(NativeBattlePoseSource source, byte frame)
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags2 = 0, Flags8 = 0, Sequence = 1, ResourceFlags44 = 0, SequenceState = new() { Pose = 0, PoseFrame114 = frame, PoseLimit115 = 1, DecodedPose = source.Sample(0, 1, 0) } };
        return NativeBattleBodyBoneMatrices.Publish508C90(model, source);
    }

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        return new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0x80, 2, 0]);
    }
}
