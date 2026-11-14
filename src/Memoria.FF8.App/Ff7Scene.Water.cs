using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Vector2N = System.Numerics.Vector2;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    internal static Ff7ModelRenderOverrides CreateWaterModelEffects(Ff7NativeWaterEffectState water, Ff7NativePartVisibilityState? visibility = null)
    {
        water.Validate();
        visibility?.Validate();
        Ff7NativeWaterEffectSample sample = water is { OwnsColors: true, DisplayedSample: { } displayed } ? displayed : throw new InvalidDataException("Native water colors require their captured material owner.");
        Dictionary<int, bool[]> masks = sample.Parts.ToDictionary(part => part.Part, part => part.ModifiedVertices);
        return new(part => visibility is null || Ff7NativePartVisibility.PartVisible(visibility, part), null, false)
        {
            SourceVertexColor = (part, vertex, original) =>
            {
                if (!masks.TryGetValue(part, out bool[]? mask))
                {
                    return original;
                }
                if ((uint)vertex >= mask.Length)
                {
                    throw new InvalidDataException("Native water colors require their original vertex topology.");
                }
                return mask[vertex] ? Ff7NativeColorOffset.Shade(original, sample.Parameters.Red, sample.Parameters.Green, sample.Parameters.Blue) : original;
            }
        };
    }

    private static Ff7FieldWaterSplashVisual[] WaterSplashes(Ff7FieldProcess process)
    {
        return process.NativeWaterSplashes.OrderBy(splash => splash.TextureIndex).Select(splash => new Ff7FieldWaterSplashVisual(splash.TextureIndex, new Vector2N(splash.X, splash.Y), splash.Depth, splash.HalfSize)).ToArray();
    }
}
