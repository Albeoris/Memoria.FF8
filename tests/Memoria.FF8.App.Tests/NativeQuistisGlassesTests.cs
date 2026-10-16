using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeQuistisGlassesTests
{
    [Fact]
    public void OriginalQuistisPreservesTheEightTransparentGlassesFaces()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldModel quistis = assets.LoadField(180).Models[4];
        Assert.Equal("d021pxx", quistis.Name);
        Assert.Equal(Enumerable.Range(734, 8), quistis.Faces.Select((face, index) => (face, index)).Where(item => item.face.SemiTransparent).Select(item => item.index));
        Assert.All(quistis.Faces.Where(face => face.SemiTransparent), face =>
        {
            Assert.True(face.Quad);
            Assert.All(face.Vertices, vertex => Assert.Equal(9, quistis.Vertices[vertex].Bone));
        });
    }

    [Fact]
    public void OriginalGlassesRevealTheFaceAndAddHighlightsWithoutDarkeningIt()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldModel quistis = assets.LoadField(180).Models[4];
        // A declared portrait camera isolates the actual model's glasses from field placement and VM state.
        FieldAssets field = new(180, "quistis-glasses-component", [], [], [], new FieldCamera(-Vector3.UnitY, -Vector3.UnitZ, Vector3.UnitX, new Vector3(0, 305, 256), Vector2.Zero, 1600), new FieldWalkmesh([]), [quistis]);
        ActorVisual actor = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] Render(FieldModel model)
        {
            return new FieldRenderer(field with { Models = [model] }).Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        }
        byte[] face = Render(quistis with { Faces = quistis.Faces.Where(primitive => !primitive.SemiTransparent).ToArray() });
        byte[] opaqueGlasses = Render(quistis with { Faces = quistis.Faces.Select(primitive => primitive with { SemiTransparent = false }).ToArray() });
        byte[] transparentGlasses = Render(quistis);
        int highlights = 0;
        int opaqueDarkening = 0;
        for (int pixel = 0; pixel < face.Length; pixel += 4)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                Assert.True(transparentGlasses[pixel + channel] >= face[pixel + channel]);
                highlights += transparentGlasses[pixel + channel] > face[pixel + channel] ? 1 : 0;
                opaqueDarkening += opaqueGlasses[pixel + channel] < face[pixel + channel] ? 1 : 0;
            }
        }
        Assert.True(highlights > 100);
        Assert.True(opaqueDarkening > 100);
    }
}
