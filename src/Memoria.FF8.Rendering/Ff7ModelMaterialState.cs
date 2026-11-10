using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed class Ff7ModelMaterialState
{
    public TriangleBlend Blend = TriangleBlend.Replace;
    public bool DepthTest = true;
    public bool DepthWrite = true;
    public bool Cull;
    public bool Gouraud = true;
    public float AlphaReference = 1f / 255;
    public TriangleSampler Sampler = TriangleSampler.PointWrap;

    public void Apply(PcRenderState state)
    {
        const uint supportedStateMask = 0x2 | 0x4 | 0x8 | 0x100 | 0x200 | 0x400 | 0x800 | 0x2000 | 0x4000 | 0x8000 | 0x10000 | 0x20000;
        if ((state.Features & ~supportedStateMask) != 0)
        {
            throw new NotSupportedException($"FF7 P state update mask {state.Features:X8} contains unsupported required features {state.Features & ~supportedStateMask:X8}.");
        }
        bool Updated(uint flag) => (state.Features & flag) != 0;
        bool Enabled(uint flag) => (state.Options & flag) != 0;
        if (Updated(0x400))
        {
            Blend = !Enabled(0x400) ? TriangleBlend.Replace : state.BlendMode switch
            {
                0 => TriangleBlend.HalfAdd,
                1 => TriangleBlend.Add,
                2 => TriangleBlend.Subtract,
                3 => TriangleBlend.QuarterAdd,
                4 => TriangleBlend.Replace,
                _ => throw new NotSupportedException($"FF7 P blend mode {state.BlendMode} is unsupported.")
            };
        }
        if (Updated(0x8000))
        {
            DepthTest = Enabled(0x8000);
        }
        if (Updated(0x10000))
        {
            DepthWrite = Enabled(0x10000);
        }
        if (Updated(0x800))
        {
            AlphaReference = Enabled(0x800) ? state.AlphaReference / 255f : 1f / 255;
        }
        if (Updated(0x4))
        {
            Sampler = Enabled(0x4) ? TriangleSampler.LinearWrap : TriangleSampler.PointWrap;
        }
        if (Updated(0x2000))
        {
            Cull = Enabled(0x2000);
        }
        if (Updated(0x4000) && Enabled(0x4000))
        {
            Cull = false;
        }
        if (Updated(0x20000))
        {
            Gouraud = Enabled(0x20000) && state.ShadeMode switch
            {
                1 => false,
                2 => true,
                _ => throw new NotSupportedException($"FFVII P shade mode {state.ShadeMode} requires its native interpolation contract.")
            };
        }
    }
}
