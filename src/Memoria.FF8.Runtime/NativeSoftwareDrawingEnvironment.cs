using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareDrawingRectangle([property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] short Width, [property: JsonRequired] short Height);
public sealed record NativeSoftwareEnvironmentDerived([property: JsonRequired] ushort Width, [property: JsonRequired] ushort Height, [property: JsonRequired] int DrawPositionX, [property: JsonRequired] int DrawPositionY, [property: JsonRequired] int DrawOffsetX, [property: JsonRequired] int DrawOffsetY, [property: JsonRequired] NativeSoftwareDrawingRectangle Rectangle, [property: JsonRequired] NativeSoftwareClipBounds ClipBounds, [property: JsonRequired] uint Mode);
public sealed record NativeSoftwareDrawingEnvironmentState([property: JsonRequired] short? TopLeftX, [property: JsonRequired] short? TopLeftY, [property: JsonRequired] uint? RightX, [property: JsonRequired] uint? BottomY, [property: JsonRequired] short? OffsetX, [property: JsonRequired] short? OffsetY, [property: JsonRequired] int? OriginX, [property: JsonRequired] int? OriginY, [property: JsonRequired] uint? Debug, [property: JsonRequired] NativeSoftwareEnvironmentDerived? Derived)
{
    public static NativeSoftwareDrawingEnvironmentState FromColdProcess() => new(null, null, null, null, null, null, 0, 0, 0, null);
}
public sealed record NativeSoftwareTextureCacheInvalidation(int Left, int Top, int Right, int Bottom);
public sealed record NativeSoftwareEnvironmentUpdate(NativeSoftwareDrawingEnvironmentState State, NativeSoftwareTextureCacheInvalidation? Invalidation);

/// <summary>E3/E4/E5's software-consumed environment and retained origin lifecycle; normal viewport float outputs are separate.</summary>
public static class NativeSoftwareDrawingEnvironment
{
    public static NativeSoftwareDrawingEnvironmentState WriteE345BA30(NativeSoftwareDrawingEnvironmentState state, uint command)
    {
        return Recalculate45B6F0(state with { TopLeftX = (short)(command & 0x3FF), TopLeftY = (short)((command >> 10) & 0x1FF) });
    }

    public static NativeSoftwareEnvironmentUpdate WriteE445BA60(NativeSoftwareDrawingEnvironmentState state, uint command)
    {
        state = Recalculate45B6F0(state with { RightX = command & 0x3FF, BottomY = (command >> 10) & 0x1FF });
        NativeSoftwareEnvironmentDerived derived = state.Derived!;
        NativeSoftwareDrawingRectangle rectangle = derived.Rectangle;
        NativeSoftwareTextureCacheInvalidation? invalidate = (derived.Mode & 2) != 0 ? new(rectangle.X, rectangle.Y, unchecked(rectangle.X + rectangle.Width - 1), unchecked(rectangle.Y + rectangle.Height - 1)) : null;
        return new(state, invalidate);
    }

    public static NativeSoftwareDrawingEnvironmentState WriteE545BAD0(NativeSoftwareDrawingEnvironmentState state, uint command)
    {
        return Recalculate45B6F0(state with { OffsetX = Sign11(command), OffsetY = Sign11(command >> 11) });
    }

    public static NativeSoftwareDrawingEnvironmentState SetOrigin45B5E0(NativeSoftwareDrawingEnvironmentState state, int x, int y)
    {
        uint debug = state.Debug ?? throw new NotSupportedException("Native drawing origin requires its retained debug DWORD.");
        return state with { OriginX = x, OriginY = y, Debug = debug | 1 };
    }

    public static NativeSoftwareDrawingEnvironmentState EndBatch45D080(NativeSoftwareDrawingEnvironmentState state) => state with { OriginX = 0, OriginY = 0, Debug = 0 };

    public static NativeSoftwareDrawingEnvironmentState Recalculate45B6F0(NativeSoftwareDrawingEnvironmentState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.TopLeftX is not short left || state.TopLeftY is not short top || state.RightX is not uint right || state.BottomY is not uint bottom || state.OffsetX is not short offsetX || state.OffsetY is not short offsetY || state.OriginX is not int originX || state.OriginY is not int originY || state.Debug is not uint debug)
        {
            throw new NotSupportedException("Native software environment requires every actual retained rectangle, offset, origin and debug input.");
        }
        ushort width = unchecked((ushort)(right - unchecked((uint)left) + 1));
        ushort height = unchecked((ushort)(bottom - unchecked((uint)top) + 1));
        short signedWidth = unchecked((short)width);
        short signedHeight = unchecked((short)height);
        int drawX = unchecked(offsetX + originX);
        int drawY = unchecked(offsetY + originY);
        short rectangleX = debug != 0 ? unchecked((short)(left + originX)) : left;
        short rectangleY = debug != 0 ? unchecked((short)(top + originY)) : top;
        NativeSoftwareDrawingRectangle rectangle = new(rectangleX, rectangleY, signedWidth, signedHeight);
        NativeSoftwareClipBounds clip = new(unchecked(rectangleX << 16), rectangleY, unchecked((rectangleX + signedWidth) << 16), unchecked(rectangleY + signedHeight));
        uint mode = left < 640 && top < 240 ? 1u : signedWidth >= 320 && signedHeight >= 216 ? 3u : 2u;
        return state with { Derived = new(width, height, drawX, drawY, unchecked(drawX << 16), unchecked(drawY << 16), rectangle, clip, mode) };
    }

    private static short Sign11(uint value)
    {
        int bits = (int)(value & 0x7FF);
        return (short)((bits & 0x400) == 0 ? bits : bits - 0x800);
    }
}
