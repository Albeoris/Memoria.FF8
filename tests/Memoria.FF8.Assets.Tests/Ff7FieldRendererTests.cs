using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldRendererTests
{
    [Theory]
    [InlineData(640, 240, -480, 480, 0, 0, 0, 640)]
    [InlineData(640, 240, -480, 480, 400, 160, 0, 640)]
    [InlineData(640, 240, -480, 480, -400, -160, 0, 640)]
    [InlineData(640, 240, -160, 160, 400, 0, 160, 480)]
    [InlineData(640, 240, -160, 160, -400, 0, 160, 480)]
    [InlineData(640, 240, -100, 220, 400, -60, 160, 480)]
    [InlineData(1280, 720, -160, 160, 400, 0, 160, 1120)]
    [InlineData(1915, 969, -160, 160, -400, 0, 311.5f, 1603.5f)]
    [InlineData(320, 240, -480, 480, 400, 400, 0, 320)]
    public void WidescreenStopsAtCameraBoundsAndCentersNarrowImages(int width, int height, short left, short right, float scroll, float expectedScroll, float clipLeft, float clipRight)
    {
        PcFieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0, new Vector3(0, 0, 1000), 0, 100);
        PcFieldTriggers triggers = new("viewport", 0, 0, left, -120, right, 120, [], [], [], []);
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcFieldBackground background = new(320, 240, [0], [[tile]], [new(0, 0, 1, new byte[256 * 256])]);
        PcFieldPalettes palettes = new(0, 0, 1, [[0x7FFF]]);
        PcFieldModelDescriptor descriptor = new("actor", 0, "actor.hrc", "512", Enumerable.Repeat(new PcFieldLight(0, Vector3.Zero), 3).ToArray(), 0, ["pose"]);
        Ff7FieldResources field = new("viewport", null!, null!, new PcFieldModels(0, 512, [descriptor]), [camera], [], triggers, palettes, background);
        Ff7FieldModelResources model = new("actor.hrc", null!, [], new Dictionary<string, PcFieldAnimation>());
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = model }, new(50, 32000), new ViewportAppearance());
        Albeoris.Games.FF8.Imaging.PixelImage image = new(128, 16);
        Ff7FieldRenderFrame frame = new(0, new Vector2(scroll, 7), [new(0, 0, 0, 0, 0, 0, 0, 0)], new ushort[256]) { PlayerPointer = new(image, new Vector2(160 + scroll, 127), false, false), QuadColorArgb = 0x80000000 };
        Capture capture = new();
        renderer.Render(capture, width, height, frame);
        float scale = height / 240f;
        Vector2 origin = new(width / 2f + expectedScroll * scale, 127 * scale);
        Assert.Equal(origin, capture.Positions[0]);
        Assert.Equal(origin, capture.Positions[6]);
        Assert.Equal(origin, capture.Positions[^6]);
        Assert.All(capture.Clips.Take(capture.Count - 2), clip => Assert.Equal(new Vector4(clipLeft, height * 8f / 240, clipRight, height * 232f / 240), clip));
        Assert.Equal(new Vector2(scroll, 7), frame.CameraOffsetPixels);

        Capture movie = new();
        renderer.Render(movie, width, height, frame with { MovieActive = true });
        Assert.Equal(new Vector2(width / 2f + scroll * scale, 127 * scale), movie.Positions[0]);
        Assert.Equal(new Vector4(0, height * 8f / 240, width, height * 232f / 240), movie.Clips[0]);

        foreach (bool movieActive in new[] { false, true })
        {
            Capture hidden = new();
            renderer.Render(hidden, width, height, frame with { FieldHidden = true, MovieActive = movieActive, DrawBackgroundDuringMovie = true });
            Assert.True(hidden.BeginCalled && hidden.EndCalled);
            Assert.Equal(new Vector4(0, 0, 0, movieActive ? 0 : 1), hidden.ClearColor);
            Assert.Equal(0, hidden.Count);
        }
    }

    private sealed class ViewportAppearance : IFf7ActorAppearance
    {
        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            triangles = [new(new(Vector3.Zero, Vector2.Zero, Vector4.One), new(Vector3.UnitX, Vector2.Zero, Vector4.One), new(Vector3.UnitY, Vector2.Zero, Vector4.One), new TriangleMaterial(null))];
            return true;
        }
    }

    [Theory]
    [InlineData("nrthmk")]
    [InlineData("md1stin")]
    [InlineData("tin_1")]
    public void InstalledFieldCameraEdgesUseTriggerBoundsInsteadOfBackgroundTileExtents(string fieldName)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original widescreen camera bounds.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), fieldName);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        PcFieldTile first = field.Background.Layers.SelectMany(layer => layer).First(tile => tile.Blending == 0 && (tile.Layer == 0 || tile.State == 0));
        float imageWidth = field.Triggers.CameraRight - field.Triggers.CameraLeft;
        foreach (float scroll in new[] { -10000f, 0, 10000f })
        {
            Capture capture = new();
            renderer.Render(capture, 640, 240, new(0, new Vector2(scroll, 0), [], new ushort[256]));
            float imageLeft = imageWidth < 640 ? (640 - imageWidth) / 2 : Math.Clamp(320 + field.Triggers.CameraLeft + scroll, 640 - imageWidth, 0);
            Assert.Equal(imageLeft + first.DestinationX - field.Triggers.CameraLeft, capture.Positions[0].X, 3);
            Assert.All(capture.Clips, clip => Assert.Equal(new Vector4(Math.Max(0, imageLeft), 8, Math.Min(640, imageLeft + imageWidth), 232), clip));
        }
    }

    [Fact]
    public void ReplacementRequiresExplicitNativePartOwnershipAndRetainsColorEffectRejection()
    {
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, new Vector3(0, 0, 1000), 0, 100);
        PcFieldModelDescriptor descriptor = new("actor", 0, "actor.hrc", "512", Enumerable.Repeat(new PcFieldLight(0, Vector3.Zero), 3).ToArray(), 0, ["pose"]);
        Ff7FieldResources field = new("part-mask-fixture", null!, null!, new PcFieldModels(0, 512, [descriptor]), [camera], [], null!, null!, null!);
        Ff7FieldModelResources model = new("actor.hrc", null!, [], new Dictionary<string, PcFieldAnimation>());
        Dictionary<int, Ff7FieldModelResources> models = new() { [0] = model };
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0) { ModelEffects = new(_ => false, null, false) };
        Ff7FieldRenderFrame Frame(Ff7ActorVisual selected) => new(0, Vector2.Zero, [selected], new ushort[256], MovieActive: true);
        Ff7FieldRenderer unsafeAppearance = new(field, models, new(50, 32000), new VisibilityAppearance(false));
        Assert.Throws<NotSupportedException>(() => unsafeAppearance.Render(new Capture(), 320, 240, Frame(actor)));
        Ff7FieldRenderer declaredAppearance = new(field, models, new(50, 32000), new VisibilityAppearance(true));
        declaredAppearance.Render(new Capture(), 320, 240, Frame(actor));
        Assert.Throws<NotSupportedException>(() => declaredAppearance.Render(new Capture(), 320, 240, Frame(actor with { ModelEffects = new(_ => true, (_, _, color) => color, false) })));
        Assert.Throws<NotSupportedException>(() => declaredAppearance.Render(new Capture(), 320, 240, Frame(actor with { ModelEffects = new(_ => true, null, true) })));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PlayerPointerRetainsNativeUvAndUsesTheSharedUnmaskedHud(bool flipX, bool flipY)
    {
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        Ff7FieldResources field = new("pointer-fixture", null!, null!, null!, [camera], [], null!, null!, null!);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Albeoris.Games.FF8.Imaging.PixelImage image = new(128, 16);
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256], MovieActive: true) { PlayerPointer = new(image, new Vector2(160, 1), flipX, flipY) };
        Capture capture = new();
        renderer.Render(capture, 1280, 720, frame);
        Assert.Equal(2, capture.Count);
        Assert.Equal(new Vector2(640, 3), capture.Positions[0]);
        Assert.Equal(new Vector2(685, 3), capture.Positions[1]);
        Assert.All(capture.Materials, material =>
        {
            Assert.Same(image, material.Texture);
            Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.Equal(new Vector4(0, 0, 1280, 720), material.ClipRectangle);
        });
        float v = BitConverter.Int32BitsToSingle(0x3F6F1AA0);
        Assert.Equal(new Vector2(flipX ? 0.125f : 0, flipY ? v : 0), capture.Uvs[0]);
        Assert.Equal(new Vector2(flipX ? 0 : 0.125f, flipY ? v : 0), capture.Uvs[1]);
        Assert.Equal(new Vector2(flipX ? 0.125f : 0, flipY ? 0 : v), capture.Uvs[2]);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(2560, 1080)]
    [InlineData(601, 451)]
    [InlineData(480, 800)]
    public void ArbitraryViewportKeepsNativeActorAndTileScaleAndCameraCenter(int width, int height)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for field viewport submissions.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        var resources = new Dictionary<int, Ff7FieldModelResources> { [0] = Ff7FieldModelResources.Load(characters, field.Models.Models[0]) };
        Ff7FieldRenderer renderer = new(field, resources, new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, new Vector2(12, -7), [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256]);
        Capture native = new();
        Capture resized = new();
        renderer.Render(native, 320, 240, frame);
        renderer.Render(resized, width, height, frame);
        Assert.Equal(native.Count, resized.Count);
        Assert.True(native.ModelTriangles > 0 && native.Count > native.ModelTriangles);
        float scale = height / 240f;
        float offset = (width - 320 * scale) / 2;
        float clipLeft = 0;
        float clipRight = width;
        if (width > 320 * scale)
        {
            float imageWidth = (field.Triggers.CameraRight - field.Triggers.CameraLeft) * scale;
            float imageLeft = offset + (160 + field.Triggers.CameraLeft + frame.CameraOffsetPixels.X) * scale;
            float placedLeft = imageWidth >= width ? Math.Clamp(imageLeft, width - imageWidth, 0) : (width - imageWidth) / 2;
            offset += placedLeft - imageLeft;
            clipLeft = Math.Max(0, placedLeft);
            clipRight = Math.Min(width, placedLeft + imageWidth);
        }
        for (int index = 0; index < native.Positions.Count; index++)
        {
            Assert.InRange(Math.Abs(resized.Positions[index].X - (offset + native.Positions[index].X * scale)), 0, 0.001f);
            Assert.InRange(Math.Abs(resized.Positions[index].Y - native.Positions[index].Y * scale), 0, 0.001f);
        }
        Assert.All(resized.Clips, clip => Assert.Equal(new Vector4(clipLeft, height * 8f / 240, clipRight, height * 232f / 240), clip));
        Capture restored = new();
        renderer.Render(restored, 320, 240, frame);
        Assert.Equal(native.Positions, restored.Positions);
        Assert.Equal(native.Depths, restored.Depths);
    }

    [Fact]
    public void BiggsEyeDisableRetainsOriginalTexturedHeadSubmissions()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set installed resources for the reached eye-disable render contract.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "tin_1");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, field.Models.Models[3]);
        Assert.Equal("BWFD.hrc", model.HrcName, ignoreCase: true);
        Ff7BoneGeometry head = model.Geometry.Single(part => model.Hierarchy.Bones[part.BoneIndex].Name == "head");
        Assert.Equal(3, head.Textures.Count);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [3] = model }, new(50, 32000));
        Ff7ActorVisual actor = new(33, 3, 0, 0, 0, 0, 0, 0);
        Capture before = new();
        Capture after = new();
        renderer.Render(before, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256], MovieActive: true));
        renderer.Render(after, 320, 240, new(0, Vector2.Zero, [actor with { AutomaticBlinkDisabled = true }], new ushort[256], MovieActive: true));
        Assert.True(before.ModelTriangles > 0);
        Assert.Contains(before.Materials, material => material.Texture is not null);
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.Positions, after.Positions);
        Assert.Equal(before.Colors, after.Colors);
    }

    [Fact]
    public void BiggsNativeBlinkUsesSelectedFieldArchiveAlternatesAndNativeFactoryMaterial()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set installed resources for original native blink submissions.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "tin_1");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, field.Models.Models[3], executable, fields);
        Assert.NotNull(model.Eyes?.ClosedLeft);
        Assert.NotNull(model.Eyes?.ClosedRight);
        var left = Ff7FieldRenderer.DecodeTexture(model.Eyes!.ClosedLeft!);
        var right = Ff7FieldRenderer.DecodeTexture(model.Eyes.ClosedRight!);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [3] = model }, new(50, 32000));
        Ff7ActorVisual actor = new(33, 3, 0, 0, 0, 0, 0, 0);
        Capture opened = new();
        Capture closed = new();
        renderer.Render(opened, 320, 240, new(0, Vector2.Zero, [actor with { NativeEyeLeft = 1, NativeEyeRight = 1 }], new ushort[256], MovieActive: true));
        renderer.Render(closed, 320, 240, new(0, Vector2.Zero, [actor with { NativeEyeLeft = 2, NativeEyeRight = 2 }], new ushort[256], MovieActive: true));
        Assert.Equal(opened.Positions, closed.Positions);
        Assert.Contains(closed.Materials, material => material.Texture is not null && material.Texture.Pixels.SequenceEqual(left.Pixels));
        Assert.Contains(closed.Materials, material => material.Texture is not null && material.Texture.Pixels.SequenceEqual(right.Pixels));
        Assert.Equal(0x20002u, Ff7FieldEyeResources.AlternateMaterial.Features);
        Assert.Equal(1u, Ff7FieldEyeResources.AlternateMaterial.ShadeMode);
        Ff7FieldEyeResources absentLeft = model.Eyes with { ClosedLeft = null };
        Assert.Null(absentLeft.Alternate(absentLeft.HeadPart, absentLeft.FirstGroup + 1, 2, 2));
        Assert.Null(model.Eyes.Alternate(model.Eyes.HeadPart, model.Eyes.FirstGroup, 1, 1));
    }

    [Fact]
    public void NativeOrdinalChannelsScaleOnlyAliasedTranslationsAndRejectAllocationOverread()
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes);
        writer.Write(1);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0x020001);
        writer.Write(new byte[20]);
        foreach (Vector3 vector in new[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6), new Vector3(7, 8, 9), new Vector3(10, 11, 12), new Vector3(13, 14, 15), new Vector3(16, 17, 18) })
        {
            writer.Write(vector.X);
            writer.Write(vector.Y);
            writer.Write(vector.Z);
        }
        PcFieldAnimation animation = PcFieldAnimation.Read(bytes.ToArray());
        Assert.Equal(new Vector3(7, 8, 9), animation.ReadNativeBoneRotation(0, 0, float.NaN));
        Assert.Equal(new Vector3(10, 11, 12), animation.ReadNativeBoneRotation(0, 1, 2));
        Assert.Equal(new Vector3(26, 28, 30), animation.ReadNativeBoneRotation(0, 2, 2));
        Assert.Equal(new Vector3(16, 17, 18), animation.ReadNativeBoneRotation(0, 3, 2));
        Assert.Throws<NotSupportedException>(() => animation.ReadNativeBoneRotation(0, 2, float.NaN));
        Assert.Throws<NotSupportedException>(() => animation.ReadNativeBoneRotation(1, 1, 2));
    }

    [Fact]
    public void ModelDirectoryKeepsUnboundClipsAndSelectedPoseUsesBoundedNativeChannels()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original selected-animation binding.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "rootmap");
        PcFieldModelDescriptor descriptor = field.Models.Models[0];
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, descriptor);
        Assert.Equal(23, model.Hierarchy.DeclaredBoneCount);
        Assert.Equal(21, model.Animations["AAFF.a"].BoneCount);
        Assert.Equal(21, model.Animations["CMDC.a"].BoneCount);
        foreach (string name in new[] { "CMDA.a", "CMDB.a", "CMDD.a" })
        {
            PcFieldAnimation clip = model.Animations[name];
            for (int frame = 0; frame < clip.Frames.Count; frame++)
            {
                IReadOnlyList<Matrix4x4> pose = Ff7FieldRenderer.Pose(model, clip, frame);
                Assert.Equal(23, pose.Count);
                for (int bone = 0; bone < clip.BoneCount; bone++)
                {
                    Assert.Equal(clip.Frames[frame].BoneRotations[bone], clip.ReadNativeBoneRotation(frame, bone, float.NaN));
                }
                foreach (Ff7BoneGeometry part in model.Geometry)
                {
                    Assert.All(part.Polygon.Vertices, vertex =>
                    {
                        Vector3 point = Vector3.Transform(vertex, pose[part.BoneIndex]);
                        Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
                    });
                }
            }
        }
        float nativeScale = Ff7FieldRenderer.ModelBasis(descriptor).M11;
        foreach (string name in new[] { "AAFF.a", "CMDC.a" })
        {
            PcFieldAnimation clip = model.Animations[name];
            for (int frame = 0; frame < clip.Frames.Count - 1; frame++)
            {
                Assert.Equal(clip.Frames[frame].BoneRotations[20], clip.ReadNativeBoneRotation(frame, 20, nativeScale));
                Assert.Equal(clip.Frames[frame + 1].RootRotation, clip.ReadNativeBoneRotation(frame, 21, nativeScale));
                Assert.Equal(clip.Frames[frame + 1].RootTranslation * nativeScale, clip.ReadNativeBoneRotation(frame, 22, nativeScale));
                Assert.Equal(23, Ff7FieldRenderer.Pose(model, clip, frame, nativeScale).Count);
            }
            Assert.Throws<NotSupportedException>(() => Ff7FieldRenderer.Pose(model, clip, 0));
            Assert.Throws<NotSupportedException>(() => Ff7FieldRenderer.Pose(model, clip, clip.Frames.Count - 1, nativeScale));
        }
        Assert.Equal(38.03862762451172f, Ff7FieldRenderer.ModelBasis(descriptor).M11);
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(960, 720)]
    public void NativeBlackFadeUsesTheSharedAlphaQuadAfterFieldGeometry(int width, int height)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field fade submission.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "md1stin");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(1, 10000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256]);
        Capture baseline = new();
        renderer.Render(baseline, width, height, frame);
        Capture faded = new();
        renderer.Render(faded, width, height, frame with { QuadColorArgb = 0x80000000 });
        Assert.Equal(baseline.Count + 2, faded.Count);
        Assert.Equal(baseline.Colors, faded.Colors.Take(baseline.Colors.Count));
        Assert.All(faded.Colors.TakeLast(6), color => Assert.Equal(new Vector4(0, 0, 0, 128 / 255f), color));
        Assert.All(faded.Materials.TakeLast(2), material =>
        {
            Assert.Equal(TriangleBlend.Alpha, material.Blend);
            Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.False(material.PreserveTargetAlpha);
            Assert.Null(material.Texture);
        });
        Assert.Contains(new Vector2(0, height * 8f / 240), faded.Positions.TakeLast(6));
        Assert.Contains(new Vector2(width, height * 232f / 240), faded.Positions.TakeLast(6));
        Capture clear = new();
        renderer.Render(clear, width, height, frame with { QuadColorArgb = 0 });
        Assert.Equal(baseline.Count, clear.Count);
        Capture movie = new();
        renderer.Render(movie, width, height, frame with { MovieActive = true, QuadColorArgb = 0x80000000 });
        Assert.Equal(0, movie.ClearColor.W);
        Assert.Equal(2, movie.Count);
        Assert.All(movie.Materials, material => Assert.False(material.PreserveTargetAlpha));
    }

    [Fact]
    public void NativeAdditiveFadeUsesUnscaledRgbAndKeepsTheUnderlyingMovieAlpha()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field fade submission.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "tin_1");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(1, 10000));
        foreach (bool movieActive in new[] { false, true })
        {
            Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256], MovieActive: movieActive);
            Capture baseline = new();
            renderer.Render(baseline, 320, 240, frame);
            Capture faded = new();
            renderer.Render(faded, 320, 240, frame with { QuadColorArgb = 0x80140000, QuadBlend = TriangleBlend.Add });
            Assert.Equal(baseline.Count + 2, faded.Count);
            Assert.Equal(baseline.Colors, faded.Colors.Take(baseline.Colors.Count));
            Assert.All(faded.Colors.TakeLast(6), color => Assert.Equal(new Vector4(20 / 255f, 0, 0, 128 / 255f), color));
            Assert.All(faded.Materials.TakeLast(2), material =>
            {
                Assert.Equal(TriangleBlend.Add, material.Blend);
                Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
                Assert.False(material.DepthWrite);
                Assert.True(material.PreserveTargetAlpha);
                Assert.Null(material.Texture);
            });
            Assert.Equal(movieActive ? 0 : 1, faded.ClearColor.W);
        }
    }

    [Fact]
    public void ActorFailureClosesTheSharedFrameBeforeHostErrorPresentation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field frame cleanup.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "md1stin");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Capture submitted = new();
        Ff7ActorVisual missing = new(0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Throws<NotSupportedException>(() => renderer.Render(submitted, 320, 240, new(0, Vector2.Zero, [missing], new ushort[256], MovieActive: true)));
        Assert.True(submitted.BeginCalled);
        Assert.True(submitted.EndCalled);
    }

    [Fact]
    public void NativeFieldTintUsesSharedVertexColorsAndDoesNotPaintMoviePixels()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field tint submission.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "md1stin");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256]) { FieldColorArgb = 0x80800D0D };
        Capture submitted = new();
        renderer.Render(submitted, 320, 240, frame);
        Assert.NotEmpty(submitted.Colors);
        Assert.All(submitted.Colors, color => Assert.Equal(new Vector4(128 / 255f, 13 / 255f, 13 / 255f, 1), color));
        Capture movie = new();
        renderer.Render(movie, 320, 240, frame with { MovieActive = true });
        Assert.Empty(movie.Colors);
        Assert.Equal(0, movie.ClearColor.W);
    }

    [Theory]
    [InlineData(1u, false)]
    [InlineData(2u, true)]
    public void NativeShadeModePreservesFlatAndGouraudVertexColorOwnership(uint mode, bool interpolated)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for the selected native model pose.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        byte[] state = new byte[100];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(8), 0x20000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(12), 0x20000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(36), mode);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(92), 255);
        PcPolygonModel triangle = new([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ], [], [0xFFFF0000, 0xFF00FF00, 0xFF0000FF], [], [new(0, 0, 1, 2, 0, 1, 2, default)], [state], [new(0, 0, 1, 0, 3, 0, 0, 0, default)], default);
        Ff7FieldModelResources resources = original with { Geometry = [original.Geometry[0] with { Polygon = triangle }] };
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = resources }, new(50, 32000));
        Ff7ActorVisual actor = new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0) { ModelEffects = new(_ => true, null, true) };
        Capture submitted = new();
        renderer.Render(submitted, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256], MovieActive: true));
        Assert.Equal(3, submitted.Colors.Count);
        Assert.Equal(new Vector4(1, 0, 0, 1), submitted.Colors[0]);
        Assert.Equal(interpolated ? new Vector4(0, 1, 0, 1) : submitted.Colors[0], submitted.Colors[1]);
        Assert.Equal(interpolated ? new Vector4(0, 0, 1, 1) : submitted.Colors[0], submitted.Colors[2]);
    }

    [Fact]
    public void ModelRenderOverridesKeepVisibilityAndCachedColorInTheSharedTriangleStream()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original model submission tests.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = model }, new(50, 32000));
        int shaded = 0;
        Ff7ActorVisual actor = new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)
        {
            ModelEffects = new(part => part == 0, (_, _, _) =>
            {
                shaded++;
                return 0xFF800040;
            }, true)
        };
        Capture submitted = new();
        renderer.Render(submitted, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256], MovieActive: true));
        Assert.True(shaded > 0);
        Assert.True(submitted.Count > 0 && submitted.Count < model.Geometry.Sum(part => part.Polygon.Polygons.Count));
        Assert.All(submitted.Colors, color => Assert.Equal(new Vector4(128 / 255f, 0, 64 / 255f, 1), color));
        Assert.All(submitted.Clips, clip => Assert.Equal(new Vector4(0, 8, 320, 232), clip));
        Capture hidden = new();
        renderer.Render(hidden, 320, 240, new(0, Vector2.Zero, [actor with { ModelEffects = new(_ => false, null, true) }], new ushort[256], MovieActive: true));
        Assert.Equal(0, hidden.Count);
    }

    [Fact]
    public void CachedTileReservesPaletteZeroAndPreservesDirectCameraProjection()
    {
        PcFieldTexturePage page = new(0, 0, 1, new byte[65536]);
        PcFieldBackground background = new(320, 240, new byte[] { 1 }, [], new[] { page });
        PcFieldPalettes palettes = new(0, 0, 1, new IReadOnlyList<ushort>[] { new ushort[] { 31 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile);
        Assert.Equal((byte)0, image.Pixels[0].R);
        Assert.Equal((byte)0, image.Pixels[0].A);
        Assert.Equal(0, new Ff7ProjectionDepth(50, 32000).Project(50), 5);
        Assert.Equal(1, new Ff7ProjectionDepth(50, 32000).Project(32000), 5);
    }

    [Fact]
    public void IndexedZeroColorIsTransparentAtEveryPaletteIndexButStpBlackIsOpaque()
    {
        byte[] pixels = new byte[65536];
        pixels[0] = 1;
        pixels[1] = 2;
        PcFieldTexturePage page = new(0, 0, 1, pixels);
        PcFieldBackground background = new(320, 240, new byte[] { 0 }, [], [page]);
        PcFieldPalettes palettes = new(0, 0, 3, new IReadOnlyList<ushort>[] { new ushort[] { 31, 0, 0x8000 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile);
        Assert.Equal((byte)0, image.Pixels[0].A);
        Assert.Equal((byte)255, image.Pixels[1].A);
        Assert.Equal((byte)255, image.Pixels[2].A);
    }

    [Fact]
    public void LiveCanonicalPaletteRowsAreNotExchangedAgainAfterScriptEdits()
    {
        byte[] pixels = new byte[65536];
        pixels[1] = 1;
        PcFieldTexturePage page = new(0, 0, 1, pixels);
        PcFieldBackground background = new(320, 240, new byte[] { 1 }, [], [page]);
        PcFieldPalettes raw = new(0, 0, 3, new IReadOnlyList<ushort>[] { new ushort[] { 31, 0, 0x8000 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        ushort[][] rows = raw.CreateCachedBackgroundRows(background);
        PcFieldPalettes canonical = raw with { Palettes = rows.Select(row => (IReadOnlyList<ushort>)row).ToArray() };
        var initial = Ff7FieldRenderer.DecodeTile(background, raw, tile);
        var live = Ff7FieldRenderer.DecodeTile(background, canonical, tile, paletteRowsUseCachedIndices: true);
        Assert.Equal(initial.Pixels, live.Pixels);
        rows[0][0] = 31;
        rows[0][1] = 0;
        var changed = Ff7FieldRenderer.DecodeTile(background, canonical, tile, paletteRowsUseCachedIndices: true);
        Assert.Equal((byte)255, changed.Pixels[0].A);
        Assert.Equal((byte)255, changed.Pixels[0].R);
        Assert.Equal((byte)0, changed.Pixels[1].A);
    }

    [Fact]
    public void BackgroundAtlasKeepsTileOrderAndReusesPagesUntilPaletteChanges()
    {
        byte[] pixels = new byte[65536];
        pixels[0] = 1;
        pixels[32] = 2;
        PcFieldTexturePage page = new(0, 0, 1, pixels);
        PcFieldTile first = new(0, 0, -20, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcFieldTile second = first with { Index = 1, DestinationX = -10 };
        PcFieldTile blended = first with { Layer = 2, DestinationX = -15, Blending = 1, BlendMode = 1, SourceX2 = 32, State = 1, Z = 4000000 };
        PcFieldTile inactive = blended with { Index = 1, State = 2, TextureId2 = 3 };
        PcFieldBackground background = new(320, 240, new byte[] { 1 }, [[first, second], [], [blended, inactive], []], [page]);
        PcFieldPalettes raw = new(0, 0, 3, new IReadOnlyList<ushort>[] { new ushort[] { 31, 0, 31 << 5 } });
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        Ff7FieldResources field = new("atlas-fixture", null!, null!, null!, [camera], [], null!, raw, background);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        ushort[] parameters = new ushort[256];
        ushort[][] rows = raw.CreateCachedBackgroundRows(background);
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], parameters, PaletteRows: rows);
        Capture hidden = new();
        renderer.Render(hidden, 320, 240, frame);
        Assert.Equal(4, hidden.Count);
        Assert.Same(hidden.Materials[0].Texture, hidden.Materials[2].Texture);

        parameters[0] = 1;
        Capture visible = new();
        renderer.Render(visible, 320, 240, frame);
        Assert.Equal(6, visible.Count);
        var atlas = visible.Materials[0].Texture!;
        Assert.All(visible.Materials, material => Assert.Same(atlas, material.Texture));
        Assert.Equal(new[] { 140f, 150f, 145f }, visible.Positions.Where((_, index) => index % 6 == 0).Select(position => position.X));
        Assert.Equal(new[] { TriangleBlend.Replace, TriangleBlend.Replace, TriangleBlend.Add }, visible.Materials.Where((_, index) => index % 2 == 0).Select(material => material.Blend));
        Assert.Equal(0.4f, visible.Depths[12]);
        Assert.False(visible.Materials[4].DepthWrite);

        PcFieldTile[] orderedTiles = [first, second, blended];
        for (int index = 0; index < orderedTiles.Length; index++)
        {
            PcFieldTile tile = orderedTiles[index];
            Vector4 region = visible.TexelRectangles[index * 2];
            Assert.Equal(tile.PixelSize, region.Z);
            Assert.Equal(tile.PixelSize, region.W);
            var decoded = Ff7FieldRenderer.DecodeTile(background, raw, tile);
            for (int row = 0; row < tile.PixelSize; row++)
            {
                Assert.Equal(decoded.Pixels.AsSpan(row * tile.PixelSize, tile.PixelSize).ToArray(), atlas.Pixels.AsSpan(((int)region.Y + row) * atlas.Width + (int)region.X, tile.PixelSize).ToArray());
            }
        }

        parameters[0] = 0;
        Capture hiddenAgain = new();
        renderer.Render(hiddenAgain, 320, 240, frame);
        Assert.Equal(4, hiddenAgain.Count);
        Assert.All(hiddenAgain.Materials, material => Assert.Same(atlas, material.Texture));

        rows[0][1] = 0;
        Capture changed = new();
        renderer.Render(changed, 320, 240, frame with { PaletteVersion = 1 });
        var updated = changed.Materials[0].Texture!;
        Assert.NotSame(atlas, updated);
        Assert.Equal((byte)255, atlas[0, 0].R);
        Assert.Equal((byte)0, updated[0, 0].A);

        Capture reset = new();
        renderer.Render(reset, 320, 240, frame with { PaletteRows = null });
        Assert.Equal((byte)255, reset.Materials[0].Texture![0, 0].R);
    }

    [Theory]
    [InlineData("md1stin")]
    [InlineData("nmkin_4")]
    [InlineData("md1_1")]
    [InlineData("nrthmk")]
    public void InstalledCachedIndicesUseTheNativeRemappedPaletteColors(string name)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original background checks.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), name);
        int checkedPixels = 0;
        foreach (PcFieldTile tile in field.Background.Layers.SelectMany(layer => layer))
        {
            PcFieldTexturePage page = field.Background.Textures.Single(texture => texture.Id == tile.EffectiveTextureId);
            ushort first = field.Palettes.Palettes[tile.PaletteId][0];
            if (page.Depth == 2 || field.Background.PaletteHasTransparentColor[tile.PaletteId] == 0 || first == 0)
            {
                continue;
            }
            var image = Ff7FieldRenderer.DecodeTile(field.Background, field.Palettes, tile);
            for (int y = 0; y < tile.PixelSize; y++)
            {
                for (int x = 0; x < tile.PixelSize; x++)
                {
                    int index = page.GetIndex(tile.EffectiveSourceX + x, tile.EffectiveSourceY + y);
                    if (index != 0 && field.Palettes.Palettes[tile.PaletteId][index] == 0)
                    {
                        Assert.Equal((byte)255, image[x, y].A);
                        Assert.Equal((byte)(((first & 31) << 3) | ((first & 31) >> 2)), image[x, y].R);
                        Assert.Equal((byte)((((first >> 5) & 31) << 3) | (((first >> 5) & 31) >> 2)), image[x, y].G);
                        Assert.Equal((byte)((((first >> 10) & 31) << 3) | (((first >> 10) & 31) >> 2)), image[x, y].B);
                        checkedPixels++;
                    }
                }
            }
        }
        Assert.True(checkedPixels > 0, "The selected original background must exercise indices whose original transparent color was exchanged with palette zero.");
    }

    [Fact]
    public void OptionalInstalledOpeningSubmitsOriginalLayeredBackgroundAndActorTriangles()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for read-only original render-contract tests.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        var resources = new Dictionary<int, Ff7FieldModelResources> { [0] = Ff7FieldModelResources.Load(characters, field.Models.Models[0]) };
        Ff7FieldRenderer renderer = new(field, resources, new(50, 32000));
        Capture triangles = new();
        renderer.Render(triangles, 320, 240, new(0, Vector2.Zero, [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256]));
        Assert.True(triangles.Count > field.Background.Layers[0].Count * 2);
        Assert.True(triangles.ModelTriangles > 0);
        Assert.Single(triangles.Materials.Where((_, index) => triangles.TexelRectangles[index].Z > 0).Select(material => material.Texture).Distinct());
        Assert.All(triangles.Depths, depth => Assert.True(float.IsFinite(depth)));
        Assert.True(triangles.BeginCalled && triangles.EndCalled);
        Capture movie = new();
        renderer.Render(movie, 320, 240, new(0, Vector2.Zero, [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256], MovieActive: true, CameraOverride: field.Cameras[0]));
        Assert.Equal(0, movie.ClearColor.W);
        Assert.True(movie.ModelTriangles > 0 && movie.Count < triangles.Count);
        Assert.All(movie.PreservesAlpha, preserve => Assert.False(preserve));
        Capture endedCamera = new();
        PcFieldCamera blankCamera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        renderer.Render(endedCamera, 320, 240, new(0, Vector2.Zero, [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256], MovieActive: true, CameraOverride: blankCamera));
        Assert.Equal(0, endedCamera.Count);
        string? report = Environment.GetEnvironmentVariable("MEMORIA_FF7_RENDER_CONTRACT_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { States = resources[0].Geometry.SelectMany(part => part.Polygon.RenderStates.Select(raw => PcRenderState.Read(raw.Span))).Select(state => new { state.Options, state.Features, state.ShadeMode, state.AmbientLight, state.AlphaReference, state.BlendMode, state.SourceBlend, state.DestinationBlend, state.VertexAlpha }).Distinct(), Textures = resources[0].Geometry.SelectMany(part => part.Textures).Select(texture => new { texture.BytesPerPixel, texture.RenderInfo }).Distinct() }));
        }
    }

    private sealed class VisibilityAppearance(bool supportsPartVisibility) : IFf7ActorAppearance
    {
        public bool SupportsPartVisibility => supportsPartVisibility;

        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            triangles = [];
            return true;
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public int Count { get; private set; }
        public int ModelTriangles { get; private set; }
        public bool BeginCalled { get; private set; }
        public bool EndCalled { get; private set; }
        public List<float> Depths { get; } = [];
        public Vector4 ClearColor { get; private set; }
        public List<bool> PreservesAlpha { get; } = [];
        public List<Vector4> Colors { get; } = [];
        public List<Vector4?> Clips { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];
        public List<Vector2> Positions { get; } = [];
        public List<Vector2> Uvs { get; } = [];
        public List<Vector4> TexelRectangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            BeginCalled = true;
            ClearColor = clearColor;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Colors.AddRange([a.Color, b.Color, c.Color]);
            Materials.Add(material);
            TexelRectangles.Add(a.TextureTexelRectangle);
            Positions.AddRange([a.Position, b.Position, c.Position]);
            Uvs.AddRange([a.Uv, b.Uv, c.Uv]);
            Clips.Add(material.ClipRectangle);
            Count++;
            PreservesAlpha.Add(material.PreserveTargetAlpha);
            if (a.ReciprocalW != 1)
            {
                ModelTriangles++;
            }
            Depths.Add(a.Depth);
            Depths.Add(b.Depth);
            Depths.Add(c.Depth);
        }

        public void End()
        {
            EndCalled = true;
        }
    }
}
