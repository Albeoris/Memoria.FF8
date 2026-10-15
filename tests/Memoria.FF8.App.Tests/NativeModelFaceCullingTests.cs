using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class NativeModelFaceCullingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoplanarOppositeFacesShowOnlyTheNativeFrontWindingRegardlessOfSubmissionOrder(bool quad)
    {
        PixelImage front = new(1, 1, [new Rgba32(200, 100, 50, 255)]);
        PixelImage back = new(1, 1, [new Rgba32(20, 40, 80, 255)]);
        ModelVertex[] vertices = [new(new Vector3(0, -32, -32), 0), new(new Vector3(0, 32, -32), 0), new(new Vector3(0, -32, 32), 0), new(new Vector3(0, 32, 32), 0)];
        // At direction192 the root's native Y quarter turn projects these vertices into a front-facing screen primitive.
        ModelFace frontFace = new([0, 2, 1, 3], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, quad);
        ModelFace backFace = new([1, 3, 0, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 1, quad);
        FieldModel model = new("opposite-coat-surfaces", [new ModelBone(-1, 0)], vertices, [frontFace], [front, back], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = new(100, "native-winding-component", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [model]);
        ActorVisual actor = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] Render(params ModelFace[] faces) => new FieldRenderer(field with { Models = [model with { Faces = faces }] }, modelTextureSampling: ModelTextureSampling.Nearest).Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        byte[] expected = Render(frontFace);
        Assert.Contains((byte)200, expected);
        Assert.Equal(expected, Render(backFace, frontFace));
        Assert.Equal(expected, Render(frontFace, backFace));
        byte[] rejected = Render(backFace);
        Assert.DoesNotContain((byte)80, rejected);
    }
}
