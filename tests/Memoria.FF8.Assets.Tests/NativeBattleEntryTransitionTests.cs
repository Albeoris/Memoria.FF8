using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEntryTransitionTests
{
    private static readonly Lazy<NativeBattleEntryTransitionResources> Resources = new(() => NativeBattleEntryTransitionResourcesReader.Read(GameDataResources.ReadNativeExecutable(TestResources.GameDirectory)));

    [Fact]
    public void Original_selector_and_coefficients_come_from_the_authenticated_executable()
    {
        NativeBattleEntryTransitionResources resources = Resources.Value;
        Assert.Equal(43, resources.SpecialEncounters.Length);
        Assert.Contains((ushort)94, resources.SpecialEncounters);
        Assert.DoesNotContain((ushort)0, resources.SpecialEncounters);
        using GameAssets assets = new(TestResources.GameDirectory);
        Assert.Same(assets.ReadNativeBattleEntryTransitionResources(), assets.ReadNativeBattleEntryTransitionResources());
        Assert.Equal(resources.Constants, assets.ReadNativeBattleEntryTransitionResources().Constants);
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryTransitionResourcesReader.Read(new byte[128]));
    }

    [Fact]
    public void Special_entry_draws_four_additive_copies_and_consumes_no_crt_values()
    {
        int draws = 0;
        NativeBattleEntryTransition transition = new(Resources.Value, 94, 640, 448, () => ++draws);
        NativeBattleEntryTransitionFrame first = transition.Current;
        Assert.True(transition.IsSpecial);
        Assert.False(first.CopyCapture);
        Assert.False(first.Complete);
        Assert.Equal(4, first.Quads.Length);
        Assert.All(first.Quads, quad => Assert.Equal(new NativeBattleEntryQuad(0, 0, 640, 448, 64), quad));
        Assert.Empty(first.WipeRows);
        Assert.Equal(0, draws);
        Assert.True(first.Quads.AsSpan().SequenceEqual(transition.Current.Quads.AsSpan()));
    }

    [Fact]
    public void Special_midpoint_matches_the_original_four_quad_positions_and_submission_order()
    {
        NativeBattleEntryTransitionFrame frame = new NativeBattleEntryTransition(Resources.Value, 94, 640, 448).Sample(40);
        Assert.Equal(4, frame.Quads.Length);
        Assert.All(frame.Quads, quad =>
        {
            Assert.Equal(800f, quad.Width);
            Assert.Equal(560f, quad.Height);
            Assert.Equal((byte)159, quad.Gray);
        });
        Assert.Equal(-133.333328f, frame.Quads[0].X, 4);
        Assert.Equal(-93.333336f, frame.Quads[0].Y, 4);
        Assert.Equal(-26.666668f, frame.Quads[1].X, 4);
        Assert.Equal(frame.Quads[0].Y, frame.Quads[1].Y);
        Assert.Equal(frame.Quads[0].X, frame.Quads[2].X);
        Assert.Equal(-18.666666f, frame.Quads[2].Y, 4);
        Assert.Equal(frame.Quads[1].X, frame.Quads[3].X);
        Assert.Equal(frame.Quads[2].Y, frame.Quads[3].Y);
    }

    [Fact]
    public void Special_white_clear_and_two_black_tail_callbacks_precede_battle_entry()
    {
        NativeBattleEntryTransition transition = new(Resources.Value, 94, 640, 448);
        Assert.Equal(0, transition.Sample(60).ClearGray);
        Assert.Equal(0.5f, transition.Sample(70).ClearGray);
        Assert.Equal(1, transition.Sample(80).ClearGray);
        Assert.Equal(4, transition.Sample(80).Quads.Length);
        Assert.Equal((byte)254, transition.Sample(80).Quads[0].Gray);
        Assert.Empty(transition.Sample(81).Quads);
        Assert.Equal(0, transition.Sample(81).ClearGray);
        Assert.False(transition.Sample(81).Complete);
        Assert.True(transition.Sample(82).Complete);
        for (int counter = 1; counter <= transition.CompletionCounter; counter++)
        {
            Assert.Equal(counter, transition.Advance().Counter);
        }
        Assert.Equal(82, transition.Advance().Counter);
        Assert.Equal(33, NativeBattleEntryTransition.CallbackMilliseconds);
    }

    [Fact]
    public void Generic_initialization_consumes_the_crt_profile_once_before_all_pure_samples()
    {
        NativeCrtRandomState random = new(1234);
        int draws = 0;
        NativeBattleEntryTransition transition = new(Resources.Value, 0, 320, 224, Draw);
        Assert.False(transition.IsSpecial);
        Assert.Equal(224, draws);
        Assert.True(transition.Sample(0).CopyCapture);
        Assert.Single(transition.Sample(20).Quads);
        Assert.Empty(transition.Sample(21).Quads);
        Assert.Equal(224, transition.Sample(21).WipeRows.Length);
        Assert.Equal(224, transition.Sample(70).WipeRows.Length);
        Assert.False(transition.Sample(71).Complete);
        Assert.True(transition.Sample(72).Complete);
        NativeBattleEntryTransitionFrame first = transition.Sample(37);
        NativeBattleEntryTransitionFrame second = transition.Sample(37);
        Assert.True(first.WipeRows.AsSpan().SequenceEqual(second.WipeRows.AsSpan()));
        Assert.Equal(new NativeBattleEntryWipeRow(0, 0, 192), first.WipeRows[0]);
        Assert.Equal(new NativeBattleEntryWipeRow(1, 0, 183), first.WipeRows[1]);
        Assert.Equal(new NativeBattleEntryWipeRow(111, 0, 64), first.WipeRows[111]);
        Assert.Equal(new NativeBattleEntryWipeRow(223, 0, -73), first.WipeRows[223]);
        Assert.Equal(4082739122u, random.Seed);
        Assert.Equal(224, draws);

        int Draw()
        {
            draws++;
            random = random.Draw(out ushort value);
            return value;
        }
    }

    [Fact]
    public void Generic_entry_requires_a_real_crt_owner_and_rejects_non_native_draw_width()
    {
        Assert.Throws<ArgumentNullException>(() => new NativeBattleEntryTransition(Resources.Value, 0, 320, 224));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEntryTransition(Resources.Value, 0, 320, 224, () => 32768));
    }
}
