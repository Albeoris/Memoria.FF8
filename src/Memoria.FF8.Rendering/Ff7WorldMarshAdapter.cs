using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldMarshSegment(int Index, Vector3 Position, float Direction);

/// <summary>Original twelve Midgar marsh quads from the simulation-owned point ring.</summary>
public sealed class Ff7WorldMarshAdapter(PcTexture texture, Ff7WorldPresentationData data)
{
    private readonly Ff7WorldMath math = new(data);
    private readonly Dictionary<int, PixelImage> palettes = [];

    public int Draw(ITriangleRenderer renderer, Ff7WorldView view, Vector3 focus, int groundHeight, int curvatureThreshold, IReadOnlyList<Ff7WorldChunkPlacement> chunks, IReadOnlyList<Ff7WorldMarshSegment> segments)
    {
        int submitted = 0;
        foreach (Ff7WorldMarshSegment segment in segments)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(segment.Index);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(segment.Index, 11);
            Vector3 position = segment.Position;
            if (Math.Abs(position.X - focus.X) > 30000 || Math.Abs(position.Z - focus.Z) > 30000 || !chunks.Any(chunk => chunk.ChunkX == (int)position.X >> 13 && chunk.ChunkZ == (int)position.Z >> 13))
            {
                continue;
            }
            int palette = segment.Index < 4 ? 7 - segment.Index : segment.Index >= 8 ? 11 - segment.Index : 3;
            if (!palettes.TryGetValue(palette, out PixelImage? image))
            {
                image = Ff7FieldRenderer.DecodeTexture(texture, useColorKey: true, paletteOverride: palette);
                palettes.Add(palette, image);
            }
            int curvature = Ff7WorldFootShadowAdapter.Curvature(position, view, groundHeight, curvatureThreshold);
            Vector3 center = position with { Y = unchecked((short)((int)position.Y - curvature * 4)) };
            Matrix4x4 rotation = math.Rotation(0, (int)MathF.Round(segment.Direction) + 2048, 0);
            submitted += Ff7WorldFootShadowAdapter.DrawGroundQuad(renderer, image, view, center, 300, 300, rotation, new(palette * 16, 0, palette * 16 + 15, 15), new(128 / 255f), TriangleBlend.Replace);
        }
        return submitted;
    }
}
