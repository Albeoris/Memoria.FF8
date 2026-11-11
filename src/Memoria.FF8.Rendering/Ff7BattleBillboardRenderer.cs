using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

internal sealed class Ff7BattleBillboardRenderer
{
    private readonly IReadOnlyList<Quad>[] frames;
    private readonly PcTexture[] pages;
    private readonly Dictionary<(int Page, int Palette), PixelImage> images = [];

    public Ff7BattleBillboardRenderer(IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> billboard, PcTexture[] pages, Func<ushort, int> selectPage, TriangleBlend blend = TriangleBlend.Replace, int? paletteOverride = null, bool nativePageOrigin = false)
    {
        this.pages = pages;
        frames = new IReadOnlyList<Quad>[billboard.Count];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            Quad[] quads = new Quad[billboard[frame].Count];
            for (int index = 0; index < quads.Length; index++)
            {
                BattleBillboardQuad source = billboard[frame][index];
                int page = selectPage(source.TexturePage);
                PcTexture texture = pages[page];
                if (nativePageOrigin)
                {
                    int u = BinaryPrimitives.ReadInt32LittleEndian(texture.Header.Span[0xD8..]);
                    int v = BinaryPrimitives.ReadInt32LittleEndian(texture.Header.Span[0xDC..]);
                    source = source with { U = checked((short)(source.U - u)), V = checked((short)(source.V - v)) };
                }
                int palette = paletteOverride ?? ((source.Clut >> 6) - BinaryPrimitives.ReadInt32LittleEndian(texture.Header.Span[0xE0..]));
                if ((uint)palette >= texture.Palettes.Count || (uint)source.U >= texture.Width || (uint)source.V >= texture.Height || source.Width == 0 || source.Height == 0 || source.U + source.Width > texture.Width || source.V + source.Height > texture.Height)
                {
                    throw new InvalidDataException("FFVII battle billboard quad references data outside its original page.");
                }
                quads[index] = new(source.X, source.Y, source.U, source.V, source.Width, source.Height, page, palette, (ushort)(source.Flags & 3), blend);
            }
            frames[frame] = quads;
        }
    }

    public int FrameCount => frames.Length;

    public void Draw(ITriangleRenderer renderer, int frame, Vector3 anchor, int width, int height, float focal, float scale = 1, int flip = 0, float brightness = 1, Func<Vector3, Vector3>? transform = null)
    {
        if ((uint)frame >= frames.Length)
        {
            throw new InvalidDataException("FFVII battle billboard continuation lies outside its original frames.");
        }
        float scaleX = (flip & 1) != 0 ? -scale : scale;
        float scaleY = (flip & 2) != 0 ? -scale : scale;
        foreach (Quad quad in frames[frame])
        {
            PixelImage image = Image(quad.Page, quad.Palette);
            float u0 = (quad.U + 0.5f) / image.Width;
            float u1 = (quad.U + quad.Width - 0.5f) / image.Width;
            float v0 = (quad.V + 0.5f) / image.Height;
            float v1 = (quad.V + quad.Height - 0.5f) / image.Height;
            if ((quad.Flip & 1) != 0)
            {
                (u0, u1) = (u1, u0);
            }
            if ((quad.Flip & 2) != 0)
            {
                (v0, v1) = (v1, v0);
            }

            CameraSpaceVertex Vertex(int x, int y, float u, float v)
            {
                Vector3 position = transform is null ? anchor + new Vector3(x * 8 * scaleX, -y * 8 * scaleY, 0) : transform(new(x * 8 * scaleX, y * 8 * scaleY, 0));
                return new(position, new(u, v), new(brightness, brightness, brightness, 1));
            }

            CameraSpaceVertex a = Vertex(quad.X, quad.Y, u0, v0);
            CameraSpaceVertex b = Vertex(quad.X + quad.Width, quad.Y, u1, v0);
            CameraSpaceVertex c = Vertex(quad.X, quad.Y + quad.Height, u0, v1);
            CameraSpaceVertex d = Vertex(quad.X + quad.Width, quad.Y + quad.Height, u1, v1);
            TriangleMaterial material = new(image, quad.Blend, TriangleDepthComparison.GreaterEqual, false, Sampler: TriangleSampler.PointClamp);
            CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, width, height, focal, material);
            CameraSpaceTriangleSubmission.Draw(renderer, b, d, c, width, height, focal, material);
        }
    }

    private PixelImage Image(int page, int palette)
    {
        if (!images.TryGetValue((page, palette), out PixelImage? image))
        {
            PcTexture texture = pages[page];
            byte[] header = texture.Header.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0xD0), palette);
            image = Ff7FieldRenderer.DecodeTexture(texture with { Header = header });
            images.Add((page, palette), image);
        }
        return image;
    }

    private sealed record Quad(short X, short Y, short U, short V, int Width, int Height, int Page, int Palette, ushort Flip, TriangleBlend Blend);
}
