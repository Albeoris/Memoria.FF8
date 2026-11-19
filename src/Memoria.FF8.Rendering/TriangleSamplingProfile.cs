namespace Memoria.FF8.Rendering;

internal readonly record struct TriangleSamplingProfile(TriangleSampler Sampler, bool Centroid)
{
    public static TriangleSamplingProfile PixelExact => new(TriangleSampler.PointClamp, false);
    public static TriangleSamplingProfile Video => new(TriangleSampler.LinearClamp, false);

    // Models keep the texture filter and addressing chosen by their source material.
    public static TriangleSamplingProfile Model(TriangleSampler sampler)
    {
        return new(sampler, true);
    }

    public TriangleMaterial Apply(TriangleMaterial material)
    {
        return material with { Sampler = Sampler, CentroidTextureSampling = Centroid };
    }
}
