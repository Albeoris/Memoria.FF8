using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldTextureCopyKey(short X, short Y, short Width, short Height);
public sealed record NativeFieldTextureAnimation(NativeFieldRegisteredTexture Texture, ImmutableArray<NativeFieldTextureCopyKey> Frames, int CurrentFrame);

/// <summary>Whole-plane selection owned by the authenticated renderer's physical page halves.</summary>
public sealed record NativeFieldTexturePlanes(string ExecutableHash, ImmutableArray<bool> Closed)
{
    public static NativeFieldTexturePlanes FromReset(string executableHash)
    {
        if (executableHash != NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            throw new NotSupportedException("Field texture planes require a verified Remastered native profile.");
        }
        return new(executableHash, Enumerable.Repeat(false, 64).ToImmutableArray());
    }

    public void Validate()
    {
        if (ExecutableHash != NativeFieldRuntimeProfile.RemasteredEnImageHash || Closed.IsDefault || Closed.Length != 64)
        {
            throw new InvalidDataException("Native field texture plane state has an invalid profile or physical cache.");
        }
    }

    public NativeFieldTexturePlanes Write(short x, short y, bool closed)
    {
        Validate();
        return this with { Closed = Closed.SetItem(Slot(x, y), closed) };
    }

    public bool IsClosed(short x, short y)
    {
        Validate();
        return Closed[Slot(x, y)];
    }

    private static int Slot(short x, short y)
    {
        if (x is < 0 or >= 1024 || y is < 0 or >= 1024)
        {
            throw new InvalidDataException("Native field plane coordinates leave the physical renderer cache.");
        }
        int page = x >> 6;
        int normalizedY = y;
        if ((y >> 9) == 1)
        {
            page += 16;
            normalizedY -= 256;
        }
        return page * 2 + (normalizedY < 128 ? 0 : 1);
    }
}

/// <summary>First-seen copy rectangles retained for the lifetime of registered field textures.</summary>
public sealed record NativeFieldTextureAnimations(string OneHash, ImmutableArray<NativeFieldTextureAnimation> Textures)
{
    public NativeFieldTexturePlanes? Planes { get; init; }

    public static NativeFieldTextureAnimations Register(string oneHash, IEnumerable<NativeFieldRegisteredTexture> textures, string? executableHash = null) => new(oneHash, textures.DistinctBy(texture => (texture.X, texture.Y)).Select(texture => new NativeFieldTextureAnimation(texture, [], -1)).ToImmutableArray())
    {
        Planes = executableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash ? NativeFieldTexturePlanes.FromReset(executableHash) : null
    };

    public void Validate()
    {
        Planes?.Validate();
        if (OneHash is not { Length: 64 } || !OneHash.All(Uri.IsHexDigit) || Textures.IsDefault)
        {
            throw new InvalidDataException("Registered field texture animation has an invalid source identity.");
        }
        HashSet<(short X, short Y)> coordinates = [];
        foreach (NativeFieldTextureAnimation animation in Textures)
        {
            if (animation is null || animation.Texture is not { } texture || texture.Mode > 2 || texture.X < 0 || texture.Y < 0 || texture.WidthWords <= 0 || texture.Height <= 0 || texture.X + texture.WidthWords > 1024 || texture.Y + texture.Height > 512 || texture.PaletteX < 0 || texture.PaletteX >= 1024 || texture.PaletteY < 0 || texture.PaletteY >= 512 || texture.Mode < 2 && texture.PaletteX + (texture.Mode == 0 ? 16 : 256) > 1024 || !coordinates.Add((texture.X, texture.Y)))
            {
                throw new InvalidDataException("Registered field texture animation has an invalid or duplicate VRAM texture owner.");
            }
            if (animation.Frames.IsDefault || animation.CurrentFrame < -1 || animation.CurrentFrame >= animation.Frames.Length || animation.Frames.Distinct().Count() != animation.Frames.Length || (animation.Frames.Length == 0) != (animation.CurrentFrame == -1) || animation.Frames.Any(frame => frame is null || frame.X < 0 || frame.Y < 0 || frame.Width <= 0 || frame.Height <= 0 || frame.X + frame.Width > 1024 || frame.Y + frame.Height > 512))
            {
                throw new InvalidDataException("Registered field texture animation has an invalid copy history.");
            }
        }
    }

    public NativeFieldTextureAnimations WritePlane(short destinationX, short destinationY, bool closed)
    {
        return Planes is null ? this : this with { Planes = Planes.Write(destinationX, destinationY, closed) };
    }

    public NativeFieldTextureAnimations Copy(short sourceX, short sourceY, short width, short height, short destinationX, short destinationY)
    {
        Validate();
        NativeFieldTextureCopyKey key = new(sourceX, sourceY, width, height);
        int source = Find(sourceX, sourceY);
        if (source < 0)
        {
            return this;
        }
        int destination = Find(destinationX, destinationY);
        ImmutableArray<NativeFieldTextureAnimation> updated = Textures;
        foreach (int index in new[] { source, destination }.Where(index => index >= 0).Distinct())
        {
            NativeFieldTextureAnimation animation = updated[index];
            int frame = animation.Frames.IndexOf(key);
            if (frame < 0)
            {
                frame = animation.Frames.Length;
                animation = animation with { Frames = animation.Frames.Add(key) };
            }
            updated = updated.SetItem(index, animation with { CurrentFrame = frame });
        }
        return this with { Textures = updated };
    }

    private int Find(int x, int y)
    {
        for (int index = Textures.Length - 1; index >= 0; index--)
        {
            NativeFieldRegisteredTexture texture = Textures[index].Texture;
            if (x >= texture.X && x < texture.X + texture.WidthWords && y >= texture.Y && y < texture.Y + texture.Height)
            {
                return index;
            }
        }
        return -1;
    }
}
