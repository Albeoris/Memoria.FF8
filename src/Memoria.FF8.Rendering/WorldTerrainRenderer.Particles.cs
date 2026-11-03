using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private void DrawForest(NativeWorldForestState forest, Vector3 position, IReadOnlyList<WorldForestParticleFrame>? presentation, WorldCamera camera)
    {
        forest.Validate();
        if (!forest.Particles.Any(particle => particle.Visible))
        {
            return;
        }
        NativeWorldPathProjectionCertificate projection = camera.ParticleProjection ?? throw new NotSupportedException("World particle drawing requires its retained native camera projection certificate.");
        short[] inverse = projection.ViewRotation?.ToArray() ?? throw new NotSupportedException("World particle drawing requires a unique native view matrix.");
        NativeWorldPathPosition nativeTranslation = projection.ViewTranslation ?? throw new NotSupportedException("World particle drawing requires a unique native view translation.");
        Vector3 translation = new(nativeTranslation.X, nativeTranslation.Y, nativeTranslation.Z);
        int nativeFocal = projection.Camera.FootState?.ProjectionDistance ?? 1024;
        List<(int Depth, Action Draw)> transparent = [];
        IEnumerable<WorldForestParticleFrame> particles = presentation ?? forest.Particles.Where(particle => particle.Visible).Select(particle => new WorldForestParticleFrame(particle, new Vector3(particle.X, particle.NativeY, -particle.GraphicsZ), Matrix4x4.Identity, particle.Scale, false));
        foreach (WorldForestParticleFrame visual in particles.OrderByDescending(visual => visual.Particle.Slot))
        {
            NativeWorldForestParticle particle = visual.Particle;
            NativeWorldForestEffect effect = assets.ForestTables.GetEffect(particle.Type);
            Vector3 sourcePosition = visual.PreviousParticle is null ? visual.Position : new Vector3(particle.X, particle.NativeY, -particle.GraphicsZ);
            NativeWorldPathPosition source = new((int)MathF.Round(sourcePosition.X), (int)MathF.Round(sourcePosition.Y), (int)MathF.Round(-sourcePosition.Z));
            NativeWorldPathNodeProjection node = projection.ProjectNode(source) ?? throw new NotSupportedException(projection.UnsupportedReason ?? "World particle anchor projection is unsupported.");
            if (!node.Visible)
            {
                continue;
            }
            Vector3 anchor = new(node.Words.X, node.Words.Y, node.Words.Z);
            var centerProjection = NativeWorldParticleMath.Project(NativeWorldParticleMath.Transform(inverse, anchor) + translation, nativeFocal);
            int halfSize = unchecked(particle.Scale * effect.HalfSize) >> 13;
            if (effect.DrawMode == 1)
            {
                DrawNativeForestTile(centerProjection, halfSize, effect, PreviousParticleQuad(camera, visual, particle, effect), camera.ParticleProjectionBlend);
                continue;
            }
            int frame = Math.Clamp(particle.Age / effect.FrameDuration, 0, effect.Frames - 1);
            PixelImage texture = assets.GetForestTexture(particle.Type, frame);
            short[] rotation = NativeWorldParticleMath.Rotation(particle.RotationX, particle.Yaw, particle.Roll);
            var projected = NativeWorldParticleMath.ProjectQuad(inverse, translation, anchor, rotation, halfSize, effect.DrawMode, nativeFocal);
            int ordering = NativeWorldParticleMath.OrderingDepth(projected[0].Depth, projected[1].Depth, projected[2].Depth, projected[3].Depth, effect.DrawMode);
            if (ordering < 0)
            {
                continue;
            }
            float reciprocal = NativeWorldParticleMath.OrderingReciprocal(ordering);
            Vector4 tint = new(effect.Red / 128f, effect.Green / 128f, effect.Blue / 128f, 1);
            var previousProjected = PreviousParticleQuad(camera, visual, particle, effect);
            ScreenVertex Vertex(int index, Vector2 uv)
            {
                Vector2 point = new(projected[index].X, projected[index].Y);
                if (previousProjected is not null)
                {
                    point = Vector2.Lerp(new Vector2(previousProjected[index].X, previousProjected[index].Y), point, camera.ParticleProjectionBlend);
                }
                return new ScreenVertex(point.X * Width / 2560f, point.Y * Height / 1792f, 1, uv, tint, reciprocal);
            }
            Vector2 end = new((texture.Width - 1f) / texture.Width, (texture.Height - 1f) / texture.Height);
            ScreenVertex a = Vertex(0, Vector2.Zero);
            ScreenVertex b = Vertex(1, new Vector2(end.X, 0));
            ScreenVertex c = Vertex(2, new Vector2(0, end.Y));
            ScreenVertex d = Vertex(3, end);
            TriangleBlend blend = effect.BlendMode switch
            {
                0 => TriangleBlend.HalfAdd,
                1 => TriangleBlend.Add,
                2 => TriangleBlend.Subtract,
                3 => TriangleBlend.QuarterAdd,
                _ => throw new InvalidDataException("Invalid native world particle blend.")
            };
            void DrawPass(bool stp)
            {
                DrawNativeWorldParticleTriangle(a, b, c, texture, blend, stp);
                DrawNativeWorldParticleTriangle(c, b, d, texture, blend, stp);
            }
            DrawPass(false);
            transparent.Add((ordering, () => DrawPass(true)));
        }
        foreach (var primitive in transparent.OrderByDescending(primitive => primitive.Depth))
        {
            primitive.Draw();
        }
    }

    private static (int X, int Y, int Depth)[]? PreviousParticleQuad(WorldCamera camera, WorldForestParticleFrame visual, NativeWorldForestParticle current, NativeWorldForestEffect effect)
    {
        if (camera.PreviousParticleProjection is not { } projection || camera.ParticleProjectionBlend >= 1)
        {
            return null;
        }
        NativeWorldForestParticle particle = visual.PreviousParticle ?? current;
        NativeWorldPathNodeProjection node = projection.ProjectNode(new NativeWorldPathPosition(particle.X, particle.NativeY, particle.GraphicsZ)) ?? throw new NotSupportedException(projection.UnsupportedReason);
        if (!node.Visible)
        {
            return null;
        }
        short[] view = projection.ViewRotation?.ToArray() ?? throw new NotSupportedException("Previous particle camera has no unique native matrix.");
        NativeWorldPathPosition translation = projection.ViewTranslation ?? throw new NotSupportedException("Previous particle camera has no unique native translation.");
        Vector3 anchor = new(node.Words.X, node.Words.Y, node.Words.Z);
        short[] rotation = NativeWorldParticleMath.Rotation(particle.RotationX, particle.Yaw, particle.Roll);
        int halfSize = unchecked(particle.Scale * effect.HalfSize) >> 13;
        return NativeWorldParticleMath.ProjectQuad(view, new Vector3(translation.X, translation.Y, translation.Z), anchor, rotation, halfSize, effect.DrawMode, projection.Camera.FootState?.ProjectionDistance ?? 1024);
    }

    private void DrawNativeWorldParticleTriangle(ScreenVertex a, ScreenVertex b, ScreenVertex c, PixelImage texture, TriangleBlend blend, bool stp)
    {
        if (TriangleRenderer is not null)
        {
            DrawGpuTriangle(a, b, c, texture, stp, blend, stp);
            DrawnTriangles++;
        }
        else
        {
            rasterTriangles.Add(new(a, b, c, texture, stp, blend, stp));
        }
    }

    private void DrawNativeForestTile((int X, int Y, int Depth) center, int halfSize, NativeWorldForestEffect effect, (int X, int Y, int Depth)[]? previous, float blend)
    {
        int ordering = center.Depth >> 3;
        if ((center.Depth >> 2) > 8191)
        {
            return;
        }
        short size = unchecked((short)(2 * halfSize));
        if (size == 0)
        {
            size = 8;
        }
        float reciprocal = NativeWorldParticleMath.OrderingReciprocal(ordering);
        ScreenVertex Vertex(int index, int x, int y)
        {
            Vector2 point = new(x, y);
            if (previous is not null)
            {
                point = Vector2.Lerp(new Vector2(previous[index].X, previous[index].Y), point, blend);
            }
            return new ScreenVertex(point.X * Width / 2560f, point.Y * Height / 1792f, 1, Vector2.Zero, NativeOrderingReciprocal: reciprocal);
        }
        ScreenVertex a = Vertex(0, center.X - halfSize, center.Y - halfSize);
        ScreenVertex b = Vertex(1, center.X - halfSize + size, center.Y - halfSize);
        ScreenVertex c = Vertex(2, center.X - halfSize, center.Y - halfSize + size);
        ScreenVertex d = Vertex(3, center.X - halfSize + size, center.Y - halfSize + size);
        PixelImage texture = new(1, 1, [new Rgba32(effect.Red, effect.Green, effect.Blue)]);
        if (TriangleRenderer is not null)
        {
            DrawGpuTriangle(a, b, c, texture, false);
            DrawGpuTriangle(c, b, d, texture, false);
            DrawnTriangles += 2;
        }
        else
        {
            rasterTriangles.Add(new(a, b, c, texture, false));
            rasterTriangles.Add(new(c, b, d, texture, false));
        }
    }
}
