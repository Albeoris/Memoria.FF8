namespace Memoria.FF8.Ui;

/// <summary>Resolution-independent paper pattern sampled directly at the destination pixel size.</summary>
public static class UiPaper
{
    public static UiColor[] CreatePixels(int width, int height, UiColor color)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        UiColor[] pixels = new UiColor[checked(width * height)];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)height;
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                float mottling = Noise(u * 30, v * 23) * 9 + Noise(u * 110, v * 83) * 5 + Noise(u * 280, v * 210) * 3;
                float grain = Hash(x, y) * 2;
                float fibers = Noise(u * 600, v * 130) * 1.5f;
                float variation = mottling + grain + fibers;
                pixels[y * width + x] = new UiColor(Channel(color.R, variation), Channel(color.G, variation), Channel(color.B, variation), color.A);
            }
        }
        return pixels;
    }

    private static byte Channel(byte value, float variation)
    {
        return (byte)Math.Clamp((int)MathF.Round(value + variation), 0, 255);
    }

    private static float Noise(float x, float y)
    {
        int left = (int)x;
        int top = (int)y;
        float u = x - left;
        float v = y - top;
        u = u * u * (3 - 2 * u);
        v = v * v * (3 - 2 * v);
        float a = Hash(left, top);
        float b = Hash(left + 1, top);
        float c = Hash(left, top + 1);
        float d = Hash(left + 1, top + 1);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    private static float Hash(int x, int y)
    {
        uint value = unchecked((uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^ 0x5f3759dfu);
        value ^= value >> 16;
        value = unchecked(value * 0x7feb352du);
        value ^= value >> 15;
        return (value & 65535) / 32767.5f - 1;
    }
}
