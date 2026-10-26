using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleScenePresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_backface_rejection_uses_original_face_winding_before_splitting(bool quad)
    {
        BattleVertex[] vertices = [new(new Vector3(-1, 0, 0), 0), new(new Vector3(1, 0, 0), 0), new(new Vector3(-1, 2, 0), 0), new(new Vector3(1, 2, 0), 0)];
        int[] indices = quad ? [0, 1, 2, 3] : [0, 1, 2];
        Vector2[] uv = Enumerable.Repeat(Vector2.Zero, indices.Length).ToArray();
        PixelImage texture = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
        BattleFace face = new(indices, uv, 0, new Rgba32(128, 128, 128, 255));
        BattleRenderer renderer = new(64, 64);
        BattleCamera camera = new(new Vector3(0, 1, -10), new Vector3(0, 1, 0));
        renderer.Render(new BattleStage(0, [new BattleMesh(vertices, [face])], [texture]), [], camera);
        Assert.Equal(quad ? 2 : 1, renderer.DrawnTriangles);
        BattleFace reversed = face with { Vertices = quad ? [1, 0, 3, 2] : [1, 0, 2] };
        renderer.Render(new BattleStage(0, [new BattleMesh(vertices, [reversed])], [texture]), [], camera);
        Assert.Equal(0, renderer.DrawnTriangles);
    }

    [Fact]
    public void Native_party_slots_preserve_count_order_and_opposing_depth_axis()
    {
        Assert.Equal(new Vector3(0, 0, -18), BattleFormationPresentation.PartyPosition(0, 1));
        Assert.Equal(new Vector3(-10, 0, -18), BattleFormationPresentation.PartyPosition(0, 2));
        Assert.Equal(new Vector3(10, 0, -18), BattleFormationPresentation.PartyPosition(1, 2));
        Assert.Equal(new Vector3(-12, 0, -21), BattleFormationPresentation.PartyPosition(0, 3));
        Assert.Equal(new Vector3(0, 0, -15), BattleFormationPresentation.PartyPosition(1, 3));
        Assert.Equal(new Vector3(12, 0, -21), BattleFormationPresentation.PartyPosition(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleFormationPresentation.PartyPosition(3, 3));
        BattleRenderer renderer = new(640, 448);
        renderer.Render(new BattleStage(0, [], []), []);
        Assert.True(renderer.TryProject(BattleFormationPresentation.PartyPosition(0, 1), out Vector2 party));
        Assert.True(renderer.TryProject(BattleFormationPresentation.DebugEnemyPosition(0, 1), out Vector2 enemy));
        Assert.True(party.X > enemy.X, "The original bootstrap view places the native party on the right, facing opponents on the left.");
        Assert.True(party.Y > enemy.Y);
        Assert.False(renderer.TryProject(BattleCamera.NativeInitial.Eye, out _));
    }

    [Fact]
    public void Installed_scene_uses_its_native_introduction_subject_angle_and_lens_without_actor_bounds_fitting()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        BattleDefinition single = ReadyBattle().Capture().Definition;
        BattleScenePresentation first = new(assets, single, false);
        BattleDefinition expanded = single with { Combatants = [.. single.Combatants, new() { Id = "second-party", Name = "Zell", IsPlayer = true, ModelId = 1 }] };
        BattleScenePresentation second = new(assets, expanded, false);
        BattleCamera expected = BattleCamera.FromNative(assets.LoadStage(0).IntroductionCameras[0]);
        Assert.Equal(expected, first.Camera);
        Assert.Equal(expected, second.Camera);
        first.Render(new BattleSession(single, BattleAtbMode.TurnBased), 640, 480);
        first.Render(new BattleSession(single, BattleAtbMode.TurnBased), 1280, 720);
        Assert.Equal(expected, first.Camera);
    }

    [Fact]
    public void Inactive_information_only_reserve_waits_for_activation_before_graphics_are_required()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        BattleDefinition original = ReadyBattle().Capture().Definition;
        BattleDefinition definition = original with { Combatants = [.. original.Combatants, new() { Id = "reserve", Name = "Inactive information-only reserve", ModelId = 127, SceneSlot = 7, InitialActive = false }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, false);
        Assert.NotEmpty(scene.Render(session, 320, 240));
        BattleSnapshot snapshot = session.Capture();
        BattleSnapshot activated = snapshot with { Combatants = snapshot.Combatants.Select(actor => actor.Id == "reserve" ? actor with { NativeParticipant = actor.NativeParticipant! with { Active = true, ActorIndex = 4, NativeCombatantFlags = actor.NativeParticipant.NativeCombatantFlags!.Value | 1 } } : actor).ToArray() };
        Assert.Throws<NotSupportedException>(() => scene.Render(BattleSession.Restore(activated), 320, 240));
        Assert.False(session.Combatants.Single(actor => actor.Id == "reserve").IsActive);
    }

    [Fact]
    public void Hit_feedback_uses_committed_events_survives_restore_and_expires_without_mutation()
    {
        BattleSession session = ReadyBattle();
        int previousHp = session.Combatants.Single(actor => actor.Id == "enemy").CurrentHp;
        session.QueueAttack("party", "enemy");
        Tick(session, BattleSession.AttackWindupFrames);
        Assert.Empty(BattleImpactPresentation.Collect(session));
        session.Tick();
        BattleImpactCue impact = Assert.Single(BattleImpactPresentation.Collect(session));
        Assert.True(impact.IsPhysical);
        Assert.Equal(0, impact.Age);
        Assert.True(impact.Event.Amount > 0);
        Assert.Equal(previousHp - session.Combatants.Single(actor => actor.Id == "enemy").CurrentHp, impact.Event.Amount);
        string before = JsonSerializer.Serialize(session.Capture());
        BattleSession restored = BattleSession.Restore(session.Capture());
        Assert.Equal(BattleImpactPresentation.Collect(session), BattleImpactPresentation.Collect(restored));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Tick(session, BattleImpactPresentation.Duration);
        Assert.Empty(BattleImpactPresentation.Collect(session));
    }

    [Fact]
    public void Hit_feedback_finishes_when_dispatch_is_paused_and_cannot_reappear_on_next_tick()
    {
        BattleSession session = ReadyBattle();
        session.QueueAttack("party", "enemy");
        Tick(session, BattleSession.AttackWindupFrames + 1);
        BattleImpactTimeline timeline = new();
        Assert.Single(timeline.Collect(session));
        timeline.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(timeline.Collect(session));
        session.Tick();
        Assert.Empty(timeline.Collect(session));
    }

    [Fact]
    public void Installed_scene_draws_hit_and_HP_value_through_the_shared_live_preview_canvas()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        BattleSession session = ReadyBattle();
        session.QueueAttack("party", "enemy");
        Tick(session, BattleSession.AttackWindupFrames + 1);
        string before = JsonSerializer.Serialize(session.Capture());
        using BattleAssets assets = new(directory);
        BattleScenePresentation scene = new(assets, session.Capture().Definition, false);
        byte[] pixels = scene.Render(session, 640, 448).ToArray();
        using Bitmap bitmap = new(640, 448, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            canvas.DrawRgba(pixels, 640, 448, new UiRect(0, 0, 640, 448));
            scene.DrawEffects(session, canvas, new UiRect(0, 0, 640, 448));
        }
        using Bitmap unchanged = BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 448);
        Assert.Contains(Enumerable.Range(0, 448), y => Enumerable.Range(0, 640).Any(x => unchanged.GetPixel(x, y) != bitmap.GetPixel(x, y)));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PRESENTATION_EVIDENCE") is string evidence)
        {
            Directory.CreateDirectory(evidence);
            string snapshot = TestResources.ArtifactFile("App.Tests.BattleScenePresentationTests.1.json");
            File.WriteAllText(snapshot, JsonSerializer.Serialize(session.Capture(), new JsonSerializerOptions { WriteIndented = true }));
            Assert.Equal(0, BattlePreview.Run(["--snapshot", snapshot, "--output", TestResources.ArtifactFile("App.Tests.BattleScenePresentationTests.2.png"), "--game-dir", directory]));
        }
    }

    private static BattleSession ReadyBattle()
    {
        return new BattleSession(new BattleDefinition
        {
            StageId = 0,
            Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, Attack = 80, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Test opponent", ModelId = 1, MaximumHp = 1000, CurrentHp = 1000, Defense = 20, Speed = 1 }]
        }, BattleAtbMode.Normal);
    }

    private static void Tick(BattleSession session, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            session.Tick();
        }
    }
}
