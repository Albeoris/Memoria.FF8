using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBoneSpriteModuleTests
{
    [Fact]
    public void Selector4InitializesOnlyItsNativeWordsAndModelPointer()
    {
        NativeBattleModuleState original = RetainedPool();
        NativeBattleModuleState state = original.AppendModelEffect(6, 4);
        Assert.Equal(NativeBattleModuleKind.BoneSpriteSeed573560, state.Slots[0].Kind);
        Assert.Equal((ushort)0, Word(state.Slots[0].Payload, 0));
        Assert.Equal((ushort)0, Word(state.Slots[0].Payload, 2));
        Assert.Equal(0x1D972C0u + 6 * 156u, BinaryPrimitives.ReadUInt32LittleEndian(state.Slots[0].Payload.AsSpan().Slice(20)));
        Assert.Equal(original.Slots[0].Payload.AsSpan().Slice(4, 16).ToArray(), state.Slots[0].Payload.AsSpan().Slice(4, 16).ToArray());
    }

    [Fact]
    public void FirstUnpausedCallbackPublishesBothPositionedSoundsWithoutCrtOrSkeletonReads()
    {
        NativeBattleModuleStep step = NativeBattleModuleState.Clear().AppendModelEffect(2, 4).Advance(new(0)
        {
            ReadModelAnchor = model =>
            {
                Assert.Equal(2, model);
                return new(1, -2, 3, 4);
            }
        });
        Assert.Equal(new ushort[] { 19, 20 }, step.Draws.Select(draw => draw.Sound));
        Assert.All(step.Draws, draw =>
        {
            Assert.Equal(NativeBattleModuleDrawKind.Sound, draw.Kind);
            Assert.Equal(new(1, -2, 3, 4), draw.Anchor);
            Assert.Equal((ushort)0, draw.SoundFlags);
        });
        Assert.Null(step.CrtRandomAfter);
        Assert.Equal((ushort)1, Word(step.State.Slots[0].Payload, 0));
        Assert.Equal(1, step.State.PublishedCount);
    }

    [Fact]
    public void PausedSeedReadsNothingAndPreservesRetainedPayload()
    {
        NativeBattleModuleState state = RetainedPool().AppendModelEffect(0, 4);
        NativeBattleModuleStep step = state.Advance(new(1));
        Assert.Equal(JsonSerializer.Serialize(state with { PublishedCount = 1 }), JsonSerializer.Serialize(step.State));
        Assert.Empty(step.Draws);
        Assert.Null(step.CrtRandomAfter);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)47)]
    [InlineData((byte)255)]
    public void NativeThreeCrtDrawsChooseFractionBoneThenScaleAndChildRunsInSamePass(byte bones)
    {
        NativeCrtRandomState random = new(0xFFFFFFFF);
        NativeCrtRandomState expected = random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort third);
        NativeBattleModuleState state = SeedPhase1(RetainedPool());
        NativeBattleModuleInputs inputs = Inputs(random) with
        {
            ReadModelBoneCount = _ => bones,
            ReadModelBone = (model, bone, fraction) =>
            {
                Assert.Equal(0, model);
                Assert.Equal((byte)(second % bones), bone);
                Assert.Equal((short)(first % 4096), fraction);
                return new(-100, 200, -300);
            }
        };
        NativeBattleModuleStep step = state.Advance(inputs);
        Assert.Equal(expected, step.CrtRandomAfter);
        NativeBattleModuleTask child = step.State.Slots[1];
        Assert.Equal(NativeBattleModuleKind.BoneSprite573640, child.Kind);
        Assert.Equal((ushort)1, Word(NativeBattleStorageSnapshots.Capture(child).Payload, 0));
        Assert.Equal((ushort)0xA5A5, Word(NativeBattleStorageSnapshots.Capture(child).Payload, 10));
        Assert.Equal(state.Slots[1].Payload.AsSpan().Slice(12, 4).ToArray(), NativeBattleStorageSnapshots.Capture(child).Payload.AsSpan().Slice(12, 4).ToArray());
        NativeBattleModuleDraw draw = Assert.Single(step.Draws);
        Assert.Equal(new(-100, 200, -300, unchecked((short)0xA5A5)), draw.Anchor);
        Assert.Equal(third % 1152 + 2560, draw.Scale);
        Assert.Equal(-(draw.Scale >> 3), draw.DepthOffset);
        Assert.Equal((ushort)0x10, draw.SpriteFlags);
        Assert.Equal(new byte[] { 17, 0, 0, 0 }, draw.SpritePaletteOffsets);
        Assert.Equal((ushort)0, draw.Phase);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0xFFFFFFFFu)]
    public void Selector5DoublesOnlyScaleWithTheSameCrtBoneAndRetainedPayload(uint seed)
    {
        NativeCrtRandomState random = new(seed);
        List<(int Model, byte Bone, short Fraction)> points = [];
        NativeBattleModuleInputs inputs = Inputs(random) with
        {
            ReadModelBone = (model, bone, fraction) =>
            {
                points.Add((model, bone, fraction));
                return new(200, -300, 400);
            }
        };
        NativeBattleModuleState ordinary = RetainedPool().AppendModelEffect(0, 4).Advance(inputs).State;
        NativeBattleModuleState large = RetainedPool().AppendModelEffect(0, 5).Advance(inputs).State;
        NativeBattleModuleStep first = ordinary.Advance(inputs);
        NativeBattleModuleStep second = large.Advance(inputs);
        Assert.Equal(2, points.Count);
        Assert.Equal(points[0], points[1]);
        Assert.Equal(first.CrtRandomAfter, second.CrtRandomAfter);
        NativeCrtRandomState expected = random.Draw(out _).Draw(out _).Draw(out ushort scale);
        Assert.Equal(expected, second.CrtRandomAfter);
        Assert.Equal(2 * (scale % 1152) + 0x1400, Word(second.State.Slots[1].Payload, 16));
        Assert.Equal(first.State.Slots[1].Payload.RemoveRange(16, 2).ToArray(), second.State.Slots[1].Payload.RemoveRange(16, 2).ToArray());
        NativeBattleModuleDraw normalDraw = Assert.Single(first.Draws);
        NativeBattleModuleDraw largeDraw = Assert.Single(second.Draws);
        Assert.Equal(2 * normalDraw.Scale, largeDraw.Scale);
        Assert.Equal(-(largeDraw.Scale >> 3), largeDraw.DepthOffset);
        Assert.Equal(normalDraw.Anchor, largeDraw.Anchor);
        Assert.Equal(normalDraw.SpriteFlags, largeDraw.SpriteFlags);
        Assert.Equal(normalDraw.SpritePaletteOffsets.ToArray(), largeDraw.SpritePaletteOffsets.ToArray());
        Assert.Equal((ushort)1, Word(second.State.Slots[1].Payload, 0));
    }

    [Fact]
    public void Selector5PreservesPhaseZeroSoundAndPausedChildContracts()
    {
        NativeBattleModuleState state = RetainedPool().AppendModelEffect(0, 5);
        NativeBattleModuleStep pausedSeed = state.Advance(new(1));
        Assert.Equal(JsonSerializer.Serialize(state with { PublishedCount = 1 }), JsonSerializer.Serialize(pausedSeed.State));
        Assert.Empty(pausedSeed.Draws);
        NativeBattleModuleStep seed = state.Advance(Inputs(new(4)));
        Assert.Equal(new ushort[] { 19, 20 }, seed.Draws.Select(draw => draw.Sound));
        Assert.Equal(new NativeCrtRandomState(4), seed.CrtRandomAfter);
        NativeBattleModuleStep child = seed.State.Advance(Inputs(seed.CrtRandomAfter!));
        NativeBattleModuleStep restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(child.State))!.Advance(Inputs(child.CrtRandomAfter!) with { GraphicsFlags = 1 });
        Assert.Equal(JsonSerializer.Serialize(child.State), JsonSerializer.Serialize(restored.State));
        Assert.Equal(child.CrtRandomAfter, restored.CrtRandomAfter);
        Assert.Equal((ushort)1, Assert.Single(restored.Draws).Phase);
    }

    [Fact]
    public void UnknownBoneCountOrCrtAndZeroBoneCountRemainStrictAtTheActualSpawn()
    {
        NativeBattleModuleState state = SeedPhase1(NativeBattleModuleState.Clear());
        Assert.Throws<NotSupportedException>(() => state.Advance(Inputs(new(1)) with { ReadModelBoneCount = null }));
        Assert.Throws<InvalidDataException>(() => state.Advance(Inputs(new(1)) with { ReadModelBoneCount = _ => 0 }));
        Assert.Throws<NotSupportedException>(() => state.Advance(Inputs(new(1)) with { CrtRandom = null }));
        Assert.Throws<NotSupportedException>(() => state.Advance(Inputs(new(1)) with { ReadModelBone = null }));
        Assert.Throws<NotSupportedException>(() => state.Advance(Inputs(new(1)) with { Resources = [] }));
    }

    [Theory]
    [InlineData((short)-1, (short)0, false)]
    [InlineData((short)26, (short)27, true)]
    [InlineData(short.MaxValue, short.MinValue, false)]
    public void SeedUsesSignedWordPhaseAndWrapsBeforeItsReleaseComparison(short phase, short expected, bool released)
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 4);
        byte[] payload = state.Slots[0].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, phase);
        state = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Payload = payload.ToImmutableArray() }) };
        NativeBattleModuleStep step = state.Advance(new(0));
        Assert.Equal(unchecked((ushort)expected), Word(step.State.Slots[0].Payload, 0));
        Assert.Equal(released, step.State.Head is null);
        Assert.Empty(step.Draws);
    }

    [Fact]
    public void PausedChildDrawsCurrentFrameWithoutCounterOrCrtMutation()
    {
        NativeBattleModuleStep active = SeedPhase1(NativeBattleModuleState.Clear()).Advance(Inputs(new(9)));
        NativeBattleModuleStep paused = active.State.Advance(Inputs(active.CrtRandomAfter!) with { GraphicsFlags = 1 });
        Assert.Equal(JsonSerializer.Serialize(active.State), JsonSerializer.Serialize(paused.State));
        Assert.Equal(active.CrtRandomAfter, paused.CrtRandomAfter);
        Assert.Equal((ushort)1, Assert.Single(paused.Draws).Phase);
        Assert.DoesNotContain(paused.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Sound);
    }

    [Fact]
    public void NegativeNextOffsetReleasesOnlyAfterDrawingTheFinalFrame()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        (state, int slot) = state.Append(NativeBattleModuleKind.BoneSprite573640);
        byte[] payload = state.Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16), 3000);
        state = state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
        NativeBattleModuleStep step = state.Advance(new(0) { Resources = [Sprite()] });
        Assert.Equal((ushort)1, Assert.Single(step.Draws).Phase);
        Assert.Equal((ushort)2, Word(step.State.Slots[slot].Payload, 0));
        Assert.Null(step.State.Head);
        Assert.Equal(0, step.State.PublishedCount);
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    public void CompletePoolLifetimeAndJsonResumePreserveAll75CrtDraws(byte selector)
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, selector);
        NativeCrtRandomState random = new(12);
        int frames = 0;
        int children = 0;
        while (state.Head is not null && frames < 40)
        {
            NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleStep step = state.Advance(Inputs(random));
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored.Advance(Inputs(random))));
            children += step.Draws.Count(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite && draw.Phase == 0);
            state = step.State;
            random = step.CrtRandomAfter!;
            frames++;
        }
        Assert.Equal(27, frames);
        Assert.Equal(25, children);
        Assert.Null(state.Head);
        Assert.Equal(0, state.PublishedCount);
        NativeCrtRandomState expected = new(12);
        for (int index = 0; index < 75; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
    }

    private static NativeBattleModuleState SeedPhase1(NativeBattleModuleState state) => state.AppendModelEffect(0, 4).Advance(Inputs(new(1))).State;

    private static NativeBattleModuleInputs Inputs(NativeCrtRandomState random) => new(0) { CrtRandom = random, ReadModelAnchor = _ => new(10, 20, 30, 40), ReadModelBoneCount = _ => 47, ReadModelBone = (_, _, _) => new(100, 200, 300), Resources = [Sprite()] };

    private static NativeBattleModuleState RetainedPool()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        return state with { Slots = state.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), ushort.MaxValue);
        return new(5, 1, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
}
