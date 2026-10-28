using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeBattleTextureRole
{
    Pixels,
    Clut
}

public enum NativeBattleTextureDomain
{
    Magic,
    Battle
}

public enum NativeBattleTextureSourceKind
{
    MagMember,
    Tim,
    Image
}

/// <summary>A caller-proved file namespace for a complete standalone TIM source.</summary>
public sealed record NativeBattleTextureFileIdentity(string Name, NativeBattleTextureDomain Domain)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceOwner { get; init; }
    public int SourceOffset { get; init; }
}

public sealed record NativeBattleTextureFileContext(NativeBattleTextureFileIdentity Identity, int? NextTimMember);

/// <summary>The original resource member and its unscaled word canvas, independently of its current VRAM address.</summary>
public sealed record NativeBattleTextureOrigin(string Owner, int Member, NativeBattleTextureRole Role, int BitsPerPixel, int WidthWords, int Height)
{
    public NativeBattleTextureDomain Domain { get; init; } = NativeBattleTextureDomain.Magic;
    public NativeBattleTextureSourceKind Kind { get; init; } = NativeBattleTextureSourceKind.MagMember;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectTextureRectangle? PaletteRectangle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceOwner { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SourceOffset { get; init; }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Owner) || Member < 0 || SourceOffset < 0 || !Enum.IsDefined(Role) || !Enum.IsDefined(Domain) || !Enum.IsDefined(Kind) || BitsPerPixel is not (4 or 8 or 16) || WidthWords <= 0 || WidthWords > 1024 || Height <= 0 || Height > 512)
        {
            throw new InvalidDataException("Native texture provenance requires its resource member, role, depth and bounded original word canvas.");
        }
        if (PaletteRectangle is { } palette && (palette.X < 0 || palette.Y < 0 || palette.Width <= 0 || palette.Height <= 0 || palette.X + palette.Width > 1024 || palette.Y + palette.Height > 512))
        {
            throw new InvalidDataException("Native texture palette provenance leaves its original bounded word canvas.");
        }
    }
}

/// <summary>A surviving portion of one source canvas in current word VRAM.</summary>
public sealed record NativeBattleTextureSourceRegion(NativeBattleTextureOrigin Origin, int X, int Y, int WidthWords, int Height, int SourceXWords = 0, int SourceY = 0)
{
    public void Validate()
    {
        Origin.Validate();
        if (X < 0 || Y < 0 || WidthWords <= 0 || Height <= 0 || X + WidthWords > 1024 || Y + Height > 512 || SourceXWords < 0 || SourceY < 0 || SourceXWords + WidthWords > Origin.WidthWords || SourceY + Height > Origin.Height)
        {
            throw new InvalidDataException("Native texture source region leaves its VRAM or original source canvas.");
        }
    }
}

internal static class NativeBattleTextureProvenance
{
    internal static ImmutableArray<NativeBattleTextureSourceRegion>? Overwrite(ImmutableArray<NativeBattleTextureSourceRegion>? sources, int x, int y, int width, int height, NativeBattleTextureOrigin? origin = null)
    {
        if (width == 0 || height == 0)
        {
            return sources;
        }
        if (sources is null && origin is null)
        {
            return null;
        }
        var result = ImmutableArray.CreateBuilder<NativeBattleTextureSourceRegion>();
        foreach (NativeBattleTextureSourceRegion source in sources ?? [])
        {
            int left = Math.Max(x, source.X);
            int top = Math.Max(y, source.Y);
            int right = Math.Min(x + width, source.X + source.WidthWords);
            int bottom = Math.Min(y + height, source.Y + source.Height);
            if (left >= right || top >= bottom)
            {
                result.Add(source);
                continue;
            }
            Add(source.X, source.Y, source.WidthWords, top - source.Y);
            Add(source.X, bottom, source.WidthWords, source.Y + source.Height - bottom);
            Add(source.X, top, left - source.X, bottom - top);
            Add(right, top, source.X + source.WidthWords - right, bottom - top);

            void Add(int a, int b, int w, int h)
            {
                if (w > 0 && h > 0)
                {
                    result.Add(source with { X = a, Y = b, WidthWords = w, Height = h, SourceXWords = source.SourceXWords + a - source.X, SourceY = source.SourceY + b - source.Y });
                }
            }
        }
        if (origin is not null)
        {
            origin.Validate();
            if (origin.WidthWords != width || origin.Height != height)
            {
                throw new InvalidDataException("Native texture upload provenance differs from its submitted word canvas.");
            }
            if (NativeBattleSharedEffectTexture.IsCommonImage(origin))
            {
                for (int page = 0; page < 3; page++)
                {
                    NativeBattleTextureOrigin part = origin with { Owner = $"a8def{page}.tim", Kind = NativeBattleTextureSourceKind.Image, WidthWords = 64 };
                    result.Add(new(part, x + page * 64, y, 64, height));
                }
            }
            else
            {
                result.Add(new(origin, x, y, width, height));
            }
        }
        return result.ToImmutable();
    }

