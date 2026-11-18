using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureAnimationReleaseObservationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Actual_release_is_observed_once_after_both_cleanup_callbacks_and_not_while_sequence_held(int change)
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(OriginalOwner(0), 11, new([255], 0), OriginalAliases);
        NativeBattleAuxiliaryTask original = state.Slots[0];
        NativeBattleGraphicsModel model = new(3) { ResourceFlags44 = 10, Sequence = 11 };
        List<string> operations = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = owner =>
            {
                Assert.Equal(OriginalOwner(0), owner);
                return model;
            },
            ApplyTexture = (_, selector, variant) =>
            {
                Assert.Equal(0, selector);
                Assert.Equal(0, variant);
                operations.Add("texture-reset");
            },
            RestoreTextureBlink = _ => operations.Add("blink-restored"),
            TextureAnimationReleased = (slot, task, observedModel) =>
            {
                Assert.Equal(0, slot);
                Assert.Same(original, task);
                Assert.Same(model, observedModel);
                Assert.Equal(new[] { "texture-reset", "blink-restored" }, operations);
                operations.Add("released");
            }
        };
        state = state.Advance(inputs).State;
        Assert.Empty(operations);
        Assert.Same(original, state.Slots[0]);
        model = change switch
        {
            0 => model with { Flags0 = 1 },
            1 => model with { ResourceFlags44 = 0x40 },
            _ => model with { Sequence = 12 }
        };
        state = state.Advance(inputs).State;
        Assert.Equal(new[] { "texture-reset", "blink-restored", "released" }, operations);
        Assert.Null(state.Head);
        Assert.Equal((ushort)0, state.Slots[0].Flags);
        state = state.Advance(inputs).State;
        Assert.Equal(3, operations.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_native_cleanup_cannot_emit_a_release_witness(bool failBlink)
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(OriginalOwner(0), 11, new([255], 0), OriginalAliases);
        int observations = 0;
        Assert.Throws<InvalidOperationException>(() => state.Advance(new()
        {
            ReadModel = _ => new(3) { ResourceFlags44 = 10, Sequence = 12 },
            ApplyTexture = (_, _, _) =>
            {
                if (!failBlink)
                {
                    throw new InvalidOperationException("Texture reset failed.");
                }
            },
            RestoreTextureBlink = _ => throw new InvalidOperationException("Blink restore failed."),
            TextureAnimationReleased = (_, _, _) => observations++
        }));
        Assert.Equal(0, observations);
        Assert.NotNull(state.Head);
    }
}
