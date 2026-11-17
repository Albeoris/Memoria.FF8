using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class BattleAnimationPresentationTests
{
    [Fact]
    public void Player_sequence_sections_bind_to_the_original_weapon_or_body()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel squall = assets.LoadPlayer(0);
        BattleSequenceSet sequences = assets.LoadPlayerSequences(0, 0, squall);
        Assert.Null(squall.Sequences);
        Assert.Same(assets.LoadWeapon(0, 0, squall).Sequences, sequences);
        Assert.All(new[] { 3, 4, 13, 17 }, sequence => Assert.True(sequences.Contains(sequence)));
        BattleModel edea = assets.LoadPlayer(7);
        Assert.Same(edea.Sequences, assets.LoadPlayerSequences(7, 29, edea));
        Assert.True(edea.Sequences!.Contains(13));
    }

    [Fact]
    public void Every_installed_character_animation_projects_finite_final_vertices()
    {
        using BattleAssets assets = InstalledAssets();
        foreach (int character in assets.PlayerCharacterIds)
        {
            for (int costume = 0; costume < assets.PlayerModelNames(character).Count; costume++)
            {
                BattleModel body = assets.LoadPlayer(character, costume);
                for (int animation = 0; animation < body.Animations.Count; animation++)
                {
                    Assert.All(body.Pose(animation, body.Animations[animation].Frames.Count - 1, false).SelectMany(mesh => mesh), vertex => Assert.True(float.IsFinite(vertex.X) && float.IsFinite(vertex.Y) && float.IsFinite(vertex.Z)));
                }
            }
        }
    }

    [Fact]
    public void One_shot_animation_holds_its_final_pose_without_interpolating_back_to_its_start()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel model = assets.LoadPlayer(0);
        int end = model.Animations[12].Frames.Count - 1;
        Vector3[] final = model.Pose(12, end, false).SelectMany(mesh => mesh).ToArray();
        Assert.Equal(final, model.Pose(12, end + 0.75f, false).SelectMany(mesh => mesh));
        Assert.Equal(final, model.Pose(12, 10000, false).SelectMany(mesh => mesh));
        Assert.NotEqual(final, model.Pose(12, 0, false).SelectMany(mesh => mesh));
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
