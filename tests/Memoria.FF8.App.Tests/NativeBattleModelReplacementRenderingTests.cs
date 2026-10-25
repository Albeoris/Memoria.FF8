using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleModelReplacementRenderingTests
{
    [Theory]
    [InlineData(false, 16, 16)]
    [InlineData(true, 16, 16)]
    [InlineData(false, 7, 11)]
    [InlineData(true, 17, 25)]
    public void NativeActorAndLinkedWeaponSamplingFollowLiveMaterialsInSoftwareAndTrianglePresentation(bool weapon, int imageWidth, int imageHeight)
    {
        using InMemoryResourceSource source = new(new("fixture", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        TextureBuildContext context = new(new(new("battle/example.dat"), "texture/0"), source.Descriptor, 1, "classic-battle", 8, 8, () => new(8, 8));
        ResourceTextureList textures = new([resources.GetTexture(context)]);
        BattleMesh mesh = new([new(new Vector3(-1, -1, -2), 0), new(new Vector3(-1, 1, -2), 0), new(new Vector3(1, -1, -2), 0), new(new Vector3(1, 1, -2), 0)], [new([0, 1, 2, 3], [Vector2.Zero, new(0, 7), new(7, 0), new(7, 7)], 0, new(128, 128, 128)) { NativeMaterial = new(0x80, 0x4000, [Vector2.Zero, new(0, 7), new(7, 0), new(7, 7)]) }]);
        NativeBattleEffectHeapAllocation pack = new("fixture", Pack().ToImmutableArray());
        BattleModel model = new("fixture", [new(-1, 0)], [mesh], textures, [new([new(Vector3.Zero, [Vector3.Zero], [Vector3.One])])], 1)
        {
            NativeBodyTexturePack = weapon ? null : pack,
            NativeWeaponTexturePack = weapon ? pack : null
        };
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(pack, 0);
        NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        BattleActorDraw actor = new(model, Vector3.Zero) { NativeTextureBinding = load.Binding, NativeTextures = state };
        BattleStage stage = new(0, [], []);
        BattleCamera camera = new(Vector3.Zero, -Vector3.UnitZ, 1);
        BattleRenderer renderer = new(64, 64);
        byte[] baseline = renderer.Render(stage, [actor], camera).ToArray();
        Assert.True(renderer.DrawnTriangles > 0);
        TriangleCapture capture = new();
        renderer.TriangleRenderer = capture;
        renderer.Render(stage, [actor], camera);
        Vector2 originalUv = capture.Uv;
        resources.Prepare(new SolidResolver(imageWidth, imageHeight)).Commit();
        renderer.Render(stage, [actor], camera);
        Assert.Equal(Math.Max(256, imageWidth * 32), capture.Texture!.Width);
        Assert.Equal(Math.Max(256, imageHeight * 32), capture.Texture.Height);
        Assert.Equal(new Rgba32(10, 80, 200), capture.Texture[0, 0]);
        Assert.Equal(originalUv, capture.Uv);
        renderer.TriangleRenderer = null;
        byte[] selected = renderer.Render(stage, [actor], camera).ToArray();
        Assert.NotEqual(baseline, selected);
        resources.Prepare(null).Commit();
        Assert.Equal(baseline, renderer.Render(stage, [actor], camera));
    }

    private static byte[] Pack()
    {
        byte[] bytes = new byte[8 + 8 + 524 + 76];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 9);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 524);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), 31);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(540), 76);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(548), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(550), 8);
        bytes.AsSpan(552).Fill(1);
        return bytes;
    }

    private sealed class SolidResolver(int width, int height) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => new(new(width, height, Enumerable.Repeat(new Rgba32(10, 80, 200), width * height).ToArray()), "fixture:replacement", 8, 8);
    }

    private sealed class TriangleCapture : ITriangleRenderer
    {
        public PixelImage? Texture { get; private set; }
        public Vector2 Uv { get; private set; }
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Texture = material.Texture;
            Uv = b.Uv;
        }
        public void End()
        {
        }
    }
}
