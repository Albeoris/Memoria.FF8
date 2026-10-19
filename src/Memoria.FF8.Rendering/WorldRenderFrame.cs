using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public enum WorldRenderObjectKind
{
    Allocation,
    Rail,
    Controlled
}

public readonly record struct WorldRenderObjectId(WorldRenderObjectKind Kind, int Index, int Group = 0);

/// <summary>A captured logical world sample and its independent presentation values.</summary>
public sealed record WorldRenderFrame(Vector3 Position, float Heading, int AnimationId, float AnimationFrame, WorldCamera Camera, WorldSky Sky, bool AvatarVisible = true, FieldModel? AvatarModel = null, IReadOnlyList<NativeWorldStaticModelDraw>? StaticModels = null, NativeWorldForestState? Forest = null, IReadOnlyList<NativeWorldAnimatedModelDraw>? AnimatedModels = null)
{
    public IReadOnlyList<NativeWorldStaticModelDraw>? StaticModels { get; init; } = StaticModels is null ? null : Array.AsReadOnly(StaticModels.ToArray());
    public NativeWorldForestState? Forest { get; init; } = Forest is null ? null : Forest with { Particles = Forest.Particles.ToArray() };
    public IReadOnlyList<NativeWorldAnimatedModelDraw>? AnimatedModels { get; init; } = AnimatedModels is null ? null : Array.AsReadOnly(AnimatedModels.ToArray());
    public WorldAnimationBlend? AvatarAnimationBlend { get; init; }
    public IReadOnlyList<WorldForestParticleFrame>? ForestParticles { get; init; }
}

public readonly record struct WorldAnimationBlend(int PreviousFrameIndex, int CurrentFrameIndex, float Blend);

/// <summary>Floating presentation geometry without altering the native particle or random stream.</summary>
public readonly record struct WorldForestParticleFrame(NativeWorldForestParticle Particle, Vector3 Position, Matrix4x4 Rotation, float Scale, bool Interpolated)
{
    public ushort PreviousScale { get; init; }
    public float Blend { get; init; }
}
