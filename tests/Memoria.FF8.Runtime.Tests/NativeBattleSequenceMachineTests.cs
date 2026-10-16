using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceMachineTests
{
    [Fact]
    public void Linked_weapon_decodes_its_own_frame_limit_without_completing_the_body_pose_early()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (2, "00AAA2")) with { AnimationFrames = [3], LinkedAnimationFrames = [1] };
        NativeBattleGraphicsModel model = NativeBattleSequenceMachine.Advance(Model(2), resource, new(null, null)).Model;
        Assert.Equal((byte)1, model.SequenceState!.LinkedPose!.Frame);
        Assert.Equal((byte)1, model.SequenceState.LinkedPose.Limit);
        NativeBattleSequenceStep next = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Equal((byte)2, next.Model.SequenceState!.PoseFrame114);
        Assert.Equal((byte)1, next.Model.SequenceState.LinkedPose!.Frame);
        Assert.DoesNotContain(next.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(next.Model))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(next.Model, resource, new(null, null))), JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(restored, resource, new(null, null))));
    }

    [Fact]
    public void IfritLocalCounterPublishesTextOnceAndResumesFromTheOwnedCursor()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (14, "C100E500BBA000C300E57FC100CB7FEA0791031EE610C300E57FC11ECB7FEA06E60DE602A1C300C501E500E6DAAAA2"));
        NativeBattleGraphicsModel model = Model(14);
        NativeBattleSequenceStep first = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Single(first.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.Text && cue.First == 3 && cue.Second == 30);
        Assert.DoesNotContain(first.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
        model = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(first.Model))!;
        for (int callback = 1; callback <= 30; callback++)
        {
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
            Assert.DoesNotContain(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.Text);
            Assert.Equal(callback == 30, step.Cues.Any(cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback));
            model = step.Model;
        }
        Assert.Equal((byte)1, model.Sequence);
        Assert.Equal((short)30, model.SequenceState!.Locals[0]);
        Assert.NotEqual(0, model.ResourceFlags44!.Value & 0x40);
    }

    [Fact]
    public void EntrySequenceStartsAppearanceAndCompletesOnlyAfterItsSelectedPose()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (8, "A801A2"));
        NativeBattleGraphicsModel model = Model(8);
        NativeBattleSequenceStep entry = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0));
        Assert.Equal((byte)1, entry.Model.Kind5);
        Assert.Equal((byte)0, entry.Model.Phase6);
        Assert.Equal((byte)1, entry.Model.Sequence);
        Assert.Equal((byte)1, entry.Model.SequenceState!.PoseFrame114);
        Assert.NotEqual(0, entry.Model.ResourceFlags44!.Value & 0x40);
        byte completion = 0;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, Enumerable.Range(0, 7).Select(index => index == 3 ? entry.Model.CompletionInputs() : new NativeBattleGraphicsModelCompletion(0, 0, 0, null)).ToArray(), 0, ref completion));
    }

    [Fact]
    public void PoseWaitUsesOriginalFrameCountAndDoesNotTreatAtbPauseAsACompletedPose()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (2, "00AAA2")) with { AnimationFrames = ImmutableArray.Create<byte>(3) };
        NativeBattleGraphicsModel model = Model(2);
        for (int callback = 0; callback < 3; callback++)
        {
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
            Assert.DoesNotContain(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
            model = step.Model;
        }
        NativeBattleSequenceStep ended = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Single(ended.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
    }

    [Fact]
    public void NativeExpressionWordStoresAndTemporaryDwordWidthsRemainDistinct()
    {
        NativeBattleGraphicsResource resource = Resource((1, "C0FF7FC501E500E580C380D102E501A1E6FF"));
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null));
        Assert.Equal(short.MinValue, step.Model.SequenceState!.Locals[0]);
        Assert.Equal((short)16384, step.Model.SequenceState.Locals[1]);
    }

    [Fact]
    public void ReachedEntryPredicatesRequireTheirActualOwners()
    {
        NativeBattleGraphicsResource resource = Resource((1, "C322E500A1E6FF"));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null)));
        Assert.Equal((short)4, NativeBattleSequenceMachine.Advance(Model(1), resource, new(4, null)).Model.SequenceState!.Locals[0]);
    }

    private static NativeBattleGraphicsModel Model(byte sequence) => new(3, 3) { Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = sequence, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };

    private static NativeBattleGraphicsResource Resource(params (int Id, string Hex)[] programs)
    {
        int count = programs.Max(program => program.Id);
        byte[] bytes = new byte[2 + count * 2 + programs.Sum(program => program.Hex.Length / 2)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)count);
        int cursor = 2 + count * 2;
        foreach ((int id, string hex) in programs)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(id * 2), (ushort)cursor);
            byte[] program = Convert.FromHexString(hex);
            program.CopyTo(bytes, cursor);
            cursor += program.Length;
        }
        return new(bytes.ToImmutableArray()) { AnimationFrames = ImmutableArray.Create<byte>(1) };
    }
}
