using Albeoris.Games.FF8.Imaging;
using System.Globalization;
using System.Text;

namespace Memoria.FF8.Resources;

/// <summary>Portable authored identity. Fingerprints and physical image dimensions never determine identity.</summary>
public sealed record TextureTarget(string Game, string Owner, string Subresource, int Palette = 0, string Variant = "")
{
    public const int SchemaVersion = 1;
    public TextureKey Key => new(new ResourceKey(Owner), Subresource, Palette);

    public static TextureTarget FromKey(TextureKey key, string variant = "", string game = "ff8") => new(game, key.Owner.Path, key.Subresource, key.Palette, variant);
    public static TextureTarget FromContext(TextureBuildContext context, string game = "ff8") => FromKey(context.Key, context.RequestedVariant ?? "", game);

    public void Validate()
    {
        if (Game is not ("ff8" or "ff7") || string.IsNullOrWhiteSpace(Owner) || new ResourceKey(Owner).Path != Owner || string.IsNullOrWhiteSpace(Subresource) || Subresource.Length > 512 || Subresource.Any(char.IsControl) || Palette < 0 || Variant is null || Variant.Length > 256 || Variant.Any(char.IsControl))
        {
            throw new InvalidDataException("Texture targets require a supported game, canonical logical owner, member, nonnegative palette and bounded variant.");
        }
    }

    public bool Matches(TextureBuildContext context) => Game == context.Game && Key == context.Key && Variant == (context.RequestedVariant ?? "");

    /// <summary>Each encoded segment starts with a letter, avoiding reserved desktop filenames and trailing dots.</summary>
    public string CanonicalPath(string extension = ".png")
    {
        Validate();
        if (extension is not (".png" or ".dds"))
        {
            throw new ArgumentException("Texture paths support PNG or DDS.", nameof(extension));
        }
        string owner = string.Join('/', Owner.Split('/').Select(Encode));
        return $"textures/{Game}/{owner}/members/{Encode(Subresource)}/palette-{Palette.ToString(CultureInfo.InvariantCulture)}/{Encode(Variant)}{extension}";
    }

    public static bool TryParsePath(string path, out TextureTarget? target)
    {
        target = null;
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || Path.IsPathRooted(path) || path.Length > 4096)
        {
            return false;
        }
        string[] parts = path.Split('/');
        if (parts.Length < 7 || parts[0] != "textures" || parts[^4] != "members" || !parts[^2].StartsWith("palette-", StringComparison.Ordinal) || !int.TryParse(parts[^2].AsSpan(8), NumberStyles.None, CultureInfo.InvariantCulture, out int palette))
        {
            return false;
        }
        string extension = Path.GetExtension(parts[^1]);
        if (extension is not (".png" or ".dds"))
        {
            return false;
        }
        try
        {
            string owner = string.Join('/', parts.Skip(2).Take(parts.Length - 6).Select(Decode));
            TextureTarget parsed = new(parts[1], owner, Decode(parts[^3]), palette, Decode(parts[^1][..^extension.Length]));
            parsed.Validate();
            if (parsed.CanonicalPath(extension) != path)
            {
                return false;
            }
            target = parsed;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or DecoderFallbackException or FormatException)
        {
            return false;
        }
    }

    private static string Encode(string text)
    {
        StringBuilder encoded = new("r");
        foreach (byte value in Encoding.UTF8.GetBytes(text))
        {
            if (value is >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' or (byte)'-')
            {
                encoded.Append((char)value);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return encoded.ToString();
    }

    private static string Decode(string text)
    {
        if (!text.StartsWith('r'))
        {
            throw new FormatException("Missing canonical texture segment prefix.");
        }
        using MemoryStream bytes = new();
        for (int index = 1; index < text.Length; index++)
        {
            char value = text[index];
            if (value == '%' && index + 2 < text.Length)
            {
                bytes.WriteByte(byte.Parse(text.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                index += 2;
            }
            else if (value is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
            {
                bytes.WriteByte((byte)value);
            }
            else
            {
                throw new FormatException("Invalid canonical texture segment.");
            }
        }
        return new UTF8Encoding(false, true).GetString(bytes.ToArray());
    }
}

public sealed record TextureTargetContract(string Layout, int LogicalWidth, int LogicalHeight)
{
    public static TextureTargetContract FromContext(TextureBuildContext context) => new(context.Layout, context.LogicalWidth, context.LogicalHeight);

    public void Validate(TextureBuildContext context, PixelImage image)
    {
        if (Layout != context.Layout || LogicalWidth != context.LogicalWidth || LogicalHeight != context.LogicalHeight || LogicalWidth <= 0 || LogicalHeight <= 0)
        {
            throw new InvalidDataException($"Texture contract differs from installed source {context.Key}. Re-export or select a compatible source.");
        }
        if (image.Width > PixelImage.MaximumDimension || image.Height > PixelImage.MaximumDimension || (long)image.Width * image.Height > PixelImage.MaximumPixelCount)
        {
            throw new InvalidDataException("Replacement exceeds supported image dimensions.");
        }
        // Native tile and upload rectangles require exact source-texel boundaries. Other complete
        // images may use arbitrary physical resolution while retaining their logical UV canvas.
        bool exactTexels = Layout.Contains("tile", StringComparison.Ordinal) || Layout.Contains("page", StringComparison.Ordinal) || Layout.Contains("vram", StringComparison.Ordinal) || Layout.Contains("atlas", StringComparison.Ordinal);
        if (exactTexels && (image.Width < LogicalWidth || image.Height < LogicalHeight || image.Width % LogicalWidth != 0 || image.Height % LogicalHeight != 0 || image.Width / LogicalWidth != image.Height / LogicalHeight))
        {
            throw new InvalidDataException($"{Layout} requires a uniform integer enlargement of {LogicalWidth} × {LogicalHeight}, with complete source texels.");
        }
    }
}

/// <summary>Capture references are CPU source data, never GPU handles or gameplay scene objects.</summary>
public sealed record TextureSourceSnapshot(TextureBuildContext Context, TextureValue Resolved, long Generation)
{
    public TextureTarget Target => TextureTarget.FromContext(Context, Context.Game);
    public PixelImage Original => Context.Original;
}
