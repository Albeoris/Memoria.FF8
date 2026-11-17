using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeDuelInvocationTests
{
    [Fact]
    public void Manual_native_matching_accepts_an_unlearned_finisher_and_uses_its_exact_button_words()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(1);
        BattleNativeDuelChoice finisher = Enumerable.Range(0, rules.DuelMoveSets.Length / 4).SelectMany(set => NativeBattleDuelRules.Choices(rules, (byte)set, 0, assets.NativeActions)).First(choice => choice.Finisher);
        ushort[] ring = new ushort[8];
        byte cursor = 6;
        foreach (ushort button in finisher.Buttons)
        {
            cursor = (byte)((cursor + 1) & 7);
            ring[cursor] = button;
        }

        Assert.False(finisher.Learned);
        Assert.All(finisher.Buttons, button => Assert.Equal(0, button & 0xF00));
        Assert.True(NativeBattleDuelRules.Matches(finisher, ring.ToImmutableArray(), cursor));
        ring[cursor] ^= 0x80;
        Assert.False(NativeBattleDuelRules.Matches(finisher, ring.ToImmutableArray(), cursor));
    }

    [Theory]
    [InlineData(31195u, 32765, false)]
    [InlineData(29360u, 32766, true)]
    [InlineData(27525u, 32767, true)]
    public void Automatic_three_choice_boundary_consumes_the_original_UI_draw(uint seed, int roll, bool invalidStackRead)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(1);
        byte moveSet = Enumerable.Range(0, rules.DuelMoveSets.Length / 4).Select(value => (byte)value).First(id => NativeBattleDuelRules.Choices(rules, id, 0x3FF, assets.NativeActions).Count(choice => choice.Learned) == 3);
        BattleNativeDuelChoice[] choices = NativeBattleDuelRules.Choices(rules, moveSet, 0x3FF, assets.NativeActions).Where(choice => choice.Learned).ToArray();
        NativeBattleUiRandomState random = new(seed);
        NativeBattleUiRandomState expected = random.Draw(out ushort actualRoll);
        Assert.Equal(roll, actualRoll);
        BattleNativeDuelState state = new(moveSet, BattleTargetRequest.None, 400, 279, BattleNativeDuelPhase.Ready) { Selection1D28DEF = 254, WidgetPhase = 4, WidgetRevealQ12 = 4096, AutomaticCountdown = 31 };
        BattleNativeDuelState Advance() => NativeBattleDuelRules.AdvanceWidget(state, true, 0, true, choices, () =>
        {
            random = random.Draw(out ushort draw);
            return draw;
        });
        if (invalidStackRead)
        {
            Assert.Throws<NotSupportedException>(Advance);
        }
        else
        {
            Assert.Equal(choices[2].MoveSetId, Advance().Selection1D28DEF);
        }
        Assert.Equal(expected, random);
    }
}
