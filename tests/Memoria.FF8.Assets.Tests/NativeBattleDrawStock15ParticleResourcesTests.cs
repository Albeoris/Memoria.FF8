using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDrawStock15ParticleResourcesTests
{
    [Fact]
    public void Original_DrawStock_projected_sprites_keep_kind6_and_kind0_complete_six_frame_payloads()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Draw Stock particle sprites.");
        using GameAssets game = new(directory!);
        NativeBattleDrawStock15ParticleResources resources = game.ReadNativeDrawStock15ParticleResources();
        resources.Validate();
        Assert.Equal(528, resources.Trajectory.Length);
        Assert.Equal(288, resources.Sparkle.Length);
        Assert.Equal((byte)6, resources.Trajectory[3]);
        Assert.Equal((byte)0, resources.Sparkle[3]);
        for (int phase = 0; phase < 6; phase++)
        {
            Assert.Equal(80, resources.Resource(0x162B520).ProjectedFrame571C80(phase).Quads.Length);
            Assert.Equal(40, resources.Resource(0x162B730).ProjectedFrame571C80(phase).Quads.Length);
        }
        Assert.Throws<InvalidDataException>(() => resources.Resource(0x162B520).SpriteFrame(0));
        Assert.Throws<InvalidDataException>(() => resources.Resource(0x162B730).ProjectedFrame571C80(6));
    }
}

