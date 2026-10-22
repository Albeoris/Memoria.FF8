using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleHypnotizeBeamResourcesTests
{
    [Fact]
    public void Original_Hypnotize_retains_all_seventeen_ACTs_and_exact_anchor_table()
    {
        using GameAssets game = Open();
        NativeBattleHypnotize133Resources resources = game.ReadNativeHypnotize133Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeHypnotize133Resources());
        Assert.Equal("FF8_EN:1212A30", resources.Camera.Identity);
        Assert.Equal(0xAC, resources.Camera.Data.Length);
        Assert.Equal(0x64286u, resources.Sound);
        Assert.Equal(0x1A0, resources.Sprite.Bytes.Length);
        Assert.Equal(8, resources.AnchoredOffsets.Length);
        CheckAct(resources.MainAct, 0xA8, 30);
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(0x1208C30u + (uint)(index * 0xCB4), resources.AnchoredActs[index].Address);
            Assert.Equal(0x120F1D0u + (uint)(index * 0x70C), resources.FadingActs[index].Address);
            CheckAct(resources.AnchoredActs[index], 0x9C, 9);
            CheckAct(resources.FadingActs[index], 0x40, 10);
            Assert.Same(resources.AnchoredActs[index], resources.Act(resources.AnchoredActs[index].Address));
        }
        Assert.Throws<InvalidDataException>(() => (resources with { AnchoredActs = resources.AnchoredActs.Take(7).ToImmutableArray() }).Validate());
        Assert.Throws<NotSupportedException>(() => resources.Act(0x12038DC));
        NativeBattleHypnotize133Resources restored = JsonSerializer.Deserialize<NativeBattleHypnotize133Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }
    [Fact]
    public void Original_Beam_Laser_retains_five_ACTs_static_mesh_and_distinct_sprites()
    {
        using GameAssets game = Open();
        NativeBattleBeamLaser134Resources resources = game.ReadNativeBeamLaser134Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeBeamLaser134Resources());
        Assert.Equal("FF8_EN:1207680", resources.Camera.Identity);
        Assert.Equal(0x128, resources.Camera.Data.Length);
        Assert.Equal(0x61B82u, resources.Sound);
        Assert.Equal(0x1202214u, resources.Mesh.Address);
        Assert.Equal(0x16C8, resources.Mesh.Bytes.Length);
        Assert.Equal(0x1201EBCu, resources.SpriteA.Address);
        Assert.Equal(0x1202068u, resources.SpriteB.Address);
        int[] caches = [0x58, 0x1B0, 0x220, 0xA4, 0x58];
        int[] durations = [18, 24, 22, 22, 10];
        for (int index = 0; index < resources.Acts.Length; index++)
        {
            CheckAct(resources.Acts[index], caches[index], durations[index]);
            Assert.Same(resources.Acts[index], resources.Act(resources.Acts[index].Address));
        }
        Assert.Throws<InvalidDataException>(() => (resources with { Acts = resources.Acts.Take(4).ToImmutableArray() }).Validate());
        Assert.Throws<NotSupportedException>(() => resources.Act(0x1207958));
        NativeBattleBeamLaser134Resources restored = JsonSerializer.Deserialize<NativeBattleBeamLaser134Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }
    private static void CheckAct(NativeBattleGfAnimationSource source, int cacheSize, int duration)
    {
        NativeBattleGfAnimationWorkspace workspace = new(new byte[cacheSize].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, cacheSize);
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) => current);
            Assert.True(step.EmittedNodes > 0);
            workspace = step.Workspace;
        }
        Assert.Equal(duration, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(source, 0, (current, node) => current).EmittedNodes);
        Assert.Equal(source.Address, workspace.ResourceAddress);
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for Hypnotize and Beam Laser.");
        return new(directory!);
    }
}
