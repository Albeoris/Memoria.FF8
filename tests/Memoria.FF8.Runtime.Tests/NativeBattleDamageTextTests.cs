using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDamageTextTests
{
    [Theory]
    [InlineData(0, 0, 32, 32)]
    [InlineData(-8, -8, 32, 32)]
    [InlineData(1287, 807, 160, 100)]
    [InlineData(4000, 4000, 288, 152)]
    public void First_projection_uses_signed_eighth_pixels_and_native_screen_clamps(short x, short y, short expectedX, short expectedY)
    {
        Assert.Equal(new(expectedX, expectedY), NativeBattleDamageText.Project5069B0(unchecked((ushort)x) | unchecked((uint)y << 16)));
    }

    [Theory]
    [InlineData(0, new int[] { -12, -16, -17, -17, -16, -12, -13, -13, -12, -12 })]
    [InlineData(256, new int[] { -12, -13, -14, -15, -16, -17, -18, -19, -20, -21 })]
    public void Whole_string_uses_ten_native_offsets_keeps_its_anchor_and_draws_the_release_frame(int flags, int[] offsets)
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear().PrependDamageText(3, (ushort)flags, 123);
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank();
        int projections = 0;
        for (int frame = 0; frame < 10; frame++)
        {
            state = state.Advance(models, _ =>
            {
                projections++;
                return new(160, 100);
            }).State;
            NativeBattleDamageTextDraw draw = Assert.Single(state.TextDraws);
            Assert.Equal(148, draw.X);
            Assert.Equal(100 + offsets[frame], draw.Y);
            Assert.Equal(new byte[] { 57, 58, 59, 0 }, draw.Glyphs);
            uint color = flags == 0 ? 0x808080u : 0x408040u;
            Assert.Equal(frame == 9 ? ((color >> 1) & 0x7F7F7F) | 0x04000000u : frame == 8 ? color | 0x04000000u : color, draw.Color);
            state = JsonSerializer.Deserialize<NativeBattleEffectsState>(JsonSerializer.Serialize(state))!;
        }
        Assert.Equal(1, projections);
        Assert.Equal(0, state.PublishedCount);
        Assert.Null(state.Head);
        Assert.Empty(state.Advance(models).State.TextDraws);
    }

    [Fact]
    public void Contact_suppression_and_miss_payload_remain_native()
    {
        Assert.Null(NativeBattleEffectsState.Clear().PrependDamageText(3, 1, 100).Head);
        NativeBattleEffectsState miss = NativeBattleEffectsState.Clear().PrependDamageText(3, 0x400, 0);
        NativeBattleEffectsState step = miss.Advance(NativeBattleGraphicsModels.ClearBank(), _ => new(160, 100)).State;
        Assert.Equal(new byte[] { 16, 0 }, Assert.Single(step.TextDraws).Glyphs);
        Assert.Equal(152, step.TextDraws[0].X);
    }
}
