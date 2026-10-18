using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100LoadedModelTests
{
    [Theory]
    [InlineData(4096, 0x99000000u)]
    [InlineData(2048, 0x99014080u)]
    [InlineData(1, 0x99017FFFu)]
    [InlineData(-33, 0x99018001u)]
    public void Recolor_writes_shadowRGB_only_with_signed_truncation_and_BYTE_wrap(short ratio, uint expected)
    {
        NativeBattleModulePoolTask task = Task().WriteDword(0x58, 0x77665544).WriteDword(0x5C, 0x99887766).WriteWord(0x13C, unchecked((ushort)ratio)).WriteByte(0x37, 0xAB);
        NativeBattleModulePoolTask recolored = NativeBattleChoco100LoadedModel.Recolor717770(task, 0x00017FFF);
        Assert.Equal(expected, recolored.ReadDword(0x5C));
        Assert.Equal(0x77665544u, recolored.ReadDword(0x58));
        Assert.Equal((byte)0xAB, recolored.ReadByte(0x37));
        Assert.Equal(task.ReadWord(0x13C), recolored.ReadWord(0x13C));
    }

    [Fact]
    public void Reveal_and_Normal_update_shadow_easing_fields_without_changing_body_scale_or_body_color()
    {
        NativeBattleModulePoolTask task = Task().WriteDword(0x58, 0x12345678).WriteDword(0x5C, 0xAABBCCDD).WriteDword(0x114, 0x2800).WriteDword(0x118, 0x2800).WriteDword(0x11C, 0x2800);
        NativeBattleModulePoolTask revealed = NativeBattleChoco100LoadedModel.Reveal717750(task, 0x00112233);
        Assert.Equal((ushort)0x2400, revealed.ReadWord(0x13E));
        Assert.Equal((ushort)0x1000, revealed.ReadWord(0x13C));
        Assert.Equal(0xAA000000u, revealed.ReadDword(0x5C));
        NativeBattleModulePoolTask normal = NativeBattleChoco100LoadedModel.Normal717910(revealed, 0x00112233);
        Assert.Equal((ushort)0x1000, normal.ReadWord(0x13E));
        Assert.Equal((ushort)0, normal.ReadWord(0x13C));
        Assert.Equal(0xAA112233u, normal.ReadDword(0x5C));
        Assert.Equal(0x12345678u, normal.ReadDword(0x58));
        foreach (int offset in new[] { 0x114, 0x118, 0x11C })
        {
            Assert.Equal(0x2800u, normal.ReadDword(offset));
        }
    }

    [Theory]
    [InlineData(0x1401, 0x201, 0x1400, 0x200)]
    [InlineData(0x1000, 0, 0x1000, 0)]
    [InlineData(0x8000, 0x8000, 0x7F88, 0x7F97)]
    public void Ease_subtracts_with_WORD_wrap_before_signed_clamp_and_honors_exact_no_ease_sentinels(ushort bounds, ushort ratio, ushort expectedBounds, ushort expectedRatio)
    {
        NativeBattleModulePoolTask result = NativeBattleChoco100LoadedModel.Ease717800(Task().WriteWord(0x13E, bounds).WriteWord(0x13C, ratio), 0x00808080);
        Assert.Equal(expectedBounds, result.ReadWord(0x13E));
        Assert.Equal(expectedRatio, result.ReadWord(0x13C));
    }

    [Theory]
    [InlineData(0x100, 0x023C3C3Cu)]
    [InlineData(0x1000, 0x02000000u)]
    public void Reached_type1_fade_halves_default_then_writes_bodyRGB_and_type_without_shadow_or_alpha_changes(ushort ratio, uint expected)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x13A, ratio).WriteDword(0x58, 0x99887766).WriteDword(0x5C, 0xAABBCCDD).WriteByte(0x37, 0x7E);
        NativeBattleModulePoolTask result = NativeBattleChoco100LoadedModel.FadeBodyType1_717A70(task, 0x00808080);
        Assert.Equal(expected, result.ReadDword(0x58));
        Assert.Equal(0xAABBCCDDu, result.ReadDword(0x5C));
        Assert.Equal((byte)0x7E, result.ReadByte(0x37));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100LoadedModel.FadeBodyType1_717A70(task.WriteWord(0x13A, 0), 0x00808080));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100LoadedModel.FadeBodyType1_717A70(task.WriteWord(0x13A, 0x8000), 0x00808080));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, NativeBattleChoco100LoadedModel.Callback, new byte[0x140 - 12].ToImmutableArray());
}
