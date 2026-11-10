using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Media;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7BattleGpuTests
{
    [Fact]
    public void InstalledArenaFamiliesRasterizeOriginalTexturesAndActors()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7BattlePlacementTables placement = Ff7PcExecutableData.LoadBattlePlacement(installation);
        int[] selection = [300, 308, 324, 305, 333, 336, 341, 328, 364, 392, 344];
        foreach (int id in selection)
        {
            SceneFormation formation = formations[id];
            Ff7BattleRenderer renderer = new(archive, formation.LocationId);
            SceneBattleData scene = SceneBattleData.Read(formation);
            Ff7BattleActorVisual[] enemies = scene.Enemies.Select(enemy => new Ff7BattleActorVisual(
                Ff7BattleRenderer.EnemyModelPrefix(enemy.Placement.EnemyId),
                enemy.Placement.Position,
                Ff7BattlePlacementTables.EnemyYaw(formation.Layout, enemy.Placement.Position) * MathF.Tau / 4096,
                0, 0, enemy.Placement.EnemyId == 16 ? 0 : null)).ToArray();
            Ff7BattlePlacement cloud = placement.Party(formation.Layout, 1, 0, 0, false);
            List<(int Colors, int ChangedPixels)> rasterized = [];
            foreach (SceneCameraPlacement camera in scene.Cameras.Distinct())
            {
                renderer.Render(gpu, 320, 240, new(camera, []));
                Color[] stage = GpuTestDevice.Read(gpu.Target);
                renderer.Render(gpu, 320, 240, new(camera, [.. enemies, new("rt", cloud.Position, cloud.Yaw * MathF.Tau / 4096, 0, 0, 0)]));
                Color[] posed = GpuTestDevice.Read(gpu.Target);
                rasterized.Add((stage.Select(pixel => pixel.PackedValue).Distinct().Count(), stage.Zip(posed).Count(pair => pair.First.PackedValue != pair.Second.PackedValue)));
            }
            Assert.True(rasterized.Any(result => result is { Colors: > 16, ChangedPixels: > 16 }), $"Formation {id} stage colors and actor pixels by camera were {string.Join(", ", rasterized)}.");
        }
    }

    [Fact]
    public void OpeningFieldRequestDrawsOriginalBattleThroughSceneHost()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), MovieSource, audioEnabled: false);
        using SpriteBatch batch = new(device.Device);
        using RenderTarget2D output = device.CreateTarget(320, 240);
        scene.StartNewGame();
        Ff7Session session = Assert.IsType<Ff7Session>(scene.Session);
        session.SetFullCombatEnabled(true);
        for (int tick = 0; tick < 4500 && session.Battle is null; tick++)
        {
            if (session.Host.Process.Movie is { Playing: true } movie)
            {
                session.Host.PublishMovieFrame(Math.Min(movie.Frame + tick % 2, 1799), movie.Frame >= 1799);
            }
            if (tick % 30 == 0)
            {
                foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
                {
                    session.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
                }
            }
            if (tick >= 4110 && !session.Host.Process.PlayerLocked)
            {
                session.MovePlayer(0, -1, true);
            }
            session.AdvanceTick();
        }
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        Assert.Equal(300, battle.Formation.FormationId);
        Color[] first = Draw();
        Assert.True(first.Take(320 * 150).Select(pixel => pixel.PackedValue).Distinct().Count() > 16);
        for (int tick = 0; tick < 7; tick++)
        {
            session.AdvanceTick();
        }
        Color[] next = Draw();
        Assert.True(first.Take(320 * 150).Zip(next).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16);

        long beforeDebug = battle.Tick;
        Press(Keys.F2);
        Assert.True(scene.IsDebugMenuOpen);
        Press(Keys.Up);
        Press(Keys.Enter);
        Assert.False(session.Progress.RandomEncountersEnabled);
        Press(Keys.Up);
        Press(Keys.Enter);
        Assert.False(session.Progress.FullCombatEnabled);
        Ff7SessionSave saved = session.Capture();
        Assert.False(saved.Progress.RandomEncountersEnabled);
        Assert.False(saved.Progress.FullCombatEnabled);
        Assert.NotNull(saved.Battle);
        Assert.Same(battle, session.Battle);
        Assert.Equal(beforeDebug, battle.Tick);
        Press(Keys.F2);
        Assert.False(scene.IsDebugMenuOpen);
        Assert.Same(battle, session.Battle);
        Assert.Equal(Ff7BattleOutcome.Fighting, battle.Outcome);
        session.AdvanceTick();
        Assert.True(battle.Tick > beforeDebug);

        void Press(Keys key)
        {
            GameTime inputTick = new(TimeSpan.Zero, TimeSpan.Zero);
            scene.Update(inputTick, new KeyboardState());
            scene.Update(inputTick, new KeyboardState(key));
            Assert.Null(scene.Error);
        }

        Ff7MovieSource MovieSource(int id)
        {
            Ff7MovieResources resource = Ff7MovieResources.Load(installation, id);
            Ff7MovieCameraTrack? track = resource.Cameras is { } cameras ? new(cameras) : null;
            return new(resource.Path, new MovieInformation(320, 240, 15, 120, 1800), 1800, 15)
            {
                CameraRecordCount = resource.Cameras?.Count ?? 0,
                SnapshotCameraFrame = track is null ? null : (frame, words) => track.SnapshotAt(frame, words)
            };
        }

        Color[] Draw()
        {
            long tick = battle.Tick;
            device.Device.SetRenderTarget(output);
            device.Device.Clear(Color.Black);
            scene.Draw(batch, new Rectangle(0, 0, output.Width, output.Height));
            Assert.Null(scene.Error);
            Assert.Equal(tick, battle.Tick);
            return GpuTestDevice.Read(output);
        }
    }

    [Fact]
    public void OriginalFireBillboardsFollowTheirNativeSpawnAndSpriteFrames()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneBattleData scene = SceneBattleData.Read(formations[300]);
        SceneCameraPlacement camera = scene.Cameras[0];
        SceneBattleEnemy enemy = scene.Enemies[0];
        byte[] Read(string name) => battle.ReadEntry(Assert.Single(battle.Find(name)));
        BattleModelData model = BattleModelData.Read(Read("aqaa"), Read("aqda"));
        BattleClipFrame idle = BattleClipDecoder.Decode(model.BodyClips[0])[0];
        int radius = BattleActionBank.Read(Read("aqab")).CollisionRadius;
        Ff7BattleRenderer renderer = new(battle, formations[300].LocationId, magic);
        Ff7BattleActorVisual target = new("aq", enemy.Placement.Position, MathF.PI, 0, 0, 0);
        renderer.Render(gpu, 320, 240, new(camera, [target]));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);

        Ff7BattleFireState state = Ff7BattleFireEffect.Start(0x10);
        List<int> spawnedBones = [];
        int[] random = [0, 5, 9];
        int[] spriteCounts = [];
        List<Color[]> pictures = [];
        for (int callback = 0; callback < 19; callback++)
        {
            Ff7BattleFireUpdate update = Ff7BattleFireEffect.Advance(
                state, _ => model.Bones.Count + 1, (_, bone) =>
                {
                    spawnedBones.Add(bone);
                    return BattleRigidPose.BonePosition(model, idle, target.Position, target.FacingRadians, bone);
                }, _ => radius, () => random[spawnedBones.Count]);
            Assert.Equal(callback == 0 ? 0x10 : 0, update.ImpactTargets);
            state = update.State;
            if (callback is 0 or 2 or 4 or 14 or 16 or 18)
            {
                spriteCounts = [.. spriteCounts, state.Sprites.Count];
            }
            if (callback is 0 or 7 or 17)
            {
                renderer.Render(gpu, 320, 240, new(camera, [target], state));
                Color[] effect = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(effect).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16);
                pictures.Add(effect);
            }
            if (callback == 7)
            {
                state = JsonSerializer.Deserialize<Ff7BattleFireState>(JsonSerializer.Serialize(state))!;
                Assert.Equal(3, state.Sprites.Count);
                Assert.Equal(new[] { 7, 5, 3 }, state.Sprites.Select(sprite => sprite.Frame));
            }
        }
        Assert.Equal(new[] { 1, 7 % (model.Bones.Count + 1), 17 % (model.Bones.Count + 1) }, spawnedBones);
        Assert.Equal(new[] { 1, 2, 3, 2, 1, 0 }, spriteCounts);
        Assert.True(state.Completed);
        Assert.True(pictures[0].Zip(pictures[1]).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16);
    }

    [Fact]
    public void OriginalIceCrystalGrowsAndFadesUsingItsNativeModel()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneBattleData scene = SceneBattleData.Read(formations[300]);
        SceneBattleEnemy enemy = scene.Enemies[0];
        byte[] Read(string name) => battle.ReadEntry(Assert.Single(battle.Find(name)));
        BattleModelData model = BattleModelData.Read(Read("aqaa"), Read("aqda"));
        BattleActionBank bank = BattleActionBank.Read(Read("aqab"));
        BattleClipFrame idle = BattleClipDecoder.Decode(model.BodyClips[0])[0];
        Ff7BattleRenderer renderer = new(battle, formations[300].LocationId, magic);
        Ff7BattleActorVisual target = new("aq", enemy.Placement.Position, MathF.PI, 0, 0, 0);
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);

        Ff7BattleIceState state = Ff7BattleIceEffect.Start(0x10);
        List<int> visiblePixels = [];
        for (int frame = 0; frame <= Ff7BattleIceEffect.FrameCount; frame++)
        {
            Ff7BattleIceUpdate update = Ff7BattleIceEffect.Advance(state, _ => BattleRigidPose.BonePosition(model, idle, target.Position, target.FacingRadians, bank.JointSelectors.Span[0]), _ => bank.CollisionRadius);
            Assert.Equal(frame == 0 ? 0x10 : 0, update.ImpactTargets);
            state = update.State;
            Assert.Equal(frame, state.Frame);
            if (frame is 0 or 7 or 14 or 15)
            {
                renderer.Render(gpu, 320, 240, new(scene.Cameras[0], [], Ice: state));
                visiblePixels.Add(baseline.Zip(GpuTestDevice.Read(gpu.Target)).Count(pair => pair.First.PackedValue != pair.Second.PackedValue));
            }
        }
        Assert.Equal(0, visiblePixels[0]);
        Assert.True(visiblePixels[1] > 0, $"Ice frame pixel changes were {string.Join(", ", visiblePixels)}.");
        Assert.True(visiblePixels[2] < visiblePixels[1]);
        Assert.True(Ff7BattleIceEffect.Scale(14, bank.CollisionRadius) > Ff7BattleIceEffect.Scale(7, bank.CollisionRadius));
        Assert.Equal(0, visiblePixels[3]);
        Assert.True(state.Completed);
    }

    [Fact]
    public void OriginalBoltAndCureRenderTheirDistinctNativeEffects()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneBattleData scene = SceneBattleData.Read(formations[300]);
        Ff7BattleRenderer renderer = new(battle, formations[300].LocationId, magic, Ff7BattleCureResources.Load(installation));
        SceneCameraPlacement camera = scene.Cameras[0];
        var anchor = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -320, 0);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);

        Ff7BattleBoltState bolt = Ff7BattleBoltEffect.Start(0x10);
        Ff7BattleCureState cure = Ff7BattleCureEffect.Start(0x10);
        int random = 5;
        for (int frame = 0; frame < 38; frame++)
        {
            Ff7BattleBoltUpdate boltUpdate = Ff7BattleBoltEffect.Advance(bolt, _ => anchor, _ => 350, NextRandom);
            Ff7BattleCureUpdate cureUpdate = Ff7BattleCureEffect.Advance(cure, _ => 24, (_, _) => anchor, NextRandom);
            bolt = boltUpdate.State;
            cure = cureUpdate.State;
            Assert.Equal(frame == 0 ? 0x10 : 0, boltUpdate.ImpactTargets);
            Assert.Equal(frame == 29 ? 0x10 : 0, cureUpdate.ImpactTargets);
            if (frame == 4)
            {
                Assert.Equal(3, bolt.Sparks!.Count);
                Assert.Equal(10, cure.Particles.Count);
                renderer.Render(gpu, 320, 240, new(camera, [], Bolt: bolt));
                Color[] boltPixels = GpuTestDevice.Read(gpu.Target);
                Assert.True(Changed(boltPixels) > 16);
                renderer.Render(gpu, 320, 240, new(camera, [], Cure: cure));
                Color[] curePixels = GpuTestDevice.Read(gpu.Target);
                Assert.True(Changed(curePixels) > 16);
                Assert.True(boltPixels.Zip(curePixels).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16);
            }
        }
        Assert.True(bolt.Completed);
        Assert.True(cure.Completed);

        int NextRandom()
        {
            random = unchecked(random * 214013 + 2531011);
            return (random >> 16) & 0x7FFF;
        }

        int Changed(Color[] pixels)
        {
            return baseline.Zip(pixels).Count(pair => pair.First.PackedValue != pair.Second.PackedValue);
        }
    }

    private static Ff7Installation Installation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII battle GPU checks.");
        return Ff7Installation.Discover(root!);
    }
}
