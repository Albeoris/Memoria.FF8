using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Runtime.InteropServices;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

/// <summary>Explicit diagnostic coordinate fixtures; never published as causal research saves.</summary>
public sealed class WorldForestMovementTests
{
    [Fact]
    public void Foot_crosses_original_forest_with_particles_and_restores_next_input_and_pixels()
    {
        GameSession session = ComponentSession();
        using WorldMapScene scene = new(session, GameDirectory());
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: NativeWorldEntryControl.DelayAfterCallback(scene.State.Frame, 0))).Accepted);
        PlaceDiagnosticFoot(session, scene, 18381, -68272);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 8));
        Assert.NotEqual(18381, scene.State.X);
        Assert.Equal((byte)1, scene.State.GroundType);
        NativeWorldForestState forest = scene.State.NativeFootRuntime!.Forest!;
        Assert.Equal((short)-160, forest.CameraPitch);
        Assert.Contains(forest.Particles, particle => particle.Visible);
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(saved);
        using WorldMapScene other = new(restored, GameDirectory());
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: 1);
        Assert.Equal(scene.Apply(next), other.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
        byte[] pixels = scene.Render(640, 448).ToArray();
        Assert.Equal(pixels, other.Render(640, 448));
        Assert.Throws<InvalidDataException>(() => new GameSession(saved with { WorldProcess = saved.WorldProcess! with { CameraCooldown = saved.WorldProcess.CameraCooldown + 1 } }));
        NativeWorldForestState visible = scene.State.NativeFootRuntime!.Forest!;
        session.SetWorldState(scene.State with { NativeFootRuntime = scene.State.NativeFootRuntime with { Forest = visible with { Particles = [] } } });
        Assert.NotEqual(pixels, scene.Render(640, 448));
        string output = TestResources.ArtifactFile("App.Tests.WorldForestMovementTests.1.png");
        using Bitmap bitmap = PortableRaster.FromRgba(pixels, 640, 448);
        bitmap.Save(output);
    }

    [Fact]
    public void Forest_particles_select_the_nearest_periodic_image()
    {
        using WorldAssets assets = new(GameDirectory());
        WorldTerrainRenderer renderer = new(assets, 640, 448);
        Vector3 position = new(18381, 4000, -68272);
        WorldCamera camera = WorldCamera.Foot(0, 4096);
        NativeWorldForestParticle particle = new(0, 5, 18381, -4000, 68272, 0, 0, 4096, 1, 8, true);
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0) with { Particles = [particle] };
        byte[] pixels = renderer.Render(position, 0, 0, 0, camera, avatarVisible: false, forest: forest).ToArray();
        Assert.NotEqual(pixels, renderer.Render(position, 0, 0, 0, camera, avatarVisible: false));
        foreach ((int x, int z) in new[] { (262144, 0), (-262144, 0), (0, 196608), (0, -196608), (262144, -196608) })
        {
            NativeWorldForestState shifted = forest with { Particles = [particle with { X = particle.X + x, GraphicsZ = particle.GraphicsZ + z }] };
            Assert.Equal(pixels, renderer.Render(position, 0, 0, 0, camera, avatarVisible: false, forest: shifted));
        }
    }

    [Fact]
    public void Forest_advice_uses_the_native_forest_height_exception()
    {
        using WorldAssets assets = new(GameDirectory());
        WorldTerrainRoute route = WorldTerrainNavigationGuide.Plan(assets, 18381, -68272, -5000, 285, 0, 18412, -68272, padding: 0);
        Assert.Null(route.Refusal);
        Assert.Equal((18412, -68272), (route.Waypoints[^1].X, route.Waypoints[^1].RawZ));
    }

    [Fact]
    public void Original_chocobo_forest_entry_commits_its_actual_field_packet()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, GameDirectory());
        world.EnsureField();
        for (int callback = 0; callback < 8 && world.Events.Scene.WorldMusicSelection is null; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(SceneKind.Field, session.State.Scene);
        NativeWorldMusicSelection selection = Assert.IsType<NativeWorldMusicSelection>(world.Events.Scene.WorldMusicSelection);
        Assert.Equal(NativeWorldMusicSelectionOrigin.ExecutedClear, selection.Origin);
        // Component entry executes the native music loader without claiming a traveled field departure.
        using WorldAssets assets = new(GameDirectory());
        WorldTransition packet = new(0, 0, 0);
        Action commitMusic = world.Events.PrepareWorldMusic(packet, assets.ReadMusicResource, assets.ReadMusicFileResource);
        session.RequestWorld(packet);
        commitMusic();
        NativeWorldMusicLoad music = Assert.IsType<NativeWorldMusicLoad>(world.Events.Scene.WorldMusicLoad);
        Assert.Equal(NativeWorldMusicOperation.Stop, music.Operation);
        Assert.Equal(packet, music.Packet);
        session.SetEventState(world.Events.Capture());
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: NativeWorldEntryControl.DelayAfterCallback(world.WorldScene!.State.Frame, 0))).Accepted);
        PlaceDiagnosticFoot(session, world.WorldScene!, 10866, -80300);
        PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal(PlayerTriggerKind.FieldTransition, result.Trigger.Kind);
        Assert.Equal(287, session.State.FieldId);
        Assert.Equal(new FieldTransition(287, -310, -3065, 147, 128), session.State.FieldArrival);
        Assert.True(world.EnsureField());
        Assert.Equal("cwwood1", world.Field!.Name);
        for (int callback = 0; callback < 20; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Equal((byte)3, world.Events.Actors[0].NativeLadderAnimation!.Start594);
        Console.WriteLine($"Forest first20callbacks: {world.NextTrigger.Kind}, ladder={world.Events.Actors[0].NativeLadderAnimation}.");
    }

    private static void PlaceDiagnosticFoot(GameSession session, WorldMapScene scene, int x, int z)
    {
        using WorldAssets assets = new(GameDirectory());
        var replacements = WorldTerrainSelection.Replacements(scene.State.ReplacementMask);
        WorldGroundSelection ground = assets.LookupNativeGround(x, z, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        int height = unchecked((short)(-(int)ground.Ground.Height + (ground.Ground.Triangle.Forest ? 320 : 0)));
        //User-authorized diagnostic placement, explicitly outside live journeys.
        session.SetWorldState(scene.State with { X = x, Z = z, NativeHeight = height, CameraYaw = 0, BodyYaw = 1024, CameraTargetX = x, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, GroundType = ground.Ground.Triangle.GroundType, NativeFootRuntime = scene.State.NativeFootRuntime! with { GroundCache = cache, PreviousHorizontalInput = 0 } });
    }

    private static GameSession ComponentSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory());
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        return session;
    }

    private static string GameDirectory() => TestResources.GameDirectory;

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research workspace was not found.");
    }
}
