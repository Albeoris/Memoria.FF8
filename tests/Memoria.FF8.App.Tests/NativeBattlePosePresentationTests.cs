using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePosePresentationTests
{
    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 0.5f)]
    [InlineData(5, 1, 2)]
    [InlineData(2, 2, 1)]
    public void Raw_pose_uses_the_decoded_frame_counter_including_slow_and_fast_modes(byte counter, byte flags, float expected)
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        BattleModel body = assets.LoadPlayer(0);
        BattleAnimationPresentation animation = new(body, null, new() { Id = "squall", IsPlayer = true });
        NativeBattleGraphicsModel row = new(0, 3) { Flags2 = 0, SequenceState = new() { Pose = 0, PoseFrame114 = counter, PoseFlags109 = flags } };

        BattleAnimationSample sample = animation.SampleNativeModel(row);

        Assert.Equal(0, sample.Animation);
        Assert.Equal(expected, sample.Frame);
        Assert.False(sample.Loop);
        Assert.Equal(Vector3.Zero, sample.Offset);
        Assert.Equal(0, animation.SampleNativeModel(row with { Flags0 = 7 }).Opacity);
        Assert.Equal(0, animation.SampleNativeModel(row with { SequenceState = new() }).Opacity);
    }

    [Fact]
    public void Installed_entry_pose_changes_pixels_while_gameplay_waits_and_restore_renders_identically()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors = new[] { 0, 3, 4 }.Select(character => new BattleCombatantDefinition { Id = $"party-{character}", Name = $"Character{character}", IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, InitialAtb = BattleSession.AtbMaximum }).Append(new() { Id = "ifrit", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }).ToArray();
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter), NativeStageCameraBank = stage.NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = stage.NativeCameraControl, Combatants = actors };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        byte[] initial = scene.Render(session, 320, 224).ToArray();
        for (int callback = 0; callback < 4; callback++)
        {
            session.Tick();
        }
        string beforeRender = JsonSerializer.Serialize(session.Capture());

        byte[] current = scene.Render(session, 320, 224).ToArray();

        Assert.False(initial.SequenceEqual(current));
        Assert.Equal(0, session.Frame);
        Assert.Equal(beforeRender, JsonSerializer.Serialize(session.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(beforeRender)!);
        Assert.Equal(current, new BattleScenePresentation(assets, definition, true).Render(restored, 320, 224));
    }
}
