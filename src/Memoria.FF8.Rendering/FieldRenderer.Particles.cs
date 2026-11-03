using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private void PrepareFieldParticles(IReadOnlyList<NativeFieldParticleSprite>? particles, PixelImage? texture, List<(float Depth, Action Draw)> translucent)
    {
        if (particles is null || particles.Count == 0)
        {
            return;
        }
        if (texture is null || texture.Width != 256 || texture.Height != 256)
        {
            throw new NotSupportedException("Field particle drawing requires its complete 256x256 texture page and CLUT.");
        }
        foreach (NativeFieldParticleSprite particle in particles.OrderByDescending(particle => particle.PoolIndex))
        {
            ProjectedPoint centre = ProjectFieldParticle(particle);
            int orderingIndex = (int)MathF.Truncate(centre.Depth / 4) + particle.DepthOffset;
            if (orderingIndex <= 0 || orderingIndex >= 4096)
            {
                continue;
            }
            int perspectiveScale = ((int)camera.FocalLength << 14) / orderingIndex;
            int shift = particle.DoubleSize ? 10 : 11;
            short halfWidth = unchecked((short)((perspectiveScale * particle.Width) >> shift));
            short halfHeight = unchecked((short)((perspectiveScale * particle.Height) >> shift));
            int cosine = NativeBattleCameraMath.Cosine(particle.Angle);
            int sine = NativeBattleCameraMath.Sine(particle.Angle);
            Vector2 first = RotateParticleCorner(halfWidth, unchecked((short)-halfHeight), cosine, sine);
            Vector2 second = RotateParticleCorner(halfWidth, halfHeight, cosine, sine);
            float orderingDepth = orderingIndex * 4;
            ProjectedPoint a = ParticleCorner(centre, -second, orderingDepth);
            ProjectedPoint b = ParticleCorner(centre, first, orderingDepth);
            ProjectedPoint c = ParticleCorner(centre, -first, orderingDepth);
            ProjectedPoint d = ParticleCorner(centre, second, orderingDepth);
            byte right = unchecked((byte)(particle.U + particle.TextureWidth - 1));
            byte bottom = unchecked((byte)(particle.V + particle.TextureHeight - 1));
            Vector2 uvA = new(particle.U, particle.V);
            Vector2 uvB = new(right, particle.V);
            Vector2 uvC = new(particle.U, bottom);
            Vector2 uvD = new(right, bottom);
            Vector3 color = new(particle.Red, particle.Green, particle.Blue);
            MapBlendMode blend = particle.BlendMode == 4 ? MapBlendMode.None : (particle.BlendMode & 3) switch
            {
                0 => MapBlendMode.Average,
                1 => MapBlendMode.Add,
                2 => MapBlendMode.Subtract,
                _ => MapBlendMode.QuarterAdd
            };
            void Draw(bool transparentPass)
            {
                DrawFieldParticleTriangle(a, b, c, uvA, uvB, uvC, texture, color, blend, transparentPass);
                DrawFieldParticleTriangle(b, d, c, uvB, uvD, uvC, texture, color, blend, transparentPass);
            }
            Draw(false);
            if (blend != MapBlendMode.None)
            {
                translucent.Add((orderingDepth, () => Draw(true)));
            }
        }
    }

    private ProjectedPoint ProjectFieldParticle(NativeFieldParticleSprite particle)
    {
        static int Coordinate(Vector3 axis, NativeFieldParticleSprite point, float translation)
        {
            return unchecked((int)Math.Round(axis.X * (double)point.X + axis.Y * (double)point.Y + axis.Z * (double)point.Z) + (int)translation);
        }
        int x = Coordinate(camera.AxisX, particle, camera.Translation.X);
        int y = Coordinate(camera.AxisY, particle, camera.Translation.Y);
        int z = Coordinate(camera.AxisZ, particle, camera.Translation.Z);
        int nativeDepth = Math.Clamp(z, 0, ushort.MaxValue);
        int projectionDepth = Math.Max(nativeDepth, (int)camera.FocalLength >> 1);
        if (projectionDepth == 0)
        {
            throw new NotSupportedException("Field particle projection requires a nonzero focal length.");
        }
        double perspective = camera.FocalLength / (double)projectionDepth;
        int screenX = Math.Clamp(unchecked((int)Math.Round(8 * x * perspective)), -8192, 8184);
        int screenY = Math.Clamp(unchecked((int)Math.Round(8 * y * perspective)), -8192, 8184);
        return new ProjectedPoint(160 + camera.Pan.X + screenX / 8f, 120 + camera.Pan.Y + screenY / 8f, nativeDepth);
    }
    private static Vector2 RotateParticleCorner(short x, short y, int cosine, int sine)
    {
        return new Vector2(unchecked((short)(int)Math.Round((x * (double)cosine - y * (double)sine) / 4096)), unchecked((short)(int)Math.Round((x * (double)sine + y * (double)cosine) / 4096)));
    }

    private ProjectedPoint ParticleCorner(ProjectedPoint centre, Vector2 offset, float orderingDepth)
    {
        short x = unchecked((short)(int)((centre.X - 160 - camera.Pan.X) * 8 + offset.X));
        short y = unchecked((short)(int)((centre.Y - 120 - camera.Pan.Y) * 8 + offset.Y));
        return ToViewport(new ProjectedPoint(160 + camera.Pan.X + x / 8f, 120 + camera.Pan.Y + y / 8f, orderingDepth));
    }

    private void DrawFieldParticleTriangle(ProjectedPoint a, ProjectedPoint b, ProjectedPoint c, Vector2 uvA, Vector2 uvB, Vector2 uvC, PixelImage texture, Vector3 color, MapBlendMode blend, bool transparentPass)
    {
        float area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }
        if (TriangleRenderer is not null)
        {
            TriangleBlend gpuBlend = !transparentPass ? TriangleBlend.Replace : blend switch
            {
                MapBlendMode.Average => TriangleBlend.HalfAdd,
                MapBlendMode.Add => TriangleBlend.Add,
                MapBlendMode.Subtract => TriangleBlend.Subtract,
                MapBlendMode.QuarterAdd => TriangleBlend.QuarterAdd,
                _ => TriangleBlend.Replace
            };
            TriangleMaterial material = GpuMaterial(texture, gpuBlend, !transparentPass) with
            {
                MinimumAlpha = !transparentPass && blend != MapBlendMode.None ? 0.75f : 1f / 255,
                MaximumAlpha = transparentPass ? 0.75f : 1
            };
            Vector4 tint = new(color / 128, 1);
            Vector2 textureSize = new(texture.Width, texture.Height);
            TriangleRenderer.Draw(GpuVertex(a, uvA / textureSize, tint, perspective: false), GpuVertex(b, uvB / textureSize, tint, perspective: false), GpuVertex(c, uvC / textureSize, tint, perspective: false), material);
            return;
        }
        int minX = Math.Max((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), clipLeft);
        int maxX = Math.Min((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), clipRight - 1);
        int minY = Math.Max((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), clipTop);
        int maxY = Math.Min((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), clipBottom - 1);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float wA = Edge(b, c, x + 0.5f, y + 0.5f) / area;
                float wB = Edge(c, a, x + 0.5f, y + 0.5f) / area;
                float wC = Edge(a, b, x + 0.5f, y + 0.5f) / area;
                if (wA < 0 || wB < 0 || wC < 0 || (wA == 0 && !OwnsRasterEdge(b, c, area)) || (wB == 0 && !OwnsRasterEdge(c, a, area)) || (wC == 0 && !OwnsRasterEdge(a, b, area)))
                {
                    continue;
                }
                int index = y * OutputWidth + x;
                if (a.Depth > depth[index])
                {
                    continue;
                }
                Vector2 uv = uvA * wA + uvB * wB + uvC * wC;
                int u = Math.Clamp((int)MathF.Floor(uv.X), 0, 255);
                int v = Math.Clamp((int)MathF.Floor(uv.Y), 0, 255);
                Rgba32 texel = texture.Pixels[v * texture.Width + u];
                if (texel.A == 0 || transparentPass != (blend != MapBlendMode.None && texel.A == 128))
                {
                    continue;
                }
                WritePixel(index, Shade(texel, color), transparentPass ? blend : MapBlendMode.None);
                if (!transparentPass)
                {
                    depth[index] = a.Depth;
                }
            }
        }
    }
}
