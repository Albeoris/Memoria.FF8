using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Native model RGB modulates an existing foreign material around the native neutral value128.</summary>
public readonly record struct CrossoverNativeActorColor(uint Color)
{
    public Vector4 Apply(Vector4 color) => new(color.X * (byte)Color / 128f, color.Y * (byte)(Color >> 8) / 128f, color.Z * (byte)(Color >> 16) / 128f, color.W);
}
