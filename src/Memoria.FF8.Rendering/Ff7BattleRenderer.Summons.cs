using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private readonly Dictionary<bool, Ff7BattleSummonModel> summonModels = [];

    private void DrawSummonModel(ITriangleRenderer renderer, Ff7BattleSummonModelDraw draw, CameraView view)
    {
        if (!summonModels.TryGetValue(draw.FatChocobo, out Ff7BattleSummonModel? resource))
        {
            resource = Ff7BattleSummonModel.Load(MagicArchive(), draw.FatChocobo);
            summonModels.Add(draw.FatChocobo, resource);
        }
        BattleClipFrame frame = resource.Frame(draw.Clip, draw.Frame);
        Matrix4x4 world = Matrix4x4.CreateRotationY(draw.Yaw * (MathF.Tau / 4096)) * Matrix4x4.CreateTranslation(draw.Position.ToVector());
        Span<Matrix4x4> transforms = stackalloc Matrix4x4[resource.Model.Bones.Count];
        BattleRigidPose.WriteTransforms(resource.Model, frame, world, transforms);
        Ff7ModelMaterialState material = new() { Cull = true };
        for (int index = 0; index < transforms.Length; index++)
        {
            if (resource.Parts[index] is PcPolygonModel part)
            {
                DrawModel(renderer, part, resource.Textures, transforms[index], view, material);
            }
        }
    }
}
