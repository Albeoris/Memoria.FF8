using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string LaserCannonPath = "ff7/data/battle/special/reza1/";
    private PixelImage? laserCannonBeam;
    private Ff7BattleBillboardRenderer? laserCannonImpact;

    private void DrawLaserCannonBeam(ITriangleRenderer renderer, Ff7BattlePrimaryLaserCannonBeamDraw beam, CameraView view)
    {
        Vector3 start = view.Transform(beam.Source.Position);
        Vector3 end = view.Transform(beam.Target.Position);
        if (start.Z <= 0 || end.Z <= 0)
        {
            return;
        }

        Vector2 source = CameraSpaceTriangleSubmission.Project(new(start, Vector2.Zero), view.Width, view.Height, view.Focal).Position;
        Vector2 target = CameraSpaceTriangleSubmission.Project(new(end, Vector2.Zero), view.Width, view.Height, view.Focal).Position;
        Vector2 sourcePixel = new(unchecked((short)source.X), unchecked((short)source.Y));
        Vector2 targetPixel = new(unchecked((short)target.X), unchecked((short)target.Y));
        Vector2 delta = targetPixel - sourcePixel;
        int averageDepth = (LaserCannonOrderDepth(start.Z) + LaserCannonOrderDepth(end.Z)) / 2;
        float length = delta.Length();
        if (length == 0 || averageDepth <= 0)
        {
            return;
        }

        float unit = 4096 / length;
        int dx = ((int)(delta.X * unit) * 512 / averageDepth) >> 6;
        int dy = ((int)(delta.Y * unit) * 512 / averageDepth) >> 6;
        Vector2 offset = new(dy, -dx);
        PixelImage texture = laserCannonBeam ??= ReadLaserCannonBeamTexture();
        float v0 = beam.Frame * 32f / texture.Height;
        float v1 = (beam.Frame * 32 + 31f) / texture.Height;
        float u1 = 255f / texture.Width;
        float reciprocalDepth = 1 / start.Z;
        TriangleVertex Vertex(Vector2 position, float u, float v) => new(position, reciprocalDepth, 1, new(u, v), Vector4.One);
        TriangleVertex a = Vertex(sourcePixel + offset, 0, v0);
        TriangleVertex b = Vertex(sourcePixel - offset, 0, v1);
        TriangleVertex c = Vertex(targetPixel + offset, u1, v0);
        TriangleVertex d = Vertex(targetPixel - offset, u1, v1);
        TriangleMaterial material = new(texture, TriangleBlend.Add, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointClamp);
        renderer.Draw(a, b, c, material);
        renderer.Draw(b, d, c, material);
    }

    private static int LaserCannonOrderDepth(float cameraDepth)
    {
        const float near = 250;
        const float far = 64000;
        const float projectionDepth = (120f / 512) * far / (far - near);
        return (int)(MathF.FusedMultiplyAdd(cameraDepth, projectionDepth, -near * projectionDepth) * 0.25f);
    }

    private PixelImage ReadLaserCannonBeamTexture()
    {
        PcTexture texture = PcTexture.Read(Read(MagicArchive(), "laser02.tex"));
        const ushort nativeClut = 0x7808;
        int palette = (nativeClut >> 6) - BinaryPrimitives.ReadInt32LittleEndian(texture.Header.Span[0xE0..]);
        if (texture.Width != 256 || texture.Height != 256 || (uint)palette >= texture.Palettes.Count)
        {
            throw new InvalidDataException("FFVII Laser Cannon texture differs from its original quad packet.");
        }

        byte[] header = texture.Header.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0xD0), palette);
        return Ff7FieldRenderer.DecodeTexture(texture with { Header = header });
    }

    private void DrawLaserCannonImpact(ITriangleRenderer renderer, Ff7BattlePrimaryLaserCannonImpactDraw impact, CameraView view)
    {
        laserCannonImpact ??= LoadBillboard(LaserCannonPath + "hit1.s", ["laser00.tex"], 8, _ => 0);
        Vector3 anchor = SpriteAnchor(impact.Position.X, impact.Position.Y, impact.Position.Z, 200, view);
        laserCannonImpact.Draw(renderer, impact.Frame, anchor, view.Width, view.Height, view.Focal);
    }
}
