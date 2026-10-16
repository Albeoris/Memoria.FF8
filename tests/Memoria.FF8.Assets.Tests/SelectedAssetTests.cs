using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class SelectedAssetTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void SelectedInstalledScenesHaveExpectedOriginalData()
    {
        using GameAssets game = new(GameDirectory);
        FieldAssets field = game.LoadField(180);
        Assert.Equal("bghoke_2", field.Name);
        Assert.Equal(8024, field.Jsm.Length);
        Assert.Equal(819, field.Background.Count);
        Assert.Equal(105, field.Walkmesh.Triangles.Count);
        Assert.Equal(10, field.Models.Count);
        Assert.Equal(25, field.Models[8].Bones.Count);
        Assert.Equal(308, field.Models[8].Vertices.Count);
        Assert.Equal(400, field.Models[8].Faces.Count);
        Assert.Equal(23, field.Models[9].Bones.Count);
        Assert.Equal(321, field.Models[9].Vertices.Count);
        Assert.Equal(403, field.Models[9].Faces.Count);
        Assert.Contains(field.Messages, message => message.Contains("Squall", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith("disc00_30h.avi", game.GetMoviePath(30));
        Assert.EndsWith("disc00_01h.avi", game.GetMoviePath(1));
        Assert.Equal("start0", game.LoadField(74).Name);
        Assert.Equal("bghoke_3", game.LoadField(181).Name);
        Assert.Equal("bgroad_6", game.LoadField(229).Name);
        Assert.All(field.Models, model => Assert.NotEmpty(model.Animations));
        Assert.Equal(field.Models[0].Animations[0].Frames.Count, field.GetAnimationFrameCount(0, 0));
    }

    [Fact]
    public void UnselectedClassroomSpriteModuleStaysOffUntilItsFrameIsExplicitlySelected()
    {
        using GameAssets game = new(GameDirectory);
        FieldAssets field = game.LoadField(232);
        Assert.Equal(11, field.Background.Count(tile => tile.AnimationId == 6 && tile.AnimationState == 0));
        Dictionary<int, int> states = new() { [0] = 1, [1] = 1, [2] = 0, [3] = -1, [4] = 0, [5] = -1 };
        FieldRenderer renderer = new(field, 960, 720);
        byte[] unselected = renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1).ToArray();
        states[6] = -1;
        Assert.Equal(unselected, renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1));
        states[6] = 0;
        Assert.NotEqual(unselected, renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1));
    }

    [Fact]
    public void ClassroomSelectsItsSecondInstalledCameraAndKeepsEarlierCameraZero()
    {
        using GameAssets game = new(GameDirectory);
        FieldAssets classroom = game.LoadField(232);
        Assert.Equal("bgroom_1", classroom.Name);
        Assert.Equal(2, classroom.Cameras.Count);
        Assert.Equal(new Vector3(-124, 419, 1216), classroom.GetCamera(0).Translation);
        Assert.Equal(new Vector3(-2237, 546, 284), classroom.GetCamera(1).Translation);
        Assert.Equal(439, classroom.GetCamera(1).FocalLength);
        Vector3 point = new(1000, -3200, 200);
        ProjectedPoint first = classroom.GetCamera(0).Project(point);
        ProjectedPoint second = classroom.GetCamera(1).Project(point);
        Assert.True(first.X > 320);
        Assert.InRange(second.X, 0, 320);
        Assert.InRange(second.Y, 0, 240);
        Assert.Throws<InvalidDataException>(() => classroom.GetCamera(2));

        PixelImage texture = new(16, 16, Enumerable.Repeat(new Rgba32(0, 0, 200), 256).ToArray());
        FieldModel model = new("camera-fixture", [new(-1, 0)], [new(new Vector3(0, -100, -100), 0), new(new Vector3(0, 100, -100), 0), new(new Vector3(0, 0, 100), 0)], [new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldRenderer renderer = new(classroom with { Background = [], Models = [model] });
        ActorVisual[] actors = [new(0, 0, point.X, point.Y, point.Z, 192, 0, 0, true, PolygonColor: new Vector3(128))];
        byte[] cameraZero = renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero).ToArray();
        byte[] cameraOne = renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero, cameraIndex: 1).ToArray();
        Assert.DoesNotContain(cameraZero.Where((_, index) => index % 4 != 3), value => value != 0);
        Assert.Contains(cameraOne.Where((_, index) => index % 4 != 3), value => value != 0);

        foreach ((int id, Vector3 translation, float focal) in new[] { (180, new Vector3(-232, 606, 208), 343f), (181, new Vector3(-891, 206, -31), 307f), (229, new Vector3(-190, 920, -3417), 503f) })
        {
            FieldAssets previous = game.LoadField(id);
            Assert.Single(previous.Cameras);
            Assert.Equal(translation, previous.Camera.Translation);
            Assert.Equal(focal, previous.Camera.FocalLength);
            Assert.Equal(previous.Camera, previous.GetCamera(0));
        }
    }

    [Fact]
    public void ClassroomModuleThreeShadeChangesOnlyItsSelectedTerminalTiles()
    {
        using GameAssets game = new(GameDirectory);
        FieldAssets classroom = game.LoadField(232);
        BackgroundTile[] terminal = classroom.Background.Where(tile => tile.AnimationId == 3).ToArray();
        Assert.Equal(6, terminal.Length);
        Assert.All(terminal, tile => Assert.Equal(0, tile.AnimationState));
        Assert.Equal(-32, terminal.Min(tile => tile.X));
        Assert.Equal(0, terminal.Max(tile => tile.X));
        Assert.Equal(-96, terminal.Min(tile => tile.Y));
        Assert.Equal(-80, terminal.Max(tile => tile.Y));
        Dictionary<int, int> states = new() { [0] = 1, [3] = 0 };
        FieldRenderer renderer = new(classroom);
        byte[] dark = renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1, backgroundModuleShades: new Dictionary<int, Vector3> { [3] = Vector3.Zero }).ToArray();
        byte[] light = renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1, backgroundModuleShades: new Dictionary<int, Vector3> { [3] = new(126) }).ToArray();
        int changed = 0;
        for (int pixel = 0; pixel < 320 * 240; pixel++)
        {
            if (!dark.AsSpan(pixel * 4, 4).SequenceEqual(light.AsSpan(pixel * 4, 4)))
            {
                changed++;
                Assert.InRange(pixel % 320, 128, 175);
                Assert.InRange(pixel / 320, 24, 55);
            }
        }
        Assert.True(changed > 0);
        states[3] = 1;
        dark = renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1, backgroundModuleShades: new Dictionary<int, Vector3> { [3] = Vector3.Zero }).ToArray();
        light = renderer.Render(states, [], TimeSpan.Zero, cameraIndex: 1, backgroundModuleShades: new Dictionary<int, Vector3> { [3] = new(126) }).ToArray();
        Assert.Equal(dark, light);
    }

    [Fact]
    public void AnimationAndCurtainStatesProduceDifferentOriginalPixels()
    {
        using GameAssets game = new(GameDirectory);
        FieldAssets field = game.LoadField(180);
        FieldRenderer renderer = new(field);
        byte[] first = renderer.Render(new Dictionary<int, int> { [0] = 0 }, [], TimeSpan.Zero).ToArray();
        byte[] curtain = renderer.Render(new Dictionary<int, int> { [0] = 5 }, [], TimeSpan.Zero).ToArray();
        Assert.False(first.SequenceEqual(curtain));
        Assert.Contains(first.Where((_, index) => index % 4 != 3), value => value != 0);
        FieldModel model = field.Models[0];
        Vector3[] pose1 = model.Pose(0, 0);
        Vector3[] pose2 = model.Pose(0, model.Animations[0].Frames.Count - 1);
        Assert.All(pose1, position => Assert.True(float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z)));
        if (model.Animations[0].Frames.Count > 1)
        {
            Assert.False(pose1.SequenceEqual(pose2));
        }
        string? evidence = Environment.GetEnvironmentVariable("FF8_RENDER_EVIDENCE");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.2.rgba"), first);
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.3.rgba"), curtain);
            ActorVisual[] actors = [new(0, 0, -188, 738, 242, 64, 3, 31, true, PolygonColor: new Vector3(128)), new(8, 8, 60, 600, 0, 192, 0, 0, true, PolygonColor: new Vector3(128))];
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.4.rgba"), renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero));
            actors[0] = actors[0] with { AnimationId = 0, AnimationFrame = 0, X = 0, Y = 550, Z = 0 };
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.5.rgba"), renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero));
            actors[0] = actors[0] with { AnimationId = 1, AnimationFrame = 0 };
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.6.rgba"), renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero));
            // Installed initial JSM snapshot, including Squall's world-space DOFFSET(0,48,0).
            ActorVisual[] initialActors = [new(1, 1, -141, 802, 0, 192, 3, 2, true, PolygonColor: new Vector3(128)), new(8, 8, -150, 650, 0, 64, 0, 1, true, PolygonColor: new Vector3(128))];
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.7.rgba"), renderer.Render(new Dictionary<int, int>(), initialActors, TimeSpan.Zero));
            initialActors[0] = initialActors[0] with { AnimationId = 5, AnimationFrame = 33 };
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.8.rgba"), renderer.Render(new Dictionary<int, int>(), initialActors, TimeSpan.Zero));
            foreach (int id in new[] { 179, 181, 229 })
            {
                FieldAssets scene = game.LoadField(id);
                File.WriteAllBytes(Path.Combine(evidence, $"field-{id}-background.rgba"), new FieldRenderer(scene).Render(new Dictionary<int, int>(), [], TimeSpan.Zero));
            }
        }
    }

    [Fact]
    public void TransparentForegroundTilePreservesTheBackground()
    {
        PixelImage background = new(16, 16, Enumerable.Repeat(new Rgba32(20, 40, 60), 256).ToArray());
        PixelImage foreground = new(16, 16);
        foreground[0, 0] = new Rgba32(100, 110, 120);
        FieldAssets field = new(0, "test", [], [], [new(0, 0, 100, 255, 0, 0, MapBlendMode.None, false, true, background), new(0, 0, 10, 255, 0, 1, MapBlendMode.None, false, true, foreground)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 343), new FieldWalkmesh([]), []);
        byte[] pixels = new FieldRenderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        int first = (120 * 320 + 160) * 4;
        Assert.Equal(new byte[] { 100, 110, 120, 255 }, pixels.AsSpan(first, 4).ToArray());
        Assert.Equal(new byte[] { 20, 40, 60, 255 }, pixels.AsSpan(first + 4, 4).ToArray());
    }

    [Fact]
    public void WalkmeshHonorsLockedTrianglesAndInterpolatesHeight()
    {
        FieldWalkmesh mesh = new([new(new Vector3(0, 0, 0), new Vector3(10, 0, 10), new Vector3(0, 10, 20), -1, -1, -1)]);
        Assert.True(mesh.TryPosition(2, 3, out Vector3 position, out int triangle));
        Assert.Equal(0, triangle);
        Assert.Equal(8, position.Z, 4);
        Assert.False(mesh.TryPosition(20, 20, out _, out _));
        Assert.False(mesh.TryPosition(2, 3, out _, out _, new HashSet<int> { 0 }));
    }

    [Fact]
    public void ClassroomStandingActorUsesAnimationElevationRatherThanRawSetgetaByte()
    {
        using GameAssets game = new(GameDirectory);
        FieldAssets classroom = game.LoadField(232);
        FieldModel student = classroom.Models[10];
        Vector3[] pose = student.Pose(4, 0);
        Vector3[] elevated = student.Pose(4, 0, rootTranslationZ: 242);
        Assert.StartsWith("d021", student.Name);
        Assert.Equal(new Vector3(0, 8, 230), student.Animations[4].Frames[0].Translation);
        Assert.InRange(pose.Min(point => point.Z), -3, 3);
        Assert.InRange(elevated.Min(point => point.Z), 239, 245);
        Assert.All(Enumerable.Range(0, pose.Length), index => Assert.Equal(242, elevated[index].Z - pose[index].Z, 3));
        Assert.True(classroom.Walkmesh.TryPosition(1432, -3080, out Vector3 floor, out _));
        Assert.Equal(0, floor.Z);
        Assert.All(student.Textures, texture => Assert.Equal((128, 128), (texture.Width, texture.Height)));
        ProjectedPoint floorProjection = classroom.GetCamera(1).Project(floor);
        Assert.InRange(floorProjection.Y, 136.9f, 137.1f);
        Assert.True(classroom.GetCamera(1).Project(Vector3.Zero).X < -3000);
        string? evidence = Environment.GetEnvironmentVariable("FF8_RENDER_EVIDENCE");
        if (evidence is not null)
        {
            FieldRenderer renderer = new(classroom);
            Dictionary<int, int> states = new() { [0] = 1 };
            ActorVisual actor = new(11, 10, 1432, -3080, 0, 142, 4, 0, true, PolygonColor: new Vector3(128));
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.9.rgba"), renderer.Render(states, [actor with { RootTranslationZ = 242 }], TimeSpan.Zero, cameraIndex: 1));
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.10.rgba"), renderer.Render(states, [actor], TimeSpan.Zero, cameraIndex: 1));
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.11.rgba"), renderer.Render(states, [new(3, 2, 0, 0, 0, 0, 0, 2, true)], TimeSpan.Zero, cameraIndex: 1));
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.12.rgba"), new FieldRenderer(classroom, 960, 720).Render(states, [actor], TimeSpan.Zero, cameraIndex: 1));
            File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.13.rgba"), new FieldRenderer(classroom, 960, 720, ModelTextureSampling.Nearest).Render(states, [actor], TimeSpan.Zero, cameraIndex: 1));
            WriteClassroomCheckpointEvidence(classroom, evidence);
            File.WriteAllLines(Path.Combine(evidence, "classroom-standing-actor.txt"), [$"actor11 model10 {student.Name}; texture dimensions={string.Join(",", student.Textures.Select(texture => $"{texture.Width}x{texture.Height}"))}", $"animation4/frame0 root={student.Animations[4].Frames[0].Translation}; posedZ={pose.Min(point => point.Z):F3}..{pose.Max(point => point.Z):F3}; floor={floor}", $"actor11 projected origin={classroom.GetCamera(1).Project(new Vector3(1432, -3080, 0))}; elevated origin={classroom.GetCamera(1).Project(new Vector3(1432, -3080, 242))}", $"actor3 projected origin={classroom.GetCamera(1).Project(Vector3.Zero)}", $"infirmary Squall model1 textures={string.Join(",", game.LoadField(180).Models[1].Textures.Select(texture => $"{texture.Width}x{texture.Height}"))}"]);
        }
    }

    private static void WriteClassroomCheckpointEvidence(FieldAssets classroom, string evidence)
    {
        string path = Path.GetFullPath(TestResources.FixturePath("Assets.Tests.SelectedAssetTests.1"));
        using JsonDocument checkpoint = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement events = checkpoint.RootElement.GetProperty("EventState");
        static Vector3 ReadVector(JsonElement value) => new(value.GetProperty("X").GetSingle(), value.GetProperty("Y").GetSingle(), value.GetProperty("Z").GetSingle());
        ActorVisual[] actors = events.GetProperty("Actors").EnumerateArray().Select(actor =>
        {
            Vector3 position = ReadVector(actor) + ReadVector(actor.GetProperty("DisplayOffset"));
            JsonElement control = actor.GetProperty("FirstControlRotation");
            return new ActorVisual(actor.GetProperty("ObjectId").GetInt32(), actor.GetProperty("ModelId").GetInt32(), position.X, position.Y, position.Z, actor.GetProperty("Direction").GetSingle(), actor.GetProperty("AnimationId").GetInt32(), actor.GetProperty("AnimationFrame").GetSingle(), actor.GetProperty("Active").GetBoolean() && actor.GetProperty("Visible").GetBoolean(), FirstControlRotation: control.ValueKind == JsonValueKind.Null ? null : ReadVector(control), PolygonColor: ReadVector(actor.GetProperty("PolygonColor")));
        }).ToArray();
        Dictionary<int, int> states = events.GetProperty("BackgroundStates").EnumerateObject().ToDictionary(property => int.Parse(property.Name), property => property.Value.GetInt32());
        Dictionary<int, Vector3> shades = events.GetProperty("Scene").GetProperty("BackgroundShadeLoops").EnumerateObject().ToDictionary(property => int.Parse(property.Name), property => ReadVector(property.Value.GetProperty("CurrentColor")));
        File.WriteAllBytes(TestResources.ArtifactFile("Assets.Tests.SelectedAssetTests.14.rgba"), new FieldRenderer(classroom, 960, 720).Render(states, actors, TimeSpan.Zero, cameraIndex: 1, backgroundModuleShades: shades));
    }

    [Fact]
    public void HighResolutionModelFilteringPreservesCutoutsAndWorldDepth()
    {
        PixelImage texture = new(2, 2, [new(240, 0, 0), new(0, 240, 0), new(0, 0, 240), new(240, 240, 240)]);
        FieldModel model = new("sampling-fixture", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 1, 2], [Vector2.One, Vector2.One, Vector2.One], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        PixelImage background = new(16, 16, Enumerable.Repeat(new Rgba32(100, 0, 0), 256).ToArray());
        FieldAssets field = new(0, "sampling-fixture", [], [], [new(0, 0, 26, 255, 0, 0, MapBlendMode.None, false, true, background)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
        ActorVisual[] actors = [new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128))];
        Dictionary<int, int> states = [];
        int sample = (363 * 960 + 483) * 4;
        byte[] nearest = new FieldRenderer(field, 960, 720, ModelTextureSampling.Nearest).Render(states, actors, TimeSpan.Zero);
        byte[] filtered = new FieldRenderer(field, 960, 720).Render(states, actors, TimeSpan.Zero);
        Assert.Equal(new byte[] { 240, 240, 240, 255 }, nearest.AsSpan(sample, 4).ToArray());
        Assert.Equal(new byte[] { 120, 120, 120, 255 }, filtered.AsSpan(sample, 4).ToArray());
        // Transparent source RGB must not form dark/colored halos, or fill a native cutout.
        texture[0, 0] = new Rgba32(255, 0, 255, 0);
        texture[1, 0] = new Rgba32(255, 0, 255, 0);
        texture[0, 1] = new Rgba32(255, 0, 255, 0);
        filtered = new FieldRenderer(field, 960, 720).Render(states, actors, TimeSpan.Zero);
        Assert.Equal(new byte[] { 240, 240, 240, 255 }, filtered.AsSpan(sample, 4).ToArray());
        texture[1, 1] = new Rgba32(255, 0, 255, 0);
        filtered = new FieldRenderer(field, 960, 720).Render(states, actors, TimeSpan.Zero);
        Assert.Equal(new byte[] { 100, 0, 0, 255 }, filtered.AsSpan(sample, 4).ToArray());
        texture[1, 1] = new Rgba32(240, 240, 240);
        field = field with { Background = [field.Background[0] with { Z = 24 }] };
        filtered = new FieldRenderer(field, 960, 720).Render(states, actors, TimeSpan.Zero);
        Assert.Equal(new byte[] { 100, 0, 0, 255 }, filtered.AsSpan(sample, 4).ToArray());
    }

    [Fact]
    public void DeclaredControlBoneMatchesNativeAnchorAndMovesOnlyItsDescendants()
    {
        using GameAssets game = new(GameDirectory);
        FieldModel squall = game.LoadField(180).Models[1];
        Assert.Equal(new ModelControlBones(4, 1, 24, 0x01180104), squall.ControlBones);
        Vector3 declared = squall.ReadFirstControlRotation(5, 33);
        Assert.Equal(0, declared.X);
        Assert.Equal(3748 * MathF.Tau / 4096, declared.Y, 5);
        Assert.Equal(0, declared.Z);
        Assert.NotEqual(squall.Animations[5].Frames[33].Rotations[0], declared);
        // Native first-control anchor camera (25,63,749) minus pre-root (24,104,790),
        // inverse camera and raw root Z146 gives approximately 201.97 (integer rounding).
        Assert.InRange(squall.GetFirstControlBoneHeight(5, 33), 201.2f, 202.8f);
        Assert.Equal(squall.Pose(5, 33), squall.Pose(5, 33, declared));
        FieldAssets classroom = game.LoadField(232);
        Assert.Equal(16, classroom.Models.Count);
        Assert.Equal(4, classroom.Models[0].ControlBones.First);
        Assert.Equal(6, classroom.Models[8].ControlBones.First);
        Assert.Equal(4, classroom.Models[15].ControlBones.First);
        Assert.InRange(classroom.Models[0].GetFirstControlBoneHeight(0, 0), 292.9f, 293.1f);
        Assert.InRange(classroom.Models[8].GetFirstControlBoneHeight(0, 0), 307.4f, 307.7f);
        foreach (int id in new[] { 180, 181 })
        {
            foreach (FieldModel model in game.LoadField(id).Models.Where(model => model.Bones.Count > 0))
            {
                Assert.Equal(model.Pose(0, 0), model.Pose(0, 0, null, 0));
            }
        }

        FieldModel fixture = new("control-hierarchy", [new(-1, -10), new(0, -20), new(1, -5)], [new(Vector3.Zero, 0), new(Vector3.Zero, 1), new(Vector3.Zero, 2)], [], [], [new([new(new Vector3(1, 2, 3), [Vector3.Zero, Vector3.Zero, Vector3.Zero])])]) { ControlBones = new(1, -1, -1, 1) };
        Vector3[] before = fixture.Pose(0, 0);
        Vector3[] after = fixture.Pose(0, 0, new Vector3(0, MathF.PI / 2, 0));
        Assert.Equal(before[0], after[0]);
        Assert.Equal(before[1], after[1]);
        Assert.True(Vector3.Distance(new Vector3(-9, 2, 23), after[2]) < 0.001f);
        Assert.True(Vector3.Distance(new Vector3(-29, 2, 3), before[2]) < 0.001f);
        Assert.Equal(3, fixture.GetFirstControlBoneHeight(0, 0), 3);
        Assert.Equal(245, fixture.GetFirstControlBoneHeight(0, 0, 242), 3);

        string? evidence = Environment.GetEnvironmentVariable("FF8_RENDER_EVIDENCE");
        if (evidence is not null)
        {
            File.WriteAllLines(Path.Combine(evidence, "classroom-control-bones.txt"), classroom.Models.Select((model, index) => $"{index}: {model.Name}, bones={model.Bones.Count}, control={model.ControlBones}, animation0/frame0 anchorZ={((uint)model.ControlBones.First < model.Bones.Count ? model.GetFirstControlBoneHeight(0, 0).ToString("F3") : "unavailable")}"));
            FieldModel student = classroom.Models[14];
            File.AppendAllLines(Path.Combine(evidence, "classroom-control-bones.txt"), Enumerable.Range(0, Math.Min(3, student.Animations[0].Frames.Count)).Select(frame => $"actor15/model14 animation0/frame{frame}: rootNative={student.Animations[0].Frames[frame].Rotations[0] * (4096 / MathF.Tau)}, controlNative={student.ReadFirstControlRotation(0, frame) * (4096 / MathF.Tau)}"));
        }
    }

    [Fact]
    public void CompoundBoneRotationMatchesCapturedOriginalMatrix()
    {
        // Native 0x533F90 output captured before hierarchy composition, Euler (2828,840,204).
        Vector3 angles = new Vector3(2828, 840, 204) * (MathF.Tau / 4096);
        FieldModel model = new("native-compound", [new(-1, -58), new(0, -20)], [new(Vector3.UnitX, 0), new(Vector3.UnitY, 0), new(Vector3.UnitZ, 0), new(Vector3.Zero, 1)], [], [], [new([new(Vector3.Zero, [angles, Vector3.Zero])])]);
        Vector3[] pose = model.Pose(0, 0);
        Vector3[] expected = [new(195, -3943, -1086), new(-4068, -298, 351), new(-417, 1062, -3934)];
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(Vector3.Distance(expected[i] / 4096, pose[i]) < 0.002f);
        }
        Assert.True(Vector3.Distance(pose[2] * -58, pose[3]) < 0.001f);
    }

    [Fact]
    public void BackgroundOrderingDepthUsesTheSameUnitsAsModelCameraDepth()
    {
        PixelImage background = new(16, 16, Enumerable.Repeat(new Rgba32(200, 0, 0), 256).ToArray());
        PixelImage texture = new(16, 16, Enumerable.Repeat(new Rgba32(0, 0, 200), 256).ToArray());
        FieldModel model = new("triangle", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = new(0, "test", [], [], [new(0, 0, 26, 255, 0, 0, MapBlendMode.None, false, true, background)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
        ActorVisual[] actors = [new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128))];
        int sample = (121 * 320 + 161) * 4;
        byte[] pixels = new FieldRenderer(field).Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
        Assert.Equal(200, pixels[sample + 2]);
        field = field with { Background = [field.Background[0] with { Z = 24 }] };
        pixels = new FieldRenderer(field).Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
        Assert.Equal(200, pixels[sample]);
        Assert.Equal(0, pixels[sample + 2]);
    }

    [Fact]
    public void WideRenderTargetFitsOriginalAspectWithBlackMargins()
    {
        PixelImage tile = new(16, 16, Enumerable.Repeat(new Rgba32(255, 200, 100), 256).ToArray());
        FieldAssets field = new(0, "test", [], [], [new(-160, -120, 100, 255, 0, 0, MapBlendMode.None, false, true, tile)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 343), new FieldWalkmesh([]), []);
        byte[] pixels = new FieldRenderer(field, 640, 360).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(640 * 360 * 4, pixels.Length);
        Assert.Equal(0, pixels[(0 * 640 + 79) * 4]);
        Assert.Equal(255, pixels[(0 * 640 + 80) * 4]);
        Assert.Equal(255, pixels[(12 * 640 + 103) * 4]);
        Assert.Equal(0, pixels[(13 * 640 + 103) * 4]);
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(960, 720)]
    [InlineData(641, 361)]
    [InlineData(97, 121)]
    public void ScaledBackgroundRunsPreserveSamplesTransparencyBlendingAndDynamicState(int width, int height)
    {
        PixelImage first = new(3, 2, [new(20, 40, 60), new(200, 10, 90), new(30, 250, 80), new(5, 9, 13), new(180, 160, 140), new(0, 0, 0, 0)]);
        PixelImage second = new(2, 3, [new(50, 100, 200), new(0, 0, 0, 0), new(240, 60, 120), new(30, 90, 150), new(70, 140, 210, 128), new(255, 255, 255)]);
        foreach (MapBlendMode blend in Enum.GetValues<MapBlendMode>())
        {
            FieldAssets field = new(0, "background-run-fixture", [], [], [new(-161, -121, 100, 255, 0, 0, MapBlendMode.None, false, true, first), new(-157, -118, 50, 1, 0, 1, blend, true, true, second), new(-155, -116, 40, 1, 1, 1, blend, true, true, second)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
            FieldRenderer renderer = new(field, width, height);
            Dictionary<int, int> states = new() { [1] = 0 };
            Dictionary<int, Vector3> shades = new() { [1] = new(64, 192, 256) };
            Vector3 backgroundShade = new(256, 128, 32);
            foreach (int state in new[] { 0, 1, 0 })
            {
                states[1] = state;
                foreach (bool hidden in new[] { false, true })
                {
                    HashSet<int> hiddenModules = hidden ? [1] : [];
                    byte[] expected = RenderScalarBackground(field, width, height, states, backgroundShade, shades, hiddenModules);
                    Assert.Equal(expected, renderer.Render(states, [], TimeSpan.Zero, backgroundShade, backgroundModuleShades: shades, hiddenBackgroundModules: hiddenModules));
                }
                shades[1] = new(128);
            }
            Assert.All(renderer.Render(states, [], TimeSpan.Zero, backgroundVisible: false).Where((_, index) => index % 4 != 3), value => Assert.Equal(0, value));
        }
    }

    private static byte[] RenderScalarBackground(FieldAssets field, int width, int height, IReadOnlyDictionary<int, int> states, Vector3 backgroundShade, IReadOnlyDictionary<int, Vector3> shades, IReadOnlySet<int> hiddenModules)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }
        float scale = Math.Min(width / 320f, height / 240f);
        float offsetX = (width - 320 * scale) / 2;
        float offsetY = (height - 240 * scale) / 2;
        float verticalScale = 240 * scale / 224;
        foreach (BackgroundTile tile in field.Background.OrderByDescending(tile => tile.Z))
        {
            if (!tile.Draw || hiddenModules.Contains(tile.AnimationId) || (tile.AnimationId != 255 && tile.AnimationState != states.GetValueOrDefault(tile.AnimationId)))
            {
                continue;
            }
            Vector3 shade = tile.AnimationId != 255 && shades.TryGetValue(tile.AnimationId, out Vector3 moduleShade) ? moduleShade : backgroundShade;
            int left = Math.Clamp((int)MathF.Floor(offsetX + (tile.X + 160) * scale), 0, width);
            int top = Math.Clamp((int)MathF.Floor(offsetY + (tile.Y + 112) * verticalScale), 0, height);
            int right = Math.Clamp((int)MathF.Ceiling(offsetX + (tile.X + 176) * scale), 0, width);
            int bottom = Math.Clamp((int)MathF.Ceiling(offsetY + (tile.Y + 128) * verticalScale), 0, height);
            for (int y = top; y < bottom; y++)
            {
                float logicalY = (y + 0.5f - offsetY) / verticalScale - tile.Y - 112;
                for (int x = left; x < right; x++)
                {
                    if (x + 0.5f < offsetX || x + 0.5f >= offsetX + 320 * scale || y + 0.5f < offsetY || y + 0.5f >= offsetY + 240 * scale)
                    {
                        continue;
                    }
                    float logicalX = (x + 0.5f - offsetX) / scale - tile.X - 160;
                    if (logicalX < 0 || logicalX >= 16 || logicalY < 0 || logicalY >= 16)
                    {
                        continue;
                    }
                    Rgba32 source = tile.Image[Math.Clamp((int)(logicalX * tile.Image.Width / 16), 0, tile.Image.Width - 1), Math.Clamp((int)(logicalY * tile.Image.Height / 16), 0, tile.Image.Height - 1)];
                    if (source.A == 0)
                    {
                        continue;
                    }
                    byte[] color = [(byte)Math.Clamp(source.R * shade.X / 128, 0, 255), (byte)Math.Clamp(source.G * shade.Y / 128, 0, 255), (byte)Math.Clamp(source.B * shade.Z / 128, 0, 255)];
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int index = (y * width + x) * 4 + channel;
                        int value = (tile.Blend && source.A == 128 ? tile.BlendMode : MapBlendMode.None) switch
                        {
                            MapBlendMode.Average => (pixels[index] + color[channel]) / 2,
                            MapBlendMode.Add => pixels[index] + color[channel],
                            MapBlendMode.Subtract => pixels[index] - color[channel],
                            MapBlendMode.QuarterAdd => pixels[index] + color[channel] / 4,
                            _ => color[channel]
                        };
                        pixels[index] = (byte)Math.Clamp(value, 0, 255);
                    }
                }
            }
        }
        return pixels;
    }
}
