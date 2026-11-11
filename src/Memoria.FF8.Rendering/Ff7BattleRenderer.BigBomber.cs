using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string BigBomberPath = "ff7/data/battle/special/hakugek1/";
    private readonly Ff7BattleEffectModel?[] bigBomberModels = new Ff7BattleEffectModel?[5];
    private readonly Ff7BattleBillboardRenderer?[] bigBomberSprites = new Ff7BattleBillboardRenderer?[3];

    private void DrawBigBomberMesh(ITriangleRenderer renderer, Ff7BattlePrimaryBigBomberMeshDraw draw, CameraView view)
    {
        string descriptor = draw.Model switch
        {
            0 => "bom.rsd",
            1 => "cover.rsd",
            2 => "wakka.rsd",
            3 => "imp.rsd",
            4 => "impact.rsd",
            _ => throw new InvalidDataException("FFVII Big Bomber has an invalid original model descriptor.")
        };
        Ff7BattleEffectModel model = bigBomberModels[draw.Model] ??= Ff7BattleEffectModel.Read(MagicArchive(), BigBomberPath, descriptor);
        Matrix4x4 scale = Matrix4x4.CreateScale(draw.Width / 4096f, draw.Height / 4096f, draw.Width / 4096f);
        Matrix4x4 transform = scale * BattleRigidPose.Rotation(new(0, draw.Yaw, 0)) * Matrix4x4.CreateTranslation(draw.Position.Position);
        DrawModel(renderer, model.Mesh, model.Textures, transform, view, new() { Cull = true }, colorBlend: new(0, 0, 0, draw.Weight));
    }

    private void DrawBigBomberSprite(ITriangleRenderer renderer, Ff7BattlePrimaryBigBomberSpriteDraw draw, CameraView view)
    {
        Ff7BattleBillboardRenderer sprite = draw.Sprite switch
        {
            5 => bigBomberSprites[0] ??= LoadBillboard(BigBomberPath + "kakera.s", [BigBomberPath + "geki01.tex"], 3, _ => 0, TriangleBlend.Add),
            6 => bigBomberSprites[1] ??= LoadBillboard(BigBomberPath + "ball.s", [BigBomberPath + "geki01.tex"], 3, _ => 0, TriangleBlend.Add),
            7 => bigBomberSprites[2] ??= LoadBillboard(BigBomberPath + "dokkan.s", [BigBomberPath + "geki00.tex"], 8, _ => 0, TriangleBlend.Add),
            _ => throw new InvalidDataException("FFVII Big Bomber has an invalid original sprite descriptor.")
        };
        if (draw.Sprite == 7)
        {
            Matrix4x4 transform = BattleRigidPose.Rotation(new(0, draw.Angle, -512)) * Matrix4x4.CreateTranslation(draw.Position.Position);
            sprite.Draw(renderer, draw.Frame, default, view.Width, view.Height, view.Focal, draw.Scale / 4096f, transform: local => view.Transform(Vector3.Transform(local, transform)));
        }
        else
        {
            Vector3 anchor = view.Transform(draw.Position.Position);
            Matrix4x4 rotation = BattleRigidPose.Rotation(new(0, 0, draw.Angle));
            sprite.Draw(renderer, draw.Frame, anchor, view.Width, view.Height, view.Focal, draw.Scale / 4096f, brightness: draw.Brightness / 255f, transform: local =>
            {
                Vector3 rotated = Vector3.Transform(local, rotation);
                return anchor + new Vector3(rotated.X, -rotated.Y, rotated.Z);
            });
        }
    }
}
