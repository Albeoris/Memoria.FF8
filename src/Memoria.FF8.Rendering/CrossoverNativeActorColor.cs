using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Native model RGB modulates an existing foreign material around the native neutral value128.</summary>
public readonly record struct CrossoverNativeActorColor(uint Color)
{
    public bool SemiTransparent => (Color & 0x02000000) != 0;

    public NativeBattlePrimitiveColor Diffuse(int blend) => NativeBattlePrimitiveColorState.FromColdProcess().Textured(Color, SemiTransparent, blend, false);

    public Vector4 Apply(Vector4 color, int blend = 0)
    {
        NativeBattlePrimitiveColor diffuse = Diffuse(blend);
        return color * new Vector4(diffuse.Rgb / 255f, diffuse.Alpha / 255f);
    }

    public TriangleMaterial ApplyMaterial(TriangleMaterial material, int blend)
    {
        TriangleBlend equation = !SemiTransparent ? TriangleBlend.Replace : blend switch
        {
            0 or 2 => TriangleBlend.Alpha,
            1 => TriangleBlend.Add,
            3 => TriangleBlend.AlphaAdd,
            _ => throw new NotSupportedException("The host material has no native semi-transparent blend operation.")
        };
        return material with { Blend = equation, DepthWrite = !SemiTransparent, MinimumAlpha = material.Texture is null ? 0 : !SemiTransparent ? 112f / 255 : blend == 0 ? 122f / 255 : 1f / 255, TextureSelectsAlpha = true, PreserveTargetAlpha = true, CentroidTextureSampling = material.Texture is not null };
    }

    public Vector4 ApplyHost(Vector4 color, int blend) => Apply(color, blend == 4 ? 0 : blend);

    public TriangleMaterial ApplyHostMaterial(TriangleMaterial material, int blend)
    {
        if (!SemiTransparent)
        {
            return material;
        }
        TriangleBlend equation = blend switch
        {
            0 or 2 or 4 => TriangleBlend.Alpha,
            1 => TriangleBlend.Add,
            3 => TriangleBlend.AlphaAdd,
            _ => throw new NotSupportedException("The host actor material has an unsupported blend operation.")
        };
        return material with { Blend = equation, DepthWrite = false, TextureSelectsAlpha = false };
    }
}
