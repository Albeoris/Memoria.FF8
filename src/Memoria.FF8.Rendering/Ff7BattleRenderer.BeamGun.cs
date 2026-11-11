using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string BeamGunPath = "ff7/data/battle/special/mgbeam/";
    private PcPolygonModel? beamGunMesh;
    private Ff7BattleBillboardRenderer? beamGunFlash;
    private Ff7BattleBillboardRenderer? beamGunBurst;

    private void DrawBeamGunMesh(ITriangleRenderer renderer, Ff7BattlePrimaryBeamGunMeshDraw shot, CameraView view)
    {
        beamGunMesh ??= PcPolygonModel.Read(Read(MagicArchive(), BeamGunPath + "gan1.p"));
        Matrix4x4 transform = shot.Basis.Matrix * Matrix4x4.CreateTranslation(shot.Position.Position);
        DrawModel(renderer, beamGunMesh, [], transform, view, new() { Cull = true });
    }

    private void DrawBeamGunSprite(ITriangleRenderer renderer, Ff7BattlePrimaryBeamGunSpriteDraw sprite, CameraView view)
    {
        if (sprite.FlashBasis is { } basis)
        {
            beamGunFlash ??= LoadBillboard(BeamGunPath + "hassya.s", [BeamGunPath + "hassya.tex"], 4, _ => 0);
            Matrix4x4 transform = basis.Matrix * Matrix4x4.CreateTranslation(sprite.Position.Position);
            beamGunFlash.Draw(renderer, sprite.Frame, default, view.Width, view.Height, view.Focal, transform: local => view.Transform(Vector3.Transform(local, transform)));
        }
        else
        {
            beamGunBurst ??= LoadBillboard(BeamGunPath + "hit1.s", [BeamGunPath + "hit1.tex"], 8, _ => 0, nativePageOrigin: true);
            Vector3 anchor = SpriteAnchor(sprite.Position.X, sprite.Position.Y, sprite.Position.Z, sprite.Radius, view);
            beamGunBurst.Draw(renderer, sprite.Frame, anchor, view.Width, view.Height, view.Focal);
        }
    }
}
