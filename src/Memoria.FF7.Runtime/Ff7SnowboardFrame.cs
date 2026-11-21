using System.Numerics;

namespace Memoria.FF7.Runtime;

public enum Ff7SnowboardPhase
{
    Intro,
    Playing,
    Finishing,
    FadeOut,
    Finished
}

public sealed record Ff7SnowboardModelInstance(int ModelIndex, Matrix4x4 Transform)
{
    public int NativePrimitiveKind { get; init; }
    public int DrawArgument { get; init; }
}

public sealed record Ff7SnowboardFrame(long Tick, Ff7SnowboardPhase Phase, Vector3 RiderPositionQ12, Matrix4x4 View, Matrix4x4 Projection, IReadOnlyList<Ff7SnowboardModelInstance> Instances, byte FadeAlpha, int Course, int Segment, int Speed);
