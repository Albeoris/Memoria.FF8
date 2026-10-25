using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class NativeStagePageRenderingTests
{
    [Fact]
    public void OverlappingEightBitPagesUseIndependentImagesAndOriginalPageLocalUv()
    {
        using InMemoryResourceSource source = new(new("fixture", GameDataEdition.Classic, ["native-battle-stage-page"]));
        ResourceService resources = new(source);
        BattleStage stage = Stage(resources, source.Descriptor);
        BattleRenderer renderer = new(96, 64);
        BattleCamera camera = new(Vector3.Zero, -Vector3.UnitZ, 1);
        byte[] baseline = renderer.Render(stage, [], camera).ToArray();
        TriangleCapture capture = new();
        renderer.TriangleRenderer = capture;
        renderer.Render(stage, [], camera);
        Vector3[][] originalGeometry = capture.Draws.Select(draw => draw.Positions).ToArray();
        BattleFace[] originalFaces = stage.Meshes.SelectMany(mesh => mesh.Faces).ToArray();
        resources.Prepare(new PageResolver()).Commit();
        renderer.Render(stage, [], camera);
        Assert.Equal(4, capture.Draws.Count);
        Assert.Equal(originalGeometry.Length, capture.Draws.Count);
        for (int index = 0; index < capture.Draws.Count; index++)
        {
            Assert.Equal(originalGeometry[index], capture.Draws[index].Positions);
            Assert.Equal(512, capture.Draws[index].Texture.Width);
            Assert.Equal(index < 2 ? 512 : 510, capture.Draws[index].Texture.Height);
            Assert.Equal(index < 2 ? new Rgba32(220, 30, 40) : new Rgba32(20, 70, 230), capture.Draws[index].Texture[0, 0]);
            Vector2[] expectedUv = index % 2 == 0
                ? [new(10f / 256, 20f / 256), new(10f / 256, 50f / 256), new(40f / 256, 50f / 256)]
                : [new(40f / 256, 50f / 256), new(40f / 256, 20f / 256), new(10f / 256, 20f / 256)];
            Assert.Equal(expectedUv, capture.Draws[index].Uv);
        }
        Assert.Equal(new Vector2(10f / 256, 20f / 256), capture.Draws[0].Uv[0]);
        Assert.Equal(new Vector2(10f / 256, 20f / 256), capture.Draws[2].Uv[0]);
        Assert.Equal(originalFaces, stage.Meshes.SelectMany(mesh => mesh.Faces));
        renderer.TriangleRenderer = null;
        byte[] selected = renderer.Render(stage, [], camera).ToArray();
        Assert.NotEqual(baseline, selected);
        resources.Prepare(null).Commit();
        Assert.Equal(baseline, renderer.Render(stage, [], camera));
    }

    [Fact]
    public void AnimationBoundariesLoopAndRestoredClockRenderDeterministically()
    {
        using InMemoryResourceSource source = new(new("fixture", GameDataEdition.Classic, ["native-battle-stage-page"]));
        ResourceService resources = new(source);
        BattleStage stage = Stage(resources, source.Descriptor);
        resources.Prepare(new PageResolver()).Commit();
        BattleRenderer renderer = new(96, 64);
        BattleCamera camera = new(Vector3.Zero, -Vector3.UnitZ, 1);
        byte[] first = renderer.Render(stage, [], camera, TimeSpan.Zero).ToArray();
        Assert.Equal(first, renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(99)));
        byte[] second = renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(100)).ToArray();
        Assert.NotEqual(first, second);
        Assert.Equal(second, renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(199)));
        Assert.Equal(first, renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(200)));
        Assert.Equal(second, renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(300)));
        Assert.Equal(first, renderer.Render(stage, [], camera));
        Assert.Equal(second, renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(100)));
        TriangleCapture capture = new();
        renderer.TriangleRenderer = capture;
        renderer.Render(stage, [], camera, TimeSpan.FromMilliseconds(100));
        Assert.Equal(new Rgba32(30, 210, 60), capture.Draws[0].Texture[0, 0]);
        Assert.Equal(new Rgba32(230, 200, 20), capture.Draws[2].Texture[0, 0]);
    }

    private static BattleStage Stage(ResourceService resources, ResourceSourceDescriptor source)
    {
        List<BattleMesh> meshes = [];
        Dictionary<(ushort Page, ushort Clut), AssetHandle<TextureValue>> pages = [];
        foreach (ushort page in new ushort[] { 0x90, 0x91 })
        {
            float left = page == 0x90 ? -1.5f : 0.1f;
            float right = left + 1.4f;
            Vector2[] localUv = [new(10, 20), new(10, 50), new(40, 20), new(40, 50)];
            Vector2[] atlasUv = localUv.Select(uv => uv + new Vector2(page == 0x91 ? 128 : 0, 0)).ToArray();
            BattleFace face = new([0, 1, 2, 3], atlasUv, 0, new(128, 128, 128)) { NativeMaterial = new(page, 0, localUv) };
            meshes.Add(new([new(new Vector3(left, -0.8f, -2), 0), new(new Vector3(left, 0.8f, -2), 0), new(new Vector3(right, -0.8f, -2), 0), new(new Vector3(right, 0.8f, -2), 0)], [face]));
            TextureBuildContext context = new(new(new("battle/a0stg001.x"), $"page/{page & 31}"), source, 1, "native-battle-stage-page", 256, 256, () => new(256, 256));
            pages.Add((page, 0), resources.GetTexture(context));
        }
        return new(1, meshes, [new(384, 256, Enumerable.Repeat(new Rgba32(90, 90, 90), 384 * 256).ToArray())]) { NativePages = pages };
    }

    private sealed class PageResolver : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            bool first = context.Key.Subresource == "page/16";
            PixelImage image = Solid(first ? new(220, 30, 40) : new(20, 70, 230), first ? 512 : 510);
            PixelImage next = Solid(first ? new(30, 210, 60) : new(230, 200, 20), first ? 512 : 510);
            return new(image, "fixture:page", 256, 256) { Animation = new([image, next], 100) };
        }

        private static PixelImage Solid(Rgba32 color, int height) => new(512, height, Enumerable.Repeat(color, 512 * height).ToArray());
    }

    private sealed record CapturedDraw(Vector3[] Positions, Vector2[] Uv, PixelImage Texture);

    private sealed class TriangleCapture : ITriangleRenderer
    {
        public List<CapturedDraw> Draws { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Draws.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Draws.Add(new([new Vector3(a.Position, a.Depth), new Vector3(b.Position, b.Depth), new Vector3(c.Position, c.Depth)], [a.Uv, b.Uv, c.Uv], material.Texture!));
        }

        public void End()
        {
        }
    }
}
