using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAuxiliaryMotionTests
{
    [Fact]
    public void ModelGroupFadeClearsOnlyItsMaskBitAndDrawsFifteenFrames()
    {
        NativeBattleGraphicsModel model = Model(new(0, 0, 0)) with { SequenceState = new() { MeshMask124 = uint.MaxValue } };
        List<byte> frames = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = _ => model,
            WriteModel = (_, value) => model = value,
            DrawModelGroup50F190 = (index, group, frame, color) =>
            {
                Assert.Equal(OriginalOwner(0), index);
                Assert.Equal((byte)33, group);
                Assert.Equal(255u, color);
                frames.Add(frame);
            }
        };
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelGroupFade50F0B0(OriginalOwner(0), 33, OriginalAliases);
        for (int frame = 0; frame < 15; frame++)
        {
            state = state.Advance(inputs).State;
        }
        Assert.Null(state.Head);
        Assert.Equal(Enumerable.Range(0, 15).Select(frame => (byte)frame), frames);
        Assert.Equal(uint.MaxValue & ~2u, model.SequenceState!.MeshMask124);
    }

    [Fact]
    public void StageFadePublishesInclusiveSineEndpointBeforeRelease()
    {
        List<short> values = [];
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendStageFade501F50(2);
        NativeBattleAuxiliaryInputs inputs = new() { WriteStageColorDepth = values.Add };
        state = state.Advance(inputs).State;
        state = state.Advance(inputs).State;
        Assert.NotNull(state.Head);
        state = state.Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal(new short[] { 4096, 2896, 0 }, values);
        Assert.Throws<DivideByZeroException>(() => NativeBattleAuxiliaryState.Clear().AppendStageFade501F50(0).Advance(inputs));
    }

    [Fact]
    public void CameraShakeWritesBeforeIncrementAlternatesAndReleasesWithoutAForcedZero()
    {
        List<short> values = [];
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendCameraShake50F680(20, 4, 4);
        NativeBattleAuxiliaryInputs inputs = new() { WriteCameraTranslationY = values.Add };
        for (int index = 0; index < 4; index++)
        {
            state = state.Advance(inputs).State;
        }
        Assert.Equal(new short[] { 20, -16, 12, -8 }, values);
        Assert.Null(state.Head);
        Assert.Throws<DivideByZeroException>(() => NativeBattleAuxiliaryState.Clear().AppendCameraShake50F680(1, 0, 0).Advance(inputs));
    }

    [Fact]
    public void MovePreincrementsReadsMovingTargetAndCommitsOnlyXZAtCompletion()
    {
        BattleActorModels models = new([new(OriginalOwner(0), Model(new(10, 11, 20))), new(OriginalOwner(1), Model(new(110, 55, 220)))]);
        NativeBattleAuxiliaryInputs inputs = new() { ReadModel = owner => models.Get(owner), WriteModel = (owner, model) => models = models.Set(owner, model) };
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelMotion(0x9E, OriginalOwner(0), OriginalOwner(1), 0, 0, 0, 2, OriginalAliases).State;
        state = state.Advance(inputs).State;
        Assert.Equal(new NativeBattleVector(60, 11, 120), models.Get(OriginalOwner(0)).Transform!.Position28);
        Assert.Equal(new NativeBattleVector(10, 11, 20), models.Get(OriginalOwner(0)).Transform!.Initial20);
        state = state with { };
        NativeBattleGraphicsModel target = models.Get(OriginalOwner(1));
        models = models.Set(OriginalOwner(1), target with { Transform = target.Transform! with { Position28 = new(210, 55, -20) } });
        state = state.Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal(new NativeBattleVector(210, 11, -20), models.Get(OriginalOwner(0)).Transform!.Position28);
        Assert.Equal(models.Get(OriginalOwner(0)).Transform!.Position28, models.Get(OriginalOwner(0)).Transform!.Initial20);
    }

    [Theory]
    [InlineData(0xAD, 0x80, 255, 242, 20)]
    [InlineData(0xAE, 0, 201, 241, 30)]
    [InlineData(0xAD, 255, 255, 242, 100)]
    public void AttachmentUsesExactSelectorsSignedFractionCachedTranslationAndCompletion(byte opcode, byte first, byte second, byte expectedTargetBone, int desiredX)
    {
        BattleActorModels models = new([new(OriginalOwner(0), Model(new(0, 0, 0))), new(OriginalOwner(1), Model(new(100, 200, 300)) with { Flags2 = 0x21 })]);
        List<(BattleModelOwner Model, byte Bone, short Fraction)> reads = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = owner => models.Get(owner),
            WriteModel = (owner, model) => models = models.Set(owner, model),
            ReadBone = (index, bone, fraction) =>
            {
                reads.Add((index, bone, fraction));
                return index == OriginalOwner(1) ? new(5, 6, 7) : bone == 201 ? new(40, 60, 80) : new(20, 40, 60);
            }
        };
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelMotion(opcode, OriginalOwner(0), OriginalOwner(1), first, second, -1234, 1, OriginalAliases).State;
        state = state.Advance(inputs).State;
        Assert.Null(state.Head);
        Assert.Equal((ushort)1, models.Get(OriginalOwner(1)).Flags2);
        Assert.Equal(100 + desiredX - 5, models.Get(OriginalOwner(1)).GeometryWorldMatrix!.X);
        Assert.Equal(new NativeBattleVector(100, 200, 300), models.Get(OriginalOwner(1)).Transform!.Position28);
        Assert.Equal((OriginalOwner(1), expectedTargetBone, (short)2048), reads[^1]);
        if (first != 255)
        {
            Assert.Equal((OriginalOwner(0), (byte)(first & 127), (short)-1234), reads[0]);
        }
    }

    [Fact]
    public void ZeroAttachmentDurationWrapsAndExhaustionDoesNotAllocate()
    {
        NativeBattleGraphicsModel model = Model(new(10, 20, 30)) with { Flags2 = 0x20 };
        NativeBattleAuxiliaryInputs inputs = new() { ReadModel = _ => model, WriteModel = (_, value) => model = value, ReadBone = (_, _, _) => new(0, 0, 0) };
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendModelMotion(0xAD, OriginalOwner(0), OriginalOwner(0), 255, 255, 0, 0, OriginalAliases).State.Advance(inputs).State;
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[0].Payload.AsSpan().Slice(12)));
        Assert.Equal((ushort)0x20, model.Flags2);
        for (int index = 1; index < 16; index++)
        {
            state = state.AppendModelMotion(0x9E, OriginalOwner(0), OriginalOwner(0), 0, 0, 0, 1, OriginalAliases).State;
        }
        Assert.False(state.AppendModelMotion(0xAE, OriginalOwner(0), OriginalOwner(0), 0, 1, 0, 1, OriginalAliases).Allocated);
    }

    private static NativeBattleGraphicsModel Model(NativeBattleVector position)
    {
        return new(3) { Flags2 = 0, Transform = new(position, position, 0, 0, 2048), GeometryWorldMatrix = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), position.X, position.Y, position.Z, 4096) };
    }
}