    internal static ImmutableArray<NativeBattleTextureSourceRegion>? Copy(ImmutableArray<NativeBattleTextureSourceRegion>? sources, NativeBattleEffectVramCopy copy)
    {
        if (sources is null || sources.Value.IsEmpty || copy.Width == 0 || copy.Height == 0)
        {
            return sources;
        }
        if (copy.SourceX == copy.DestinationX && copy.SourceY == copy.DestinationY)
        {
            // The transfer and frame observation still execute; source coordinates do not change.
            return sources;
        }
        if (copy.SourceX + copy.Width <= copy.DestinationX || copy.DestinationX + copy.Width <= copy.SourceX || copy.SourceY + copy.Height <= copy.DestinationY || copy.DestinationY + copy.Height <= copy.SourceY)
        {
            var copied = (Overwrite(sources, copy.DestinationX, copy.DestinationY, copy.Width, copy.Height) ?? []).ToBuilder();
            foreach (NativeBattleTextureSourceRegion source in sources.Value)
            {
                int left = Math.Max(copy.SourceX, source.X);
                int top = Math.Max(copy.SourceY, source.Y);
                int right = Math.Min(copy.SourceX + copy.Width, source.X + source.WidthWords);
                int bottom = Math.Min(copy.SourceY + copy.Height, source.Y + source.Height);
                if (left < right && top < bottom)
                {
                    copied.Add(source with { X = copy.DestinationX + left - copy.SourceX, Y = copy.DestinationY + top - copy.SourceY, WidthWords = right - left, Height = bottom - top, SourceXWords = source.SourceXWords + left - source.X, SourceY = source.SourceY + top - source.Y });
                }
            }
            return copied.ToImmutable();
        }
        // Mirror forward MOVSD/MOVSB semantics, including overlapping source and destination rows.
        Dictionary<int, (NativeBattleTextureOrigin Origin, int X, int Y)?> changed = [];
        for (int row = 0; row < copy.Height; row++)
        {
            for (int column = 0; column < copy.Width; column += 2)
            {
                var first = Read(copy.SourceX + column, copy.SourceY + row);
                var second = column + 1 < copy.Width ? Read(copy.SourceX + column + 1, copy.SourceY + row) : null;
                changed[(copy.DestinationY + row) * 1024 + copy.DestinationX + column] = first;
                if (column + 1 < copy.Width)
                {
                    changed[(copy.DestinationY + row) * 1024 + copy.DestinationX + column + 1] = second;
                }
            }
        }
        var result = (Overwrite(sources, copy.DestinationX, copy.DestinationY, copy.Width, copy.Height) ?? []).ToBuilder();
        for (int row = 0; row < copy.Height; row++)
        {
            for (int column = 0; column < copy.Width;)
            {
                int key = (copy.DestinationY + row) * 1024 + copy.DestinationX + column;
                var first = changed[key];
                int end = column + 1;
                if (first is { } known)
                {
                    while (end < copy.Width && changed[key + end - column] is { } next && next.Origin == known.Origin && next.X == known.X + end - column && next.Y == known.Y)
                    {
                        end++;
                    }
                    result.Add(new(known.Origin, copy.DestinationX + column, copy.DestinationY + row, end - column, 1, known.X, known.Y));
                }
                column = end;
            }
        }
        return result.ToImmutable();

        (NativeBattleTextureOrigin Origin, int X, int Y)? Read(int x, int y)
        {
            if (changed.TryGetValue(y * 1024 + x, out var value))
            {
                return value;
            }
            foreach (NativeBattleTextureSourceRegion source in sources.Value)
            {
                if (x >= source.X && y >= source.Y && x < source.X + source.WidthWords && y < source.Y + source.Height)
                {
                    return (source.Origin, source.SourceXWords + x - source.X, source.SourceY + y - source.Y);
                }
            }
            return null;
        }
    }
}
