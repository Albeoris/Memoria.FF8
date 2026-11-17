using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class BattleAnimationPresentationTests
{
    [Fact]
    public void Native_weapon_sequences_select_real_physical_attack_hit_KO_and_victory_clips()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel squall = assets.LoadPlayer(0);
        BattleSequenceSet sequences = assets.LoadPlayerSequences(0, 0, squall);
        Assert.Null(squall.Sequences);
        Assert.Equal(new[] { 33, 35, 32, 34, 39, 40, 41, 36, 37, 38 }, sequences.Read(13).Animations);
        Assert.Equal(new[] { 5 }, sequences.Read(4).Animations);
        Assert.Equal(new[] { 31 }, sequences.Read(17).Animations);
        Assert.Equal(new[] { 13, 12, 13 }, sequences.Read(3).Animations);
        BattleModel edea = assets.LoadPlayer(7);
        Assert.Same(edea.Sequences, assets.LoadPlayerSequences(7, 29, edea));
        Assert.Equal(new[] { 8, 29, 9 }, edea.Sequences!.Read(13).Animations);
    }

    [Fact]
    public void Native_enemy_ability_records_own_the_action_sequence_and_unused_models_cannot_guess_it()
    {
        using BattleAssets assets = InstalledAssets();
        Assert.Equal(14, assets.ReadEnemyAttackSequence(1));
        Assert.Equal(12, assets.ReadEnemyAttackSequence(22));
        Assert.Equal(12, assets.ReadEnemyAttackSequence(61));
        Assert.Equal(12, assets.ReadEnemyAttackSequence(71));
        Assert.Equal(11, assets.ReadEnemyAttackSequence(0));
        Assert.Throws<NotSupportedException>(() => assets.ReadEnemyAttackSequence(127));
        BattleModel ifrit = assets.LoadEnemy(61);
        Assert.Equal(new[] { 8, 9, 10, 14, 11, 12, 13 }, ifrit.Sequences!.Read(assets.ReadEnemyAttackSequence(61)).Animations);
    }

    [Fact]
    public void Every_installed_character_physical_track_references_its_original_rig()
    {
        using BattleAssets assets = InstalledAssets();
        foreach (int character in assets.PlayerCharacterIds)
        {
            for (int costume = 0; costume < assets.PlayerModelNames(character).Count; costume++)
            {
                BattleModel body = assets.LoadPlayer(character, costume);
                BattleSequenceSet sequences = assets.LoadPlayerSequences(character, null, body);
                foreach (int sequence in new[] { 3, 4, 13, 17 })
                {
                    foreach (int animation in sequences.Read(sequence).Animations)
                    {
                        Assert.InRange(animation, 0, body.Animations.Count - 1);
                        Assert.All(body.Pose(animation, body.Animations[animation].Frames.Count - 1, false).SelectMany(mesh => mesh), vertex => Assert.True(float.IsFinite(vertex.X) && float.IsFinite(vertex.Y) && float.IsFinite(vertex.Z)));
                    }
                }
                foreach (int weapon in assets.WeaponModelIds(character))
                {
                    BattleModel held = assets.LoadWeapon(character, weapon, body);
                    foreach (int animation in held.Sequences!.Read(13).Animations)
                    {
                        Assert.InRange(animation, 0, held.Animations.Count - 1);
                        Assert.InRange(animation, 0, body.Animations.Count - 1);
                    }
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

    [Fact]
    public void Sequence_sound_operands_do_not_become_pose_commands_and_uncertain_branches_are_silent()
    {
        BattleSequenceSet sequences = new([1, 0, 4, 0, 0xB8, 3, 2, 0x20, 0xB5, 6, 0, 0xA0, 7, 0xA1, 8, 0xA2]);
        BattleSequence track = sequences.Read(1);
        Assert.Equal(new[] { 7, 8 }, track.Animations);
        Assert.Equal(new BattleSequenceSound(0, 3, false, 0x2000), track.Sounds[0]);
        Assert.Equal(new BattleSequenceSound(0, 6, true, 0), track.Sounds[1]);
        Assert.Empty(new BattleSequenceSet([1, 0, 4, 0, 0x98, 3, 2, 0x20, 0x97, 1, 0, 0xA2]).Read(1).Sounds);
        BattleSequence branched = new BattleSequenceSet([1, 0, 4, 0, 0xE9, 5, 0xB5, 6, 0, 0xA2]).Read(1);
        Assert.True(branched.HasUnresolvedBranches);
        Assert.Empty(branched.Sounds);
        Assert.Throws<InvalidDataException>(() => new BattleSequenceSet([1, 0, 4, 0, 0xB5, 1]).Read(1));
        Assert.Throws<NotSupportedException>(() => new BattleSequenceSet([1, 0, 4, 0, 0xF4, 0]).Read(1));
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
