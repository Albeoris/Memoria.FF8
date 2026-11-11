using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string WavePath = "ff7/data/battle/special/hado/";
    private readonly PcPolygonModel?[] waveModels = new PcPolygonModel?[8];
    private readonly Ff7BattleBillboardRenderer?[] waveSprites = new Ff7BattleBillboardRenderer?[3];
    private Ff7BattleBillboardRenderer? waveSpark;
    private PcTexture[]? waveOrbTextures;
    private PcTexture[]? waveCenterTextures;

    private void DrawWaveMesh(ITriangleRenderer renderer, Ff7BattlePrimaryWaveMeshDraw draw, CameraView view)
    {
        if (draw.Alpha == 0)
        {
            return;
        }
        string name = draw.Model switch
        {
            1 => "hado.p",
            2 => "hado1.p",
            3 => "tama1.p",
            4 => "tama2.p",
            5 => "moto.p",
            6 => "wa.p",
            7 => "sindom1.p",
            8 => "hikari.p",
            _ => throw new InvalidDataException("FFVII Wave has an unsupported model descriptor.")
        };
        PcPolygonModel model = waveModels[draw.Model - 1] ??= PcPolygonModel.Read(Read(MagicArchive(), WavePath + name));
        Matrix4x4 transform = draw.Basis.Matrix * Matrix4x4.CreateTranslation(draw.Position.Position);
        IReadOnlyList<PcTexture> textures = draw.Model switch
        {
            >= 3 and <= 5 => waveOrbTextures ??= [PcTexture.Read(Read(MagicArchive(), WavePath + "moto1.tex"))],
            7 => waveCenterTextures ??= [PcTexture.Read(Read(MagicArchive(), WavePath + "domu1.tex"))],
            _ => []
        };
        DrawModel(renderer, model, textures, transform, view, new() { Cull = true }, draw.Alpha / 255f);
    }

    private void DrawWaveSprite(ITriangleRenderer renderer, Ff7BattlePrimaryWaveSpriteDraw draw, CameraView view)
    {
        if (draw.Resource is < 11 or > 13)
        {
            throw new InvalidDataException("FFVII Wave has an unsupported sprite descriptor.");
        }
        bool explosion = draw.Resource == 13;
        Ff7BattleBillboardRenderer sprite = waveSprites[draw.Resource - 11] ??= LoadBillboard(WavePath + (explosion ? "baku1.s" : "hahen1.s"), [WavePath + (explosion ? "energy02.tex" : "energy00.tex")], explosion ? 16 : 7, _ => 0, draw.Resource == 11 ? TriangleBlend.Replace : TriangleBlend.Add);
        Vector3 anchor = view.Transform(draw.Position.Position);
        float scale = draw.Scale / 4096f;
        float brightness = draw.Brightness / 255f;
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

    private void DrawWaveSpark(ITriangleRenderer renderer, Ff7BattleWaveSparkDraw draw, CameraView view)
    {
        if (waveSpark is null)
        {
            PcTexture atlas = grenadeTexture ?? throw new InvalidOperationException("FFVII Wave requires the original shared battle effect atlas.");
            IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> frames = BattleBillboard.Read(Read(MagicArchive(), WavePath + "jo_a.s"));
            if (frames.Count != 8)
            {
                throw new InvalidDataException("FFVII Wave has an unexpected original spark frame count.");
            }
            waveSpark = new(frames, [atlas], _ => 0, TriangleBlend.Add);
        }
        waveSpark.Draw(renderer, draw.Frame, view.Transform(draw.Position.Position), view.Width, view.Height, view.Focal, 2.5f, brightness: 128 / 255f);
    }
}
