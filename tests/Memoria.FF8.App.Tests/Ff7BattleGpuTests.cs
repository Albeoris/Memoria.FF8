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
        for (int frame = 0; frame < 39; frame++)
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

    [Fact]
    public void OriginalEarlyConsumablesRenderTheirNativePalettesAndHitmark()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic, Ff7BattleCureResources.Load(installation), Ff7BattleGrenadeResources.Load(installation));
        SceneCameraPlacement camera = scene.Cameras[0];
        var anchor = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -450, 0);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        List<Color[]> variants = [];

        foreach (Ff7BattleCureKind kind in Enum.GetValues<Ff7BattleCureKind>())
        {
            Ff7BattleCureState effect = Ff7BattleCureEffect.Start(0x10, kind);
            int random = 5;
            for (int frame = 0; frame <= 38; frame++)
            {
                Ff7BattleCureUpdate update = Ff7BattleCureEffect.Advance(effect, _ => 24, (_, _) => anchor, NextRandom);
                effect = update.State;
                Assert.Equal(frame == (kind == Ff7BattleCureKind.Antidote ? 15 : 29) ? 0x10 : 0, update.ImpactTargets);
                if (frame == 4)
                {
                    renderer.Render(gpu, 320, 240, new(camera, [], Cure: effect));
                    Color[] image = GpuTestDevice.Read(gpu.Target);
                    Assert.True(Changed(baseline, image) > 16, $"No original {kind} pixels.");
                    Assert.All(variants, previous => Assert.True(Changed(previous, image) > 16));
                    variants.Add(image);
                    effect = System.Text.Json.JsonSerializer.Deserialize<Ff7BattleCureState>(System.Text.Json.JsonSerializer.Serialize(effect))!;
                    Assert.Equal(kind, effect.Kind);
                }
            }
            Assert.True(effect.Completed);

            int NextRandom()
            {
                random = unchecked(random * 214013 + 2531011);
                return (random >> 16) & 0x7FFF;
            }
        }

        Ff7BattleHitmarkDefinition grenade = Ff7PcExecutableData.LoadBattleHitmarks(installation)[0x2C];
        Assert.Equal(new Ff7BattleHitmarkCue(0, 1, 0, 0, 24576, 23, 8), Assert.Single(grenade.Cues));
        Ff7BattleSecondaryState secondary = Ff7BattleSecondaryEffects.ScheduleHitmark(new(), 4, grenade, anchor, 350);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleTargetReactions(new(), 0x90, 1);
        Ff7BattlePrimaryContext context = new((_, _, _) => anchor, _ => default, _ => 350, () => throw new InvalidOperationException("Grenade does not consume CRT draws."));
        for (int frame = 0; frame <= 8; frame++)
        {
            secondary = Ff7BattleSecondaryEffects.Advance(secondary, (_, _, _) => anchor, _ => default, context.NextCrt);
            primary = Ff7BattlePrimaryEffects.Advance(primary, context);
            Assert.Equal(frame switch { 1 => new[] { 4 }, 2 => new[] { 7 }, _ => [] }, primary.TargetReactions.Select(reaction => reaction.Target));
            if (frame is 0 or 4)
            {
                renderer.Render(gpu, 320, 240, new(camera, []) { SecondaryEffects = secondary });
                Assert.True(Changed(baseline, GpuTestDevice.Read(gpu.Target)) > 16, $"No original Grenade pixels at callback {frame}.");
                secondary = System.Text.Json.JsonSerializer.Deserialize<Ff7BattleSecondaryState>(System.Text.Json.JsonSerializer.Serialize(secondary))!;
                Assert.Equal((short)anchor.X, Assert.Single(secondary.Hitmarks).Anchor!.X);
            }
        }
        Assert.Empty(secondary.Hitmarks);
        Assert.False(primary.HasCallbacks);

        static int Changed(Color[] a, Color[] b)
        {
            return a.Zip(b).Count(pair => pair.First.PackedValue != pair.Second.PackedValue);
        }
    }

    [Fact]
    public void OriginalSmogShowsItsProjectileAndExpandingCloudFamilies()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = scene.Cameras[0];
        var target = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -500, 0);
        var source = target + new System.Numerics.Vector3(-800, -300, -500);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleSmogState state = Ff7BattleSmogEffect.Start(0x10);
        int random = 5;
        int randomCalls = 0;
        for (int frame = 0; frame <= 49; frame++)
        {
            Ff7BattleSmogUpdate update = Ff7BattleSmogEffect.Advance(state, () => source, _ => target, NextRandom);
            state = update.State;
            Assert.Equal(frame == 12 ? 0x10 : 0, update.ImpactTargets);
            if (frame is 6 or 20)
            {
                renderer.Render(gpu, 320, 240, new(camera, []) { Smog = state });
                Color[] image = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16, $"No original Smog pixels at callback {frame}.");
            }
            if (frame == 20)
            {
                state = JsonSerializer.Deserialize<Ff7BattleSmogState>(JsonSerializer.Serialize(state))!;
                Assert.NotNull(state.Targets[0].Cloud);
                Assert.Contains(state.Sprites, sprite => sprite.Velocity is null);
            }
        }
        Assert.Equal(90, randomCalls);
        Assert.True(state.Completed);
        Assert.Empty(state.Sprites);

        int NextRandom()
        {
            randomCalls++;
            random = unchecked(random * 214013 + 2531011);
            return random >> 16 & 0x7FFF;
        }
    }

    [Fact]
    public void LinkedAllIceAndBoltRetainNativeChildStaggersAfterColdRestore()
    {
        Ff7Installation installation = Installation();
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleRenderer renderer = new(archive, formation.LocationId, magic);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.Render(gpu, 320, 240, new(scene.Cameras[0], []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        foreach ((ushort spell, byte materia, int stagger) in new (ushort, byte, int)[] { (0x1E, 0x32, 4), (0x21, 0x34, 2) })
        {
            Ff7CharacterState[] characters = (Ff7CharacterState[])original.Progress.Characters.Clone();
            int cloud = Array.FindIndex(characters, character => character.Id == 0);
            byte[] record = (byte[])characters[cloud].NativeRecord.Clone();
            Array.Clear(record, 64, 8);
            record[64] = materia;
            record[68] = 0x17;
            characters[cloud] = characters[cloud] with { NativeRecord = record };
            Ff7Progress progress = original.Progress with { Party = [0, 255, 255], Characters = characters };
            Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
            Ff7BattleSnapshot snapshot = initial.Capture();
            snapshot.Actors[0] = snapshot.Actors[0] with { AwaitingTurn = true, Gauge = ushort.MaxValue };
            Ff7BattleSession battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);
            Assert.True(battle.Submit(0, Ff7BattleCommand.Magic, spell, 0x30, true));
            bool active = false;
            bool restored = false;
            bool rendered = false;
            for (int tick = 0; tick < 500; tick++)
            {
                battle.Advance(waitForCommand: true);
                Ff7BattleActionState? action = battle.Action;
                active |= action is not null;
                int frame = spell == 0x1E ? action?.Ice?.Frame ?? -1 : action?.Bolt?.Frame ?? -1;
                if (frame == 3 && !restored)
                {
                    Ff7BattleSession cold = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture())), animations: animations);
                    battle.Advance(waitForCommand: true);
                    cold.Advance(waitForCommand: true);
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                    battle = cold;
                    restored = true;
                }
                if (frame == 6 && action is not null)
                {
                    int[] starts = spell == 0x1E ? action.Ice!.Crystals!.Select(crystal => crystal.StartedAt).ToArray() : action.Bolt!.Strikes!.Select(strike => strike.StartedAt).ToArray();
                    Assert.Equal(new[] { 0, stagger }, starts);
                    renderer.Render(gpu, 320, 240, new(scene.Cameras[0], [], Ice: action.Ice, Bolt: action.Bolt));
                    Color[] image = GpuTestDevice.Read(gpu.Target);
                    Assert.True(baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 4, $"No original linked All pixels for spell {spell:X2}.");
                    rendered = true;
                }
                if (active && battle.Action is null)
                {
                    break;
                }
            }
            Assert.Null(battle.Action);
            Assert.True(restored);
            Assert.True(rendered);
            Assert.Equal(0, battle.Actors[0].AllUses!.GetValueOrDefault((byte)spell));
        }
    }

    [Fact]
    public void OriginalSweeperSmokeShotMovesItsEmitterAndRetainsSmokeFrames()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = scene.Cameras[0];
        var target = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -500, 0);
        var source = target + new System.Numerics.Vector3(-800, -300, -500);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleSmokeShotState state = Ff7BattleSmokeShotEffect.Start(0x10);
        for (int frame = 0; frame <= 25; frame++)
        {
            Ff7BattleSmokeShotUpdate update = Ff7BattleSmokeShotEffect.Advance(state, () => source, _ => target);
            state = update.State;
            Assert.Equal(frame == 15 ? 0x10 : 0, update.ImpactTargets);
            if (frame is 3 or 18)
            {
                renderer.Render(gpu, 320, 240, new(camera, []) { SmokeShot = state });
                Color[] image = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16, $"No original Smoke Shot pixels at callback {frame}.");
            }
            if (frame == 8)
            {
                state = JsonSerializer.Deserialize<Ff7BattleSmokeShotState>(JsonSerializer.Serialize(state))!;
                Assert.Equal(9, state.Sprites.Count);
                Assert.NotEqual(state.Sprites[0].Position, state.Sprites[8].Position);
            }
        }
        Assert.True(state.Completed);
        Assert.Empty(state.Sprites);
    }

    [Fact]
    public void OriginalOpeningPhysicalHitmarksUseTheInstalledCueScripts()
    {
        Ff7Installation installation = Installation();
        IReadOnlyList<Ff7BattleHitmarkDefinition> definitions = Ff7PcExecutableData.LoadBattleHitmarks(installation);
        Assert.Equal(72, definitions.Count);
        Ff7BattleHitmarkCue grenade = Assert.Single(definitions[0x2C].Cues);
        Assert.Equal(new Ff7BattleHitmarkCue(0, 1, 0, 0, 0x6000, 23, 8), grenade);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic, grenadeTexture: Ff7BattleGrenadeResources.Load(installation));
        SceneCameraPlacement camera = scene.Cameras[0];
        var anchor = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -500, 0);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        foreach (int style in new[] { 0x09, 0x12, 0x24, 0x25, 0x26, 0x2D, 0x2E, 0x30, 0x3C, 0x3D })
        {
            Ff7BattleHitmarkState state = Ff7BattleHitmarkEffect.Start(4, definitions[style]);
            for (int frame = 0; frame <= state.Definition.FrameCount; frame++)
            {
                state = Ff7BattleHitmarkEffect.Advance(state, _ => anchor, _ => 350);
                if (frame == 3)
                {
                    renderer.Render(gpu, 320, 240, new(camera, []) { Hitmarks = [state] });
                    Color[] image = GpuTestDevice.Read(gpu.Target);
                    Assert.True(baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 4, $"No original physical hitmark pixels for style {style:X2}.");
                    state = JsonSerializer.Deserialize<Ff7BattleHitmarkState>(JsonSerializer.Serialize(state))!;
                    Assert.Equal((short)anchor.X, state.Anchor!.X);
                }
            }
            Assert.True(state.Completed);
        }
    }

    [Fact]
    public void OriginalBigShotShowsChargeAndBothNativeFragmentFamilies()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = scene.Cameras[0];
        var anchor = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -700, 0);
        IReadOnlyList<BattleClipFrame> frames = BattleClipDecoder.Decode(Ff7BattleLimitAnimationResources.Load(magic, 0x87).Weapon[0]);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleBigShotState state = Ff7BattleBigShotEffect.Start(0x10);
        int random = 5;
        int randomCalls = 0;
        for (int frame = 0; frame <= 44; frame++)
        {
            Ff7BattleBigShotUpdate update = Ff7BattleBigShotEffect.Advance(state, point => BattleRigidPose.WeaponPosition(frames[frame % frames.Count], anchor, 0, point), _ => anchor, 0, NextRandom);
            state = update.State;
            Assert.Equal(frame == 22 ? 0x10 : 0, update.ImpactTargets);
            if (frame is 12 or 23 or 38)
            {
                renderer.Render(gpu, 320, 240, new(camera, []) { BigShot = state });
                Color[] image = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16, $"No original Big Shot pixels at callback {frame}.");
            }
            if (frame == 22)
            {
                state = JsonSerializer.Deserialize<Ff7BattleBigShotState>(JsonSerializer.Serialize(state))!;
                Assert.Null(state.Charge);
                Assert.Equal(50, state.Particles!.Count(particle => particle.Kind == Ff7BattleBigShotParticleKind.Fragment));
                Assert.Equal(30, state.Particles!.Count(particle => particle.Kind == Ff7BattleBigShotParticleKind.Burst));
            }
        }
        Assert.Equal(812, randomCalls);
        Assert.True(state.Completed);
        Assert.Equal(14080, state.ChargeRotation);
        Assert.Equal(-9856, state.CounterRotation);
        Ff7BattleBigShotState secondCast = Ff7BattleBigShotEffect.Start(0x10, state.ChargeRotation, state.CounterRotation);
        for (int frame = 0; frame <= 12; frame++)
        {
            secondCast = Ff7BattleBigShotEffect.Advance(secondCast, point => BattleRigidPose.WeaponPosition(frames[frame % frames.Count], anchor, 0, point), _ => anchor, 0, () => 5).State;
        }
        secondCast = JsonSerializer.Deserialize<Ff7BattleBigShotState>(JsonSerializer.Serialize(secondCast))!;
        Assert.Equal(22400, secondCast.ChargeRotation);
        Assert.Equal(-15680, secondCast.CounterRotation);
        Ff7BattleBigShotState continued = Ff7BattleBigShotEffect.Advance(secondCast, point => BattleRigidPose.WeaponPosition(frames[13 % frames.Count], anchor, 0, point), _ => anchor, 0, () => 5).State;
        Assert.Equal(23040, continued.ChargeRotation);
        Assert.Equal(-16128, continued.CounterRotation);

        int NextRandom()
        {
            randomCalls++;
            random = unchecked(random * 214013 + 2531011);
            return random >> 16 & 0x7FFF;
        }
    }

    [Fact]
    public void OriginalBeatRushShowsMovingJointTrailsAndThreeNativeHitSprites()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(battle, formation.LocationId, magic);
        SceneCameraPlacement camera = scene.Cameras[0];
        var anchor = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -400, 0);
        BattleModelData model = BattleModelData.Read(battle.ReadEntry(battle.Find("ruaa")[0]), battle.ReadEntry(battle.Find("ruda")[0]));
        IReadOnlyList<BattleClipFrame> frames = BattleClipDecoder.Decode(Ff7BattleLimitAnimationResources.Load(magic, 0x95).Body[0]);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);
        Ff7BattleBeatRushState state = Ff7BattleBeatRushEffect.Start(0x10);
        int random = 5;
        int randomCalls = 0;
        for (int frame = 0; frame <= 20; frame++)
        {
            Ff7BattleBeatRushUpdate update = Ff7BattleBeatRushEffect.Advance(state, point => BattleRigidPose.BonePoint(model, frames[frame % frames.Count], anchor, 0, 32, point), NextRandom);
            state = update.State;
            Assert.Equal(frame == 12 ? 0x10 : 0, update.ImpactTargets);
            if (frame is 3 or 6 or 13)
            {
                renderer.Render(gpu, 320, 240, new(camera, []) { BeatRush = state });
                Color[] image = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(image).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16, $"No original Beat Rush pixels at callback {frame}.");
            }
            if (frame == 7)
            {
                state = JsonSerializer.Deserialize<Ff7BattleBeatRushState>(JsonSerializer.Serialize(state))!;
                Assert.Equal(2, state.Trails!.Count);
                Assert.Single(state.Sprites!);
            }
        }
        Assert.Equal(12, randomCalls);
        Assert.True(state.Completed);

        int NextRandom()
        {
            randomCalls++;
            random = unchecked(random * 214013 + 2531011);
            return random >> 16 & 0x7FFF;
        }
    }

    [Fact]
    public void OriginalBraverShowsWeaponShineAndTargetImpactFromNativeResources()
    {
        Ff7Installation installation = Installation();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneBattleData scene = SceneBattleData.Read(formations[300]);
        Ff7BattleRenderer renderer = new(battle, formations[300].LocationId, magic);
        SceneCameraPlacement camera = scene.Cameras[0];
        var anchor = scene.Enemies[0].Placement.Position + new System.Numerics.Vector3(0, -320, 0);
        IReadOnlyList<BattleClipFrame> frames = BattleClipDecoder.Decode(Ff7BattleLimitAnimationResources.Load(magic, 0x80).Weapon[2]);
        renderer.Render(gpu, 320, 240, new(camera, []));
        Color[] baseline = GpuTestDevice.Read(gpu.Target);

        Ff7BattleBraverState state = Ff7BattleBraverEffect.Start(0x10);
        for (int frame = 0; frame <= 37; frame++)
        {
            Ff7BattleBraverUpdate update = Ff7BattleBraverEffect.Advance(state, point => BattleRigidPose.WeaponPosition(frames[frame % frames.Count], anchor, 0, point), _ => anchor, _ => 350);
            Assert.Equal(frame == 26 ? 0x10 : 0, update.ImpactTargets);
            state = update.State;
            if (frame is 12 or 27)
            {
                renderer.Render(gpu, 320, 240, new(camera, [], Braver: state));
                Color[] effect = GpuTestDevice.Read(gpu.Target);
                Assert.True(baseline.Zip(effect).Count(pair => pair.First.PackedValue != pair.Second.PackedValue) > 16, $"No original Braver pixels at callback {frame}.");
            }
            if (frame == 18)
            {
                Assert.NotNull(state.Trail);
                Assert.NotNull(state.PreviousTrail);
                Assert.NotEqual(state.Trail, state.PreviousTrail);
            }
        }
        Assert.True(state.Completed);
    }

    private static Ff7Installation Installation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII battle GPU checks.");
        return Ff7Installation.Discover(root!);
    }
}
