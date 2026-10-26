using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldTextureCopyKey(short X, short Y, short Width, short Height);
public sealed record NativeFieldTextureAnimation(NativeFieldRegisteredTexture Texture, ImmutableArray<NativeFieldTextureCopyKey> Frames, int CurrentFrame);

public sealed record NativeFieldTexturePlane(bool Closed, byte Pending, bool Retain);
public sealed record NativeFieldTexturePageIdentity(uint First, uint Other);

/// <summary>Whole-plane selection owned by the authenticated renderer's physical page halves.</summary>
public sealed record NativeFieldTexturePlanes(string ExecutableHash, ImmutableArray<NativeFieldTexturePlane> Slots, ImmutableArray<NativeFieldTexturePageIdentity?> Sources)
{
    public static uint SourceIdentity(ReadOnlySpan<byte> name, int upload)
    {
        if (name.Length < 4 || name[1] is < (byte)'0' or > (byte)'9' || name[2] is < (byte)'0' or > (byte)'9' || name[3] is < (byte)'0' or > (byte)'9' || upload is < 1 or > 255)
        {
            throw new InvalidDataException("Native field material identity requires its original label and upload ordinal.");
        }
        int basis = name[0] switch { (byte)'d' => 97, (byte)'n' => 1097, (byte)'o' => 2097, (byte)'p' => 3097, _ => 0 };
        int identity = basis == 0 ? 0 : basis + (name[1] - '0') * 100 + (name[2] - '0') * 10 + name[3] - '0';
        return (uint)(identity << 8 | upload);
    }

    public static NativeFieldTexturePlanes FromReset(string executableHash)
    {
        if (executableHash != NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            throw new NotSupportedException("Field texture planes require a verified Remastered native profile.");
        }
        return new(executableHash, Enumerable.Repeat(new NativeFieldTexturePlane(false, 0, false), 64).ToImmutableArray(), Enumerable.Repeat<NativeFieldTexturePageIdentity?>(null, 32).ToImmutableArray());
    }

    public void Validate()
    {
        if (ExecutableHash != NativeFieldRuntimeProfile.RemasteredEnImageHash || Slots.IsDefault || Slots.Length != 64 || Sources.IsDefault || Sources.Length != 32 || Slots.Any(slot => slot is null || slot.Pending > 2 || slot.Pending == 0 && slot.Retain))
        {
            throw new InvalidDataException("Native field texture plane state has an invalid profile or physical cache.");
        }
    }

    public NativeFieldTexturePlanes Write(short x, short y, bool closed, bool retain = false)
    {
        Validate();
        int index = Slot(x, y);
        return this with { Slots = Slots.SetItem(index, Slots[index] with { Pending = closed ? (byte)2 : (byte)1, Retain = retain }) };
    }

    public NativeFieldTexturePlanes Bind(int page, NativeFieldTexturePageIdentity source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Validate();
        int first = CacheSlot(page, true);
        bool replaced = Sources[page] is { } previous && previous != source;
        ImmutableArray<NativeFieldTexturePlane> slots = Slots;
        for (int index = first; index <= first + 1; index++)
        {
            NativeFieldTexturePlane current = slots[index];
            if (replaced)
            {
                current = current with { Closed = false, Pending = current.Retain ? current.Pending : (byte)0 };
            }
            if (current.Pending != 0)
            {
                current = current with { Closed = current.Pending != 1, Pending = 0, Retain = false };
            }
            slots = slots.SetItem(index, current);
        }
        return Sources[page] == source && slots.SequenceEqual(Slots) ? this : this with { Slots = slots, Sources = Sources.SetItem(page, source) };
    }

    public NativeFieldTexturePlane Get(short x, short y)
    {
        Validate();
        return Slots[Slot(x, y)];
    }

    public bool IsClosed(short x, short y) => Get(x, y).Closed;

    public bool IsClosed(int page, bool firstHalf)
    {
        Validate();
        return Slots[CacheSlot(page, firstHalf)].Closed;
    }

    private static int CacheSlot(int page, bool firstHalf)
    {
        if (page is < 0 or >= 32)
        {
            throw new InvalidDataException("Native field plane page leaves the physical renderer cache.");
        }
        return page * 2 + (firstHalf ? 0 : 1);
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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
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

    public NativeFieldTextureAnimations WritePlane(short destinationX, short destinationY, bool closed, bool retain = false)
    {
        return Planes is null ? this : this with { Planes = Planes.Write(destinationX, destinationY, closed, retain) };
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
