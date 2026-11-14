using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Original overworld light matrices and the terrain float-color callback.</summary>
public static class Ff7WorldLighting
{
    public static Vector4 Terrain(Vector3 cameraNormal)
    {
        float factor = (cameraNormal.X + cameraNormal.Y + cameraNormal.Z) * (110f / 255) + 64f / 255;
        float channel = Channel(factor, 1);
        return new(channel, channel, channel, 1);
    }

    public static Vector4 Character(Vector3 cameraNormal, Vector4 original, bool underwater = false)
    {
        if (underwater)
        {
            float Light(float ambient, float color) => Channel(MathF.Max(ambient / 255, cameraNormal.Z + ambient / 255), color);
            return new(Light(32, original.X), Light(32, original.Y), Light(48, original.Z), original.W);
        }
        Vector3 first = new(659f / 4096, 4034f / 4096, -411f / 4096);
        Vector3 second = new(-1536f / 4096, -1839f / 4096, 3349f / 4096);
        Vector3 third = new(2920f / 4096, -2484f / 4096, 1474f / 4096);
        Vector3 coefficients = first * (110f / 255) + second * (170f / 255) + third * (60f / 255);
        float ambient = 64f / 255;
        float factor = MathF.Max(ambient, Vector3.Dot(coefficients, cameraNormal) + ambient);
        return new(Channel(factor, original.X), Channel(factor, original.Y), Channel(factor, original.Z), original.W);
    }

    public static int UnderwaterModelWeight(int cameraDepth)
    {
        return Math.Clamp(255 - ((cameraDepth / 4 - 4000) >> 3), 0, 255);
    }

    public static Vector4 BlendModelColor(Vector4 source, int weight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(weight);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, 255);
        float Channel(float value) => MathF.Round(value * weight, MidpointRounding.ToEven) / 255;
        return new(Channel(source.X), Channel(source.Y), Channel(source.Z), source.W);
    }

    private static float Channel(float factor, float original)
    {
        return MathF.Round(Math.Clamp(factor * original * 255, 0, 255), MidpointRounding.ToEven) / 255;
    }
}
