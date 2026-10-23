namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSonic139Bounds(short MinX, short MinY, short MaxX, short MaxY, int MinimumDepth, bool Visible)
{
    public static NativeBattleSonic139Bounds Initial => new(32767, 32767, -32767, -32767, 65536, false);
}

public static partial class NativeBattleSonic139Controller
{
    public static NativeBattleSonic139Bounds IncludePoint6662C0(NativeBattleSonic139Bounds bounds, short x, short y, short depth)
    {
        if (depth <= 0)
        {
            return bounds;
        }
        return new(Math.Min(bounds.MinX, x), Math.Min(bounds.MinY, y), Math.Max(bounds.MaxX, x), Math.Max(bounds.MaxY, y), Math.Min(bounds.MinimumDepth, depth), true);
    }

    public static (NativeBattleSonic139Frame Frame, NativeBattleSonic139Bounds Bounds) IncludeActor6A9AD0(NativeBattleSonic139Frame frame, NativeBattleGraphicsModel model, NativeBattleSonic139Bounds bounds, NativeBattleSonic139Inputs inputs)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Sonic Wave target bounds require its actual transform WORDs.");
        short size = unchecked((short)(transform.Scale38 + 256));
        NativeBattleVector bottom = transform.Position28;
        NativeBattleVector top = bottom with { Y = unchecked((short)(bottom.Y - size)) };
        int radius = size;
        for (int index = 1; index >= 0; index--)
        {
            (frame, uint screen, short depth) = Project(frame, index == 1 ? top : bottom, inputs.ReadView(frame), inputs);
            if (index == 1)
            {
                if (depth == 0)
                {
                    throw new InvalidDataException("Sonic Wave target radius reaches its original projected zero divisor.");
                }
                radius = unchecked(size * 54) / depth;
            }
            short x = unchecked((short)(unchecked((short)screen) >> 3));
            short y = unchecked((short)(unchecked((short)(screen >> 16)) >> 3));
            short left = unchecked((short)(x - radius));
            bounds = IncludePoint6662C0(bounds, left, y, depth);
            bounds = IncludePoint6662C0(bounds, unchecked((short)(left + radius * 2)), y, depth);
        }
        return (frame, bounds);
    }
}
