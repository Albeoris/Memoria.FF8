using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private PcPolygonModel[]? searchScopeMeshes;
    private Ff7BattleBillboardRenderer[]? searchScopeSprites;

    private void DrawSearchScope(ITriangleRenderer renderer, Ff7BattleSearchScopeState state, CameraView view)
    {
        foreach (Ff7BattleSearchScopeMesh mesh in state.Meshes)
        {
            DrawSearchScopeMesh(renderer, state.Source, state.Basis, mesh, view);
        }
        foreach (Ff7BattleSearchScopeSprite sprite in state.Sprites)
        {
            int age = state.Frame - sprite.StartedAt;
            int stage = age < Ff7BattleSearchScopeEffect.FirstSpriteFrames ? 0 : 1;
            int frame = stage == 0 ? age : age - Ff7BattleSearchScopeEffect.FirstSpriteFrames;
            DrawSearchScopeSprite(renderer, sprite.Position, stage, frame, view);
        }
    }

    private void DrawSearchScopeMesh(ITriangleRenderer renderer, Ff7BattleBraverPoint source, Ff7BattleSearchScopeBasis basis, Ff7BattleSearchScopeMesh mesh, CameraView view)
    {
        byte alpha = Ff7BattleSearchScopeEffect.Alpha(mesh.Transparency);
        if (alpha == 0)
        {
            return;
        }
        searchScopeMeshes ??= [PcPolygonModel.Read(Read(MagicArchive(), "ff7/data/battle/special/scorp/sarchi2.p")), PcPolygonModel.Read(Read(MagicArchive(), "sarchi33.p"))];
        Matrix4x4 transform = Matrix4x4.CreateScale(mesh.TransverseScale / 4096f, mesh.LongitudinalScale / 4096f, mesh.TransverseScale / 4096f) * BattleRigidPose.Rotation(mesh.Rotation) * basis.Matrix * Matrix4x4.CreateTranslation(source.Position);
        DrawModel(renderer, searchScopeMeshes[mesh.Model], [], transform, view, new() { Cull = true }, alpha / 255f);
    }

    private void DrawSearchScopeSprite(ITriangleRenderer renderer, Ff7BattleBraverPoint position, int stage, int frame, CameraView view)
    {
        const string path = "ff7/data/battle/special/scorp/";
        searchScopeSprites ??= [LoadBillboard(path + "hit11.s", [path + "hit1.tex"], Ff7BattleSearchScopeEffect.FirstSpriteFrames, _ => 0), LoadBillboard(path + "baku11.s", [path + "baku1.tex"], Ff7BattleSearchScopeEffect.SecondSpriteFrames, _ => 0)];
        searchScopeSprites[stage].Draw(renderer, frame, view.Transform(position.Position), view.Width, view.Height, view.Focal, 2.25f);
    }
}
