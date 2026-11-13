using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private readonly Ff7BattleBillboardRenderer?[] crossSlashSprites = new Ff7BattleBillboardRenderer?[3];

    private void DrawCrossSlash(ITriangleRenderer renderer, Ff7BattlePrimaryCrossSlashDraw draw, CameraView view)
    {
        string name = draw.Sprite switch
        {
            0 => "one.s",
            1 => "two.s",
            2 => "three.s",
            _ => throw new InvalidDataException("FFVII Cross-slash has an unsupported original sprite descriptor.")
        };
        Ff7BattleBillboardRenderer sprite = crossSlashSprites[draw.Sprite] ??= LoadBillboard(name, ["kyogiri.tex"], 1, _ => 0, TriangleBlend.Add);
        Vector3 anchor = SpriteAnchor(draw.Position.X, draw.Position.Y, draw.Position.Z, draw.Radius, view);
        float angle = draw.Rotation * (MathF.Tau / 4096);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
        sprite.Draw(renderer, 0, anchor, view.Width, view.Height, view.Focal, draw.Scale / 4096f, brightness: draw.Brightness / 128f,
            transform: point => anchor + Vector3.Transform(new(point.X, -point.Y, 0), rotation));
    }
}
