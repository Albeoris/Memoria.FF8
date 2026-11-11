using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string EnergyBallPath = "ff7/data/battle/special/ho_den/";
    private readonly Ff7BattleEffectModel?[] energyBallModels = new Ff7BattleEffectModel?[20];
    private Ff7BattleBillboardRenderer? energyBallLight;
    private Ff7BattleBillboardRenderer? energyBallBlast;

    private void DrawEnergyBallMesh(ITriangleRenderer renderer, Ff7BattlePrimaryEnergyBallMeshDraw draw, CameraView view)
    {
        if (draw.Alpha == 0)
        {
            return;
        }
        string descriptor = draw.Model switch
        {
            0 => "elec1.rsd",
            >= 1 and <= 4 => $"houden{draw.Model}.rsd",
            >= 5 and <= 16 => $"e{draw.Model - 4}.rsd",
            17 => "tama1.rsd",
            18 => "tama2.rsd",
            19 => "moto.rsd",
            _ => throw new InvalidDataException("FFVII Energy Ball has an unsupported original model descriptor.")
        };
        Ff7BattleEffectModel model = energyBallModels[draw.Model] ??= Ff7BattleEffectModel.Read(MagicArchive(), EnergyBallPath, descriptor);
        Matrix4x4 transform = draw.Basis.Matrix * Matrix4x4.CreateTranslation(draw.Position.Position);
        DrawModel(renderer, model.Mesh, model.Textures, transform, view, new() { Cull = true }, draw.Alpha / 255f);
    }

    private void DrawEnergyBallSprite(ITriangleRenderer renderer, Ff7BattlePrimaryEnergyBallSpriteDraw draw, CameraView view)
    {
        Ff7BattleBillboardRenderer sprite = draw.Resource switch
        {
            20 => energyBallLight ??= LoadBillboard(EnergyBallPath + "hikari1.s", ["ho_den02.tex"], 2, _ => 0, TriangleBlend.Add),
            21 => energyBallBlast ??= LoadBillboard(EnergyBallPath + "baku1.s", ["ho_den03.tex"], 16, _ => 0, TriangleBlend.Add),
            _ => throw new InvalidDataException("FFVII Energy Ball has an unsupported original sprite descriptor.")
        };
        sprite.Draw(renderer, draw.Frame, view.Transform(draw.Position.Position), view.Width, view.Height, view.Focal, draw.Scale / 4096f, brightness: draw.Brightness / 255f);
    }
}
