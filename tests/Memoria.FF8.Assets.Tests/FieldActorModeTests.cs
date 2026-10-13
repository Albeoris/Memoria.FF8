using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldActorModeTests
{
    [Fact]
    public void ActualCorridorAdditionalGroupSuppressionPreservesTheOtherFourHundredFaces()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(139);
        FieldModel model = field.Models[0];
        Assert.Equal(24, model.ControlBones.Additional);
        Assert.DoesNotContain(model.Bones, bone => bone.Parent == 24);
        Assert.Equal(49, model.Vertices.Count(vertex => vertex.Bone == 24));
        Assert.All(model.Faces.Take(400), face => Assert.All(face.Vertices.Take(face.Quad ? 4 : 3), index => Assert.NotEqual(24, model.Vertices[index].Bone)));
        Assert.All(model.Faces.Skip(400), face => Assert.All(face.Vertices.Take(face.Quad ? 4 : 3), index => Assert.Equal(24, model.Vertices[index].Bone)));
        Assert.Equal(441, model.Faces.Count);

        ActorVisual actor = new(0, 0, -3172, -5705, 8341, 72, 0, 0, true);
        FieldRenderer renderer = new(field, 960, 720);
        Dictionary<int, int> states = [];
        byte[] enabled = renderer.Render(states, [actor], TimeSpan.Zero, backgroundVisible: false).ToArray();
        byte[] disabled = renderer.Render(states, [actor with { AdditionalControlDisabled = true }], TimeSpan.Zero, backgroundVisible: false).ToArray();
        Assert.NotEqual(enabled, disabled);
        Assert.Contains(disabled.Where((_, index) => index % 4 != 3), value => value != 0);

        FieldModel[] bodyModels = field.Models.ToArray();
        bodyModels[0] = model with { Faces = model.Faces.Take(400).ToArray() };
        FieldRenderer bodyRenderer = new(field with { Models = bodyModels }, 960, 720);
        Assert.Equal(bodyRenderer.Render(states, [actor], TimeSpan.Zero, backgroundVisible: false), disabled);
        Assert.Equal(enabled, renderer.Render(states, [actor], TimeSpan.Zero, backgroundVisible: false));
    }
}
