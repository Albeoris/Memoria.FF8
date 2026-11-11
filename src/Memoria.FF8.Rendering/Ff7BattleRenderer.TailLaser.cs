using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string TailLaserPath = "ff7/data/battle/special/tele/";
    private readonly Ff7BattleEffectModel?[] tailLaserModels = new Ff7BattleEffectModel?[3];
    private readonly Dictionary<(int Resource, bool Additive), Ff7BattleBillboardRenderer> tailLaserSprites = [];

    private void DrawTailLaserMesh(ITriangleRenderer renderer, Ff7BattleTailLaserMeshDraw draw, CameraView view)
    {
        string name = draw.Resource switch
        {
            0 => "baku11.rsd",
            1 => "tama.rsd",
            2 => "reza.rsd",
            _ => throw new InvalidDataException("FFVII Tail Laser has an invalid original model descriptor.")
        };
        Ff7BattleEffectModel model = tailLaserModels[draw.Resource] ??= Ff7BattleEffectModel.Read(MagicArchive(), TailLaserPath, name);
        Matrix4x4 basis = draw.Basis.Matrix;
        if (draw.Resource == 1)
        {
            basis *= new Matrix4x4(view.Right.X, view.Right.Y, view.Right.Z, 0, view.Up.X, view.Up.Y, view.Up.Z, 0, view.Forward.X, view.Forward.Y, view.Forward.Z, 0, 0, 0, 0, 1);
        }
        Matrix4x4 transform = basis * Matrix4x4.CreateTranslation(draw.Position.Position);
        DrawModel(renderer, model.Mesh, model.Textures, transform, view, new() { Cull = true }, colorBlend: new(0, 0, 0, draw.ColorWeight));
    }

    private void DrawTailLaserSprite(ITriangleRenderer renderer, Ff7BattleTailLaserSprite draw, CameraView view)
    {
        (int Resource, bool Additive) key = (draw.Resource, draw.Additive);
        if (!tailLaserSprites.TryGetValue(key, out Ff7BattleBillboardRenderer? sprite))
        {
            sprite = ReadTailLaserSprite(draw.Resource, draw.Additive);
            tailLaserSprites.Add(key, sprite);
        }
        float scale = draw.Scale / 4096f;
        float brightness = Math.Min(255, draw.Brightness * 2) / 255f;
        if (draw.Basis is { } basis)
        {
            Matrix4x4 transform = basis.Matrix * Matrix4x4.CreateTranslation(draw.Position.Position);
            sprite.Draw(renderer, draw.Frame, default, view.Width, view.Height, view.Focal, scale, brightness: brightness, transform: local => view.Transform(Vector3.Transform(local, transform)));
            return;
        }
        Vector3 anchor = view.Transform(draw.Position.Position);
        if (draw.Roll == 0)
        {
            sprite.Draw(renderer, draw.Frame, anchor, view.Width, view.Height, view.Focal, scale, brightness: brightness);
            return;
        }
        Matrix4x4 rotation = BattleRigidPose.Rotation(new(0, 0, draw.Roll));
        sprite.Draw(renderer, draw.Frame, anchor, view.Width, view.Height, view.Focal, scale, brightness: brightness, transform: local =>
        {
            Vector3 rotated = Vector3.Transform(local, rotation);
            return anchor + new Vector3(rotated.X, -rotated.Y, rotated.Z);
        });
    }

    private Ff7BattleBillboardRenderer ReadTailLaserSprite(int resource, bool additive)
    {
        (string name, int frames, string? texture) = resource switch
        {
            3 => ("kou1.s", 2, "tail02.tex"),
            4 => ("hahen.s", 20, "tail02.tex"),
            5 => ("hit11.s", 8, "tail02.tex"),
            6 => ("hit12.s", 8, "tail02.tex"),
            7 => ("elec1.s", 8, "tail03.tex"),
            8 => ("elec2.s", 8, "tail03.tex"),
            10 => ("kemu1.s", 8, null),
            11 => ("hibana.s", 8, "tail02.tex"),
            12 => ("kemu11.s", 8, "tail03.tex"),
            _ => throw new InvalidDataException("FFVII Tail Laser has an invalid original sprite descriptor.")
        };
        TriangleBlend blend = additive ? TriangleBlend.Add : TriangleBlend.Replace;
        if (texture is not null)
        {
            return LoadBillboard(TailLaserPath + name, [texture], frames, _ => 0, blend);
        }
        PcTexture atlas = cureTexture ?? throw new InvalidOperationException("FFVII Tail Laser requires its original shared battle texture.");
        IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> billboard = BattleBillboard.Read(Read(MagicArchive(), TailLaserPath + name));
        if (billboard.Count != frames)
        {
            throw new InvalidDataException("FFVII Tail Laser ground smoke has an unexpected original frame count.");
        }
        return new(billboard, [atlas], _ => 0, blend);
    }
}
