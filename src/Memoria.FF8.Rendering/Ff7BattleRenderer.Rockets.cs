using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private PcPolygonModel? rocketMesh;
    private Ff7BattleBillboardRenderer[]? rocketSprites;

    private void DrawRockets(ITriangleRenderer renderer, Ff7BattleRocketState state, CameraView view)
    {
        const string path = "ff7/data/battle/special/misairu/";
        rocketMesh ??= PcPolygonModel.Read(Read(MagicArchive(), path + "misairu.p"));
        rocketSprites ??=
        [
            LoadBillboard(path + "hunsya.s", ["missil00.tex"], Ff7BattleRocketEffect.ExhaustFrameCount, _ => 0),
            LoadBillboard(path + "kemu1.s", ["missil00.tex"], Ff7BattleRocketEffect.SmokeFrameCount, _ => 0),
            LoadBillboard(path + "baku1.s", ["missil02.tex"], Ff7BattleRocketEffect.ExplosionFrameCount, _ => 0)
        ];
        Matrix4x4 baseRotation = new(0, 1, 0, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, 0, 0, 1);
        foreach (Ff7BattleRocketProjectile missile in state.Missiles)
        {
            Matrix4x4 transform = Matrix4x4.CreateScale(0.5f) * baseRotation * Ff7BattleRocketEffect.YawMatrix(missile.Yaw) * Matrix4x4.CreateTranslation(missile.Position.Position);
            DrawModel(renderer, rocketMesh, [], transform, view, new() { Cull = true }, 247 / 255f);
        }
        foreach (Ff7BattleRocketSprite sprite in state.Sprites)
        {
            rocketSprites[sprite.Kind].Draw(renderer, sprite.Frame, view.Transform(sprite.Position.Position), view.Width, view.Height, view.Focal, 2, brightness: sprite.Kind == 2 ? 128 / 255f : 1);
        }
        TriangleMaterial material = new(null, TriangleBlend.Add, TriangleDepthComparison.GreaterEqual, false);
        Vector4 color = new(128 / 255f, 64 / 255f, 32 / 255f, 1);
        foreach (Ff7BattleBraverPoint point in state.Points)
        {
            Vector3 anchor = view.Transform(point.Position);
            float pixel = anchor.Z / 512;
            CameraSpaceVertex a = new(anchor, Vector2.Zero, color);
            CameraSpaceVertex b = new(anchor + new Vector3(pixel, 0, 0), Vector2.Zero, color);
            CameraSpaceVertex c = new(anchor + new Vector3(0, -pixel, 0), Vector2.Zero, color);
            CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, view.Width, view.Height, view.Focal, material);
        }
    }
}
