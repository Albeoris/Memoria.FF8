using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tdw;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.Assets;

/// <summary>Uses actual installed font pixels and nibble widths. No proprietary data is embedded in the product.</summary>
public static class NativeInstalledFontProfile
{
    public static PixelImage DecodeAtlas(byte[] bytes, string path, int palette = 0)
    {
        if (Path.GetExtension(path).Equals(".tex", StringComparison.OrdinalIgnoreCase))
        {
            return TexFile.Read(bytes).Decode(palette);
        }
        if (Path.GetExtension(path).Equals(".tdw", StringComparison.OrdinalIgnoreCase))
        {
            TdwFile tdw = TdwFile.Read(bytes);
            if (tdw.Images.Count == 0)
            {
                throw new InvalidDataException("The TDW has metrics but no embedded atlas.");
            }
            return tdw.Images[0].Decode(palette);
        }
        if (Path.GetExtension(path).Equals(".tim", StringComparison.OrdinalIgnoreCase))
        {
            return Albeoris.Games.FF8.Imaging.Tim.TimFile.Read(bytes).Images[0].Decode(palette);
        }
        return TextureImageDecoder.Decode(bytes, path).Image;
    }

    /// <summary>European PC grid: 21 columns, 12×12 images; encoded printable byte minus 0x20 selects the image. Widths use the original glyph index.</summary>
    public static NativeFontProfile CreateEuropean(string id, string displayName, string language, NativeTextContext context, string relativeAtlasPath, PixelImage atlas, byte[] tdwBytes)
    {
        if (language.Equals("jp", StringComparison.OrdinalIgnoreCase) || language.Equals("ja", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Japanese requires explicit escaped-page and field-table image assignments; it is not the European 256-byte grid.");
        }
        byte[] widths = TdwFile.Read(tdwBytes).Widths;
        var encoding = NativeTextEncoding.Create(language);
        List<NativeGlyphImage> images = [];
        List<NativeGlyphMapping> mappings = [];
        List<NativeCodecMapping> codec = [];
        HashSet<string> assigned = new(StringComparer.Ordinal);
        for (int value = 0x20; value <= 0xFF; value++)
        {
            int glyph = value - 0x20;
            int x = glyph % 21 * 12;
            int y = glyph / 21 * 12;
            if (glyph >= widths.Length || x + 12 > atlas.Width || y + 12 > atlas.Height)
            {
                throw new InvalidDataException($"Installed font cannot resolve native image {glyph} and its TDW width.");
            }
            string text = encoding.GetString([(byte)value]);
            if (text.Length == 0 || text.Contains('\0'))
            {
                continue;
            }
            float advance = glyph switch { 0xAD => 9, 0xAE => 10, _ => widths[glyph] };
            images.Add(new(glyph, "native", x, y, 12, 12, advance));
            bool canonical = assigned.Add(text);
            codec.Add(new(value.ToString("X2"), text, canonical, text.StartsWith('{')));
            if (canonical)
            {
                mappings.Add(new(text, glyph));
            }
        }
        codec.Add(new("00", "{End}", Control: true));
        codec.Add(new("01", "{Next}", Control: true));
        codec.Add(new("02", "{Line}", Control: true));
        foreach (TextTagCode code in Enum.GetValues<TextTagCode>().Where(code => code is not (TextTagCode.End or TextTagCode.Next or TextTagCode.Line or TextTagCode.Glyph)))
        {
            int count = code == TextTagCode.Speaker ? 1 : 256;
            for (int parameter = 0; parameter < count; parameter++)
            {
                byte[] sequence = code == TextTagCode.Speaker ? [(byte)code] : [(byte)code, (byte)parameter];
                try
                {
                    string token = encoding.GetString(sequence);
                    if (token.StartsWith('{') && token.EndsWith('}') && token.Count(character => character == '{') == 1 && encoding.GetBytes(token).AsSpan().SequenceEqual(sequence))
                    {
                        codec.Add(new(Convert.ToHexString(sequence), token, assigned.Add(token), Control: true));
                    }
                }
                catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
                {
                    // The native codec rejects undefined dictionary selectors; they are not inferred as printable data.
                }
            }
        }
        return new NativeFontProfile
        {
            Id = id,
            DisplayName = displayName,
            LineHeight = 16,
            Domains = [new(context.Edition, context.ResourceFamily, context.Field, context.DictionaryLanguage)],
            Atlases = [new("native", relativeAtlasPath, atlas.Width, atlas.Height)],
            Images = images,
            Glyphs = mappings,
            Codec = codec,
            ReservedPrefixes = ["00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "0A", "0B", "0C", "0D", "0E", "0F", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "1A", "1B", "1C", "1D", "1E", "1F"]
        };
    }
}
