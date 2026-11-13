using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private Ff7BattleEffectModel? mindblowMesh;
    private Ff7BattleBillboardRenderer? mindblowParticles;

    private void DrawMindblowMesh(ITriangleRenderer renderer, Ff7BattlePrimaryMindblowMeshDraw draw, CameraView view)
    {
        Ff7BattleEffectModel mesh = mindblowMesh ??= Ff7BattleEffectModel.Read(MagicArchive(), "ff7/data/battle/limit/mind", "mind.rsd");
        Matrix4x4 transform = draw.Basis.Matrix * Matrix4x4.CreateTranslation(draw.Point.Position);
        DrawModel(renderer, mesh.Mesh, mesh.Textures, transform, view, new() { Cull = true }, draw.Inner ? 0.5f : 1f);
    }

    private void DrawMindblowParticle(ITriangleRenderer renderer, Ff7BattlePrimaryMindblowParticleDraw draw, CameraView view)
    {
        Ff7BattleBillboardRenderer sprite = mindblowParticles ??= LoadBillboard("m_brake.s", ["m_break.tex"], 4, _ => 0, TriangleBlend.Add);
        sprite.Draw(renderer, draw.Frame, view.Transform(draw.Point.Position), view.Width, view.Height, view.Focal, draw.Scale / 4096f, brightness: draw.Brightness / 128f);
    }
}
