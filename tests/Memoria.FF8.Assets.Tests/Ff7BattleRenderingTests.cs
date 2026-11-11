using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7BattleRenderingTests
{
    [Fact]
    public void OpeningGuardsUseOpaqueMaterialsUntilTheDeathCallbackFadesThem()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII battle rendering.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        Ff7BattleActorVisual[] actors = scene.Enemies.Select(enemy => new Ff7BattleActorVisual(Ff7BattleRenderer.EnemyModelPrefix(enemy.Placement.EnemyId), enemy.Placement.Position, MathF.PI, 0, 0, 0)).ToArray();
        SceneCameraPlacement camera = scene.Cameras[0];
        Capture stage = new();
        renderer.Render(stage, 320, 240, new(camera, []));
        Assert.Equal(TriangleBlend.HalfAdd, stage.LastBlend);

        List<(TriangleMaterial Material, float Alpha)> submitted = [];
        Capture capture = new((triangle, material) => submitted.Add((material, triangle.Color.W)));
        renderer.Render(capture, 320, 240, new(camera, actors));
        var opaque = submitted.Skip(stage.Triangles).ToArray();
        Assert.NotEmpty(opaque);
        Assert.All(opaque, draw =>
        {
            Assert.Equal(TriangleBlend.Replace, draw.Material.Blend);
            Assert.True(draw.Material.DepthWrite);
            Assert.Equal(1f, draw.Alpha);
        });

        Ff7BattleActorDeathUpdate death = Ff7BattleActorDeath.Advance(new(4, 0), new());
        Ff7BattleActorModelModifiers fading = death.Write.Modifiers!;
        submitted.Clear();
        renderer.Render(capture, 320, 240, new(camera, [actors[0] with { ModelModifiers = fading }]));
        var faded = submitted.Skip(stage.Triangles).ToArray();
        Assert.NotEmpty(faded);
        Assert.All(faded, draw =>
        {
            Assert.Equal(TriangleBlend.Alpha, draw.Material.Blend);
            Assert.False(draw.Material.DepthWrite);
            Assert.Equal(fading.Alpha / 255f, draw.Alpha, 5);
        });

        while (death.State is { } callback)
        {
            death = Ff7BattleActorDeath.Advance(callback, death.Write.Modifiers!);
        }
        submitted.Clear();
        renderer.Render(capture, 320, 240, new(camera, [actors[0] with { ModelModifiers = death.Write.Modifiers }]));
        Assert.Equal(stage.Triangles, submitted.Count);
    }

    [Fact]
    public void InstalledOpeningBattlesSubmitArenaAndAnimatedActorGeometry()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII battle rendering.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        IEnumerable<int> selection = Enumerable.Range(300, 25).Concat([328, 329, 330]).Concat(Enumerable.Range(332, 28)).Concat([364, 367, 392, 393, 394, 395]);
        Dictionary<int, Ff7BattleRenderer> arenas = [];
        foreach (int id in selection)
        {
            SceneFormation formation = formations[id];
            if (!arenas.TryGetValue(formation.LocationId, out Ff7BattleRenderer? renderer))
            {
                renderer = new(archive, formation.LocationId);
                arenas.Add(formation.LocationId, renderer);
            }
            SceneBattleData scene = SceneBattleData.Read(formation);
            SceneCameraPlacement camera = scene.Cameras[0];
            Capture background = new();
            renderer.Render(background, 320, 240, new(camera, []));
            Assert.True(background.Textured > 0, $"Formation {id} has no original arena triangles.");
            Ff7BattleActorVisual[] actors = scene.Enemies.Select(enemy => new Ff7BattleActorVisual(
                Ff7BattleRenderer.EnemyModelPrefix(enemy.Placement.EnemyId),
                enemy.Placement.Position,
                enemy.Placement.Position.Z < 0 ? MathF.PI : 0,
                0, 0, enemy.Placement.EnemyId == 16 ? 0 : null)).ToArray();
            Capture posed = new();
            renderer.Render(posed, 320, 240, new(camera, actors));
            Assert.True(posed.Triangles > background.Triangles, $"Formation {id} has no original actor triangles.");
        }
        Assert.Equal(11, arenas.Count);

        Ff7BattleRenderer opening = arenas[9];
        SceneCameraPlacement openingCamera = SceneBattleData.Read(formations[300]).Cameras[0];
        for (int character = 0; character < 3; character++)
        {
            string prefix = Ff7BattleRenderer.PartyModelPrefix(character);
            Ff7BattleActorVisual actor = new(prefix, new(0, 0, 1700), 0, 0, 0, 0);
            Capture first = new();
            Capture next = new();
            opening.Render(first, 320, 240, new(openingCamera, [actor]));
            int later = opening.GetClipFrameCount(prefix, 0) / 2;
            opening.Render(next, 320, 240, new(openingCamera, [actor with { ClipFrame = later }]));
            Assert.NotEqual(first.PositionSum, next.PositionSum);
            Capture bodyOnly = new();
            opening.Render(bodyOnly, 320, 240, new(openingCamera, [actor with { WeaponIndex = null }]));
            Assert.True(first.Triangles > bodyOnly.Triangles, $"Character {character} has no linked weapon geometry.");
        }
    }

    [Theory]
    [InlineData("aq", 3, 0xF7, 8)]
    [InlineData("aq", 4, 0xF7, 4)]
    [InlineData("rt", 0x14, 0xF7, 1)]
    [InlineData("sb", 0x14, 0xF7, 3)]
    [InlineData("ru", 0x14, 0xC2, 8)]
    [InlineData("rt", 0x1D, 0xEC, -1)]
    [InlineData("rt", 0x20, 0xEB, -1)]
    public void InstalledActionCallbacksShareClipAndImpactTimeline(string prefix, int action, byte impactOpcode, int delay)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII battle action timing.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        byte[] Read(string suffix) => archive.ReadEntry(Assert.Single(archive.Find(prefix + suffix)));
        BattleModelData model = BattleModelData.Read(Read("aa"), Read("da"));
        BattleActionSequence sequence = BattleActionSequence.Read(BattleActionBank.Read(Read("ab")), model, action);
        BattleActionPoseState state = new();
        List<BattleActionInstruction> events = [];
        int frames = 0;
        bool waited = false;
        for (int callback = 0; callback < 300 && !state.Completed; callback++)
        {
            BattleActionPoseUpdate next = sequence.Advance(state);
            BattleActionPoseState restored = JsonSerializer.Deserialize<BattleActionPoseState>(JsonSerializer.Serialize(state))!;
            Assert.Equal(next.State, sequence.Advance(restored).State);
            if (!next.State.Completed && next.State == state && next.Events.Count == 0)
            {
                waited = true;
                next = sequence.Advance(state, effectReady: true);
            }
            if (next.State.ClipIndex >= 0 && next.State != state)
            {
                frames++;
            }
            events.AddRange(next.Events);
            state = next.State;
        }

        Assert.True(state.Completed);
        Assert.True(frames > 1);
        BattleActionInstruction impact = Assert.Single(events, instruction => instruction.Opcode == impactOpcode);
        if (delay >= 0)
        {
            Assert.Equal(delay, impact.Operands.Span[0]);
        }
        Assert.Equal(delay < 0, waited);
        Assert.Equal(0xEE, events[^1].Opcode);
    }

    [Theory]
    [InlineData("rt", 0x80, 3, 0xEE)]
    [InlineData("sb", 0x87, 30, 0xEE)]
    [InlineData("ru", 0x95, 20, 0x9E)]
    public void InstalledFirstLimitsOverlayOriginalBodyAndWeaponFrames(string prefix, ushort attack, int firstClipFrames, byte transfer)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII Limit animation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        byte[] Read(string suffix) => battle.ReadEntry(Assert.Single(battle.Find(prefix + suffix)));
        BattleModelData baseModel = BattleModelData.Read(Read("aa"), Read("da"));
        BattleLimitAnimation limit = Ff7BattleLimitAnimationResources.Load(magic, attack);
        BattleModelData model = limit.ApplyTo(baseModel);
        Assert.Equal(firstClipFrames, BattleClipDecoder.Decode(model.BodyClips[0x2C]).Count);
        Assert.Equal(firstClipFrames, BattleClipDecoder.Decode(model.WeaponClips[0x2C]).Count);
        BattleActionSequence sequence = BattleActionSequence.Read(BattleActionBank.Read(Read("ab")), model, 0x3C);
        BattleActionPoseState state = new();
        List<BattleActionInstruction> events = [];
        for (int tick = 0; tick < 200 && !state.Completed; tick++)
        {
            BattleActionPoseUpdate update = sequence.Advance(state, effectReady: true);
            state = update.State;
            events.AddRange(update.Events);
        }
        Assert.True(state.Completed);
        Assert.Equal(transfer, events[^1].Opcode);
    }

    private sealed class Capture(Action<TriangleVertex, TriangleMaterial>? inspect = null) : ITriangleRenderer
    {
        public int Triangles { get; private set; }
        public int Textured { get; private set; }
        public Vector2 PositionSum { get; private set; }
        public TriangleBlend LastBlend { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles++;
            LastBlend = material.Blend;
            inspect?.Invoke(a, material);
            if (material.Texture is not null)
            {
                Textured++;
            }
            Assert.True(a.ReciprocalW > 0 && b.ReciprocalW > 0 && c.ReciprocalW > 0);
            PositionSum += a.Position + b.Position + c.Position;
        }

        public void End()
        {
        }
    }
}
