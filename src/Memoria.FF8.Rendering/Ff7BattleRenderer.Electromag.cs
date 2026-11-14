using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string ElectromagPath = "ff7/data/battle/special/geki2/";
    private static readonly string[] ElectromagMeshes = ["geki2.rsd", "domu1.rsd", "domu2.rsd", "domu3.rsd", "domu4.rsd", "okuri6.rsd", "okuri2.rsd", "okuri3.rsd", "okuri5.rsd", "isi1.rsd", "isi2.rsd", "isi3.rsd", "isi4.rsd", "baku1.rsd", "wa.rsd"];
    private readonly Ff7BattleEffectModel?[] electromagModels = new Ff7BattleEffectModel?[15];
    private readonly Dictionary<(int Resource, bool Additive), Ff7BattleBillboardRenderer> electromagSprites = [];

    private void DrawElectromagMesh(ITriangleRenderer renderer, Ff7BattleElectromagMeshDraw draw, CameraView view)
    {
        if ((uint)draw.Resource >= ElectromagMeshes.Length)
        {
            throw new InvalidDataException("FFVII Electromag has an unsupported original model descriptor.");
        }
        Ff7BattleEffectModel model = electromagModels[draw.Resource] ??= Ff7BattleEffectModel.Read(MagicArchive(), ElectromagPath, ElectromagMeshes[draw.Resource]);
        Matrix4x4 transform = draw.Basis.Matrix * Matrix4x4.CreateTranslation(draw.Position.Position);
        DrawModel(renderer, model.Mesh, model.Textures, transform, view, new() { Cull = true }, colorBlend: new ModelColorBlend(0, 0, 0, draw.Weight));
    }

    private void DrawElectromagSprite(ITriangleRenderer renderer, Ff7BattleElectromagSpriteDraw draw, CameraView view)
    {
        if (!electromagSprites.TryGetValue((draw.Resource, draw.Additive), out Ff7BattleBillboardRenderer? sprite))
        {
            sprite = ReadElectromagSprite(draw.Resource, draw.Additive);
            electromagSprites.Add((draw.Resource, draw.Additive), sprite);
        }
        Vector3 anchor = view.Transform(draw.Position.Position);
        float brightness = Math.Min(255, draw.Brightness * 2) / 255f;
        float scale = draw.Scale / 4096f;
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

    private Ff7BattleBillboardRenderer ReadElectromagSprite(int resource, bool additive)
    {
        (string name, int frames) = resource switch
        {
            15 => ("kemu1.s", 8),
            16 => ("hahen1.s", 6),
            17 => ("hibana.s", 8),
            18 => ("hikari1.s", 1),
            19 => ("jo_c01.s", 8),
            _ => throw new InvalidDataException("FFVII Electromag has an unsupported original sprite descriptor.")
        };
        TriangleBlend blend = additive ? TriangleBlend.Add : TriangleBlend.Replace;
        if (resource is 16 or 17 or 18)
        {
            return LoadBillboard(ElectromagPath + name, [ElectromagPath + "geki00.tex"], frames, _ => 0, blend);
        }
        PcTexture texture = resource == 15
            ? cureTexture ?? throw new InvalidOperationException("FFVII Electromag smoke requires the original shared battle texture.")
            : grenadeTexture ?? throw new InvalidOperationException("FFVII Electromag impact requires the original shared hitmark texture.");
        IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> billboard = BattleBillboard.Read(Read(MagicArchive(), ElectromagPath + name));
        if (billboard.Count != frames)
        {
            throw new InvalidDataException("FFVII Electromag sprite differs from its original frame count.");
        }
        return new(billboard, [texture], _ => 0, blend);
    }
}
