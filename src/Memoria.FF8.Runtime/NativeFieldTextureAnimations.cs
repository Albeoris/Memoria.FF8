using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldTextureCopyKey(short X, short Y, short Width, short Height);
public sealed record NativeFieldTextureAnimation(NativeFieldRegisteredTexture Texture, ImmutableArray<NativeFieldTextureCopyKey> Frames, int CurrentFrame);

/// <summary>First-seen copy rectangles retained for the lifetime of registered field textures.</summary>
public sealed record NativeFieldTextureAnimations(string OneHash, ImmutableArray<NativeFieldTextureAnimation> Textures)
{
    public static NativeFieldTextureAnimations Register(string oneHash, IEnumerable<NativeFieldRegisteredTexture> textures) => new(oneHash, textures.DistinctBy(texture => (texture.X, texture.Y)).Select(texture => new NativeFieldTextureAnimation(texture, [], -1)).ToImmutableArray());

    public void Validate()
    {
        if (OneHash is not { Length: 64 } || Textures.IsDefault || Textures.Any(texture => texture is null || texture.Texture is null || texture.Frames.IsDefault || texture.CurrentFrame < -1 || texture.CurrentFrame >= texture.Frames.Length || texture.Frames.Distinct().Count() != texture.Frames.Length || (texture.Frames.Length == 0) != (texture.CurrentFrame == -1) || texture.Frames.Any(frame => frame.X < 0 || frame.Y < 0 || frame.Width <= 0 || frame.Height <= 0 || frame.X + frame.Width > 1024 || frame.Y + frame.Height > 512)))
        {
            throw new InvalidDataException("Registered field texture animation has an invalid copy history.");
        }
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
