using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

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
    public void Attack_changes_skeleton_without_mutating_or_replaying_the_session()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel model = assets.LoadPlayer(0);
        BattleCombatantDefinition definition = new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, Attack = 80 };
        BattleSession session = new(new BattleDefinition { StageId = 0, Combatants = [definition, new() { Id = "dummy", Name = "Dummy", ModelId = -1, MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }] }, BattleAtbMode.Normal);
        BattleAnimationPresentation animation = new(model, assets.LoadPlayerSequences(0, 0, model), definition) { Melee = true };
        BattleAnimationSample idle = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20);
        session.QueueAttack("party", "dummy");
        session.Tick();
        BattleAnimationSample attack = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20);
        Assert.NotEqual(idle.Animation, attack.Animation);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames; frame++)
        {
            session.Tick();
        }
        string before = JsonSerializer.Serialize(session.Capture());
        IReadOnlyList<BattleSequenceSoundCue> cues = animation.CollectSounds(session);
        Assert.Empty(cues);
        Assert.True(assets.LoadPlayerSequences(0, 0, model).Read(13).HasUnresolvedBranches);
        Assert.Equal(cues, animation.CollectSounds(session));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void Branchless_native_KO_sounds_become_due_while_paused_with_stable_unique_keys()
    {
        using BattleAssets assets = InstalledAssets();
        BattleCombatantDefinition party = new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, Attack = 80, InitialAtb = BattleSession.AtbMaximum };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Bite Bug", ModelId = 1, MaximumHp = 1, CurrentHp = 1, Speed = 1 };
        BattleSession session = new(new BattleDefinition { Combatants = [party, enemy] }, BattleAtbMode.Normal);
        BattleModel model = assets.LoadEnemy(1);
        BattleAnimationPresentation animation = new(model, model.Sequences, enemy) { AttackSequence = assets.ReadEnemyAttackSequence(1) };
        session.QueueAttack("party", "enemy");
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            session.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
        Assert.Empty(animation.CollectSounds(session));
        animation.Advance(TimeSpan.FromSeconds(3));
        string before = JsonSerializer.Serialize(session.Capture());
        IReadOnlyList<BattleSequenceSoundCue> cues = animation.CollectSounds(session);
        Assert.Equal(3, cues.Count);
        Assert.Equal(cues.Count, cues.Select(cue => (cue.ActorId, cue.ActionFrame, cue.CueIndex)).Distinct().Count());
        Assert.Equal(cues, animation.CollectSounds(session));
        Assert.Contains(cues, cue => cue.ActorSound && cue.SoundId == 1 && cue.Attribute == 1024);
        Assert.Contains(cues, cue => !cue.ActorSound && cue.SoundId == 13 && cue.Attribute == 2048);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
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

    [Fact]
    public void KO_and_victory_complete_on_the_presentation_clock_while_results_pause_combat()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel model = assets.LoadPlayer(0);
        BattleCombatantDefinition party = new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, InitialAtb = BattleSession.AtbMaximum, Attack = 100, MaximumHp = 100, CurrentHp = 100 };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Dummy", ModelId = -1, MaximumHp = 1, CurrentHp = 1, Speed = 1 };
        BattleSession victory = new(new BattleDefinition { Combatants = [party, enemy] }, BattleAtbMode.Normal);
        BattleAnimationPresentation winner = new(model, assets.LoadPlayerSequences(0, 0, model), party);
        victory.QueueAttack("party", "enemy");
        for (int count = 0; count <= BattleSession.AttackWindupFrames; count++)
        {
            victory.Tick();
        }
        Assert.Equal(BattleOutcome.Victory, victory.Outcome);
        long frozenFrame = victory.Frame;
        winner.Sample(victory, victory.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        winner.Advance(TimeSpan.FromSeconds(3));
        BattleAnimationSample win = winner.Sample(victory, victory.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        Assert.Equal(31, win.Animation);
        Assert.False(win.Loop);
        Assert.Equal(frozenFrame, victory.Frame);

        BattleSession defeat = new(new BattleDefinition { Combatants = [party with { CurrentHp = 1, InitialAtb = 0 }, enemy with { MaximumHp = 1000, CurrentHp = 1000, Attack = 255, InitialAtb = BattleSession.AtbMaximum }] }, BattleAtbMode.Normal);
        BattleAnimationPresentation loser = new(model, assets.LoadPlayerSequences(0, 0, model), party);
        while (defeat.Outcome == BattleOutcome.Active)
        {
            defeat.Tick();
        }
        loser.Sample(defeat, defeat.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        loser.Advance(TimeSpan.FromSeconds(3));
        BattleAnimationSample ko = loser.Sample(defeat, defeat.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        Assert.Equal(13, ko.Animation);
        Assert.Equal(model.Animations[13].Frames.Count - 1, ko.Frame);
        Assert.False(ko.Loop);
    }

    [Fact]
    public void Melee_approach_does_not_apply_the_animation_horizontal_root_travel_twice()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel model = assets.LoadPlayer(0);
        BattleCombatantDefinition definition = new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, InitialAtb = BattleSession.AtbMaximum };
        BattleSession session = new(new BattleDefinition { Combatants = [definition, new() { Id = "dummy", Name = "Dummy", ModelId = -1, Speed = 1 }] }, BattleAtbMode.Normal);
        BattleAnimationPresentation animation = new(model, assets.LoadPlayerSequences(0, 0, model), definition) { Melee = true };
        Vector3 rest = model.Animations[0].Frames[0].Translation;
        session.QueueAttack("party", "dummy");
        for (int count = 0; count < BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames; count++)
        {
            session.Tick();
            BattleAnimationSample sample = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ * 20);
            int current = (int)sample.Frame;
            int next = Math.Min(current + 1, model.Animations[sample.Animation].Frames.Count - 1);
            Vector3 root = Vector3.Lerp(model.Animations[sample.Animation].Frames[current].Translation, model.Animations[sample.Animation].Frames[next].Translation, sample.Frame - current);
            Vector3 relativeRoot = new Vector3(root.X - rest.X, 0, rest.Z - root.Z) * model.Scale + sample.Offset;
            Assert.InRange(relativeRoot.Z, -0.0001f, 14.0001f);
            Assert.InRange(relativeRoot.X, -0.0001f, 0.0001f);
            Assert.Equal(0, sample.Offset.Y);
        }
    }

    [Fact]
    public void Settled_KO_is_visible_above_the_arena_instead_of_below_the_standing_foot_anchor()
    {
        using BattleAssets assets = InstalledAssets();
        BattleModel model = assets.LoadPlayer(0);
        BattleCombatantDefinition definition = new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, CurrentHp = 0 };
        BattleSession session = new(new BattleDefinition { Combatants = [definition, new() { Id = "dummy", Name = "Dummy", ModelId = -1 }] }, BattleAtbMode.Normal);
        BattleAnimationPresentation animation = new(model, assets.LoadPlayerSequences(0, 0, model), definition);
        BattleAnimationSample sample = animation.Sample(session, session.Combatants[0], Vector3.Zero, Vector3.UnitZ);
        float standingOffset = model.Pose(0, 0).SelectMany(mesh => mesh).Min(vertex => vertex.Y);
        float ground = animation.GroundOffset(sample, false, standingOffset);
        Vector3[] vertices = model.Pose(sample.Animation, sample.Frame, false).SelectMany(mesh => mesh).Select(vertex => vertex - Vector3.UnitY * ground).ToArray();
        Assert.InRange(vertices.Min(vertex => vertex.Y), -0.0001f, 0.0001f);
        Assert.True(vertices.Max(vertex => vertex.Y) > 1);
        Assert.True(ground < standingOffset);
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
