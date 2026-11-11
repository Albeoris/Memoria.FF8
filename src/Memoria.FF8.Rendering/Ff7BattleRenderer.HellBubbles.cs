using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private const string BubblePath = "ff7/data/battle/special/awa2/";
    private Ff7BattleBillboardRenderer? bubbleBurst;
    private Ff7BattleBillboardRenderer? bubbleStream;
    private PixelImage? bubbleRibbon;
    private PcPolygonModel? bubbleCloud;
    private PcTexture[]? bubbleCloudTextures;

    private void DrawBubbleSprite(ITriangleRenderer renderer, Ff7BattlePrimaryBubbleSpriteDraw draw, CameraView view)
    {
        Ff7BattleBillboardRenderer sprites = draw.Burst
            ? bubbleBurst ??= LoadBillboard(BubblePath + "baku1.s", ["awa.tex"], 12, _ => 0)
            : bubbleStream ??= LoadBillboard(BubblePath + "maru.s", ["awa.tex"], 1, _ => 0, TriangleBlend.Add);
        sprites.Draw(renderer, draw.Frame, view.Transform(draw.Position.Position), view.Width, view.Height, view.Focal, draw.Scale / 4096f, brightness: Math.Min(255, draw.Brightness * 2) / 255f);
    }

    private void DrawBubbleCloud(ITriangleRenderer renderer, Ff7BattlePrimaryBubbleCloudDraw draw, CameraView view)
    {
        if (draw.Brightness == 0)
        {
            return;
        }
        bubbleCloud ??= PcPolygonModel.Read(Read(MagicArchive(), BubblePath + "suna2.p"));
        bubbleCloudTextures ??= [PcTexture.Read(Read(MagicArchive(), BubblePath + "enmaku.tex"))];
        Matrix4x4 rotation = BattleRigidPose.Rotation(new(draw.Pitch, draw.Yaw, 1024));
        Matrix4x4 scale = Matrix4x4.CreateScale(draw.Width / 4096f, draw.Length / 4096f, draw.Width / 4096f);
        Matrix4x4 transform = scale * rotation * Matrix4x4.CreateTranslation(draw.Position.Position);
        DrawModel(renderer, bubbleCloud, bubbleCloudTextures, transform, view, new() { Cull = true }, Math.Min(255, draw.Brightness * 2) / 255f);
    }

    private void DrawBubbleRibbon(ITriangleRenderer renderer, Ff7BattlePrimaryBubbleRibbonDraw draw, CameraView view)
    {
        if (draw.Brightness == 0)
        {
            return;
        }
        if (bubbleRibbon is null)
        {
            PcTexture texture = PcTexture.Read(Read(MagicArchive(), "mizu_1.tex"));
            byte[] header = texture.Header.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0xD0), (0x7908 >> 6) - BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0xE0)));
            bubbleRibbon = Ff7FieldRenderer.DecodeTexture(texture with { Header = header });
        }
        Vector3 anchor = OffsetTowardCamera(view.Transform(draw.Position.Position), -1000);
        float scale = 8 * draw.Scale / 4096f;
        Vector4 color = new(new Vector3(Math.Min(255, draw.Brightness * 2) / 255f), 1);
        TriangleMaterial material = new(bubbleRibbon, TriangleBlend.Add, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointClamp);
        CameraSpaceVertex Vertex(int x, int y, int u, int v) => new(anchor + new Vector3(x * scale, -y * scale, 0), new((u + 0.5f) / bubbleRibbon.Width, (v + 0.5f) / bubbleRibbon.Height), color);
        for (int segment = 0; segment < 32; segment++)
        {
            int v0 = segment * 240 / 32;
            int v1 = (segment + 1) * 240 / 32;
            int x0 = Sine(draw.Angle + segment * 256);
            int x1 = Sine(draw.Angle + (segment + 1) * 256);
            CameraSpaceVertex a = Vertex(x0 - 120, v0 - 120, 0, v0);
            CameraSpaceVertex b = Vertex(x0 + 120, v0 - 120, 240, v0);
            CameraSpaceVertex c = Vertex(x1 - 120, v1 - 120, 0, v1);
            CameraSpaceVertex d = Vertex(x1 + 120, v1 - 120, 240, v1);
            CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, view.Width, view.Height, view.Focal, material);
            CameraSpaceTriangleSubmission.Draw(renderer, b, d, c, view.Width, view.Height, view.Focal, material);
        }

        static int Sine(int angle) => (int)(Math.Sin(angle * 0.087890625f * (Math.PI / 180)) * 4096) * 8 >> 12;
    }
}
