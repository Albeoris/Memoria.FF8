using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Albeoris.Games.FF8.Imaging.Map;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class OpenRasterStack
{
    public const String PaletteLayerName = "FF8 Palettes";
    public const String SourceMimLayerName = "FF8 Source MIM [do not edit]";
    public const String SourceMapLayerName = "FF8 Source MAP [do not edit]";
    private const String TextureLayerPrefix = "Pupu ";

    public static void Write(IWorkspaceWriter output, MimMapLayout layout)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(layout);
        XElement stack = new("stack");
        stack.Add(new XElement("layer", new XAttribute("name", PaletteLayerName), new XAttribute("src", MimMapWorkspacePaths.Palette), new XAttribute("visibility", "hidden")));
        stack.Add(new XElement("layer", new XAttribute("name", SourceMimLayerName), new XAttribute("src", MimMapWorkspacePaths.SourceMimLayer), new XAttribute("visibility", "hidden"), new XAttribute("edit-locked", "true")));
        stack.Add(new XElement("layer", new XAttribute("name", SourceMapLayerName), new XAttribute("src", MimMapWorkspacePaths.SourceMapLayer), new XAttribute("visibility", "hidden"), new XAttribute("edit-locked", "true")));
        foreach (MimMapTextureGroup group in layout.Groups.AsEnumerable().Reverse())
            stack.Add(CreateTextureLayer(group));
        XDocument document = new(new XDeclaration("1.0", "UTF-8", null), new XElement("image", new XAttribute("version", "0.0.6"), new XAttribute("w", layout.Width), new XAttribute("h", layout.Height), stack));
        output.Write(MimMapWorkspacePaths.Stack, stream => Save(document, stream));
    }

    public static OpenRasterLayerSet Read(IWorkspaceReader input)
    {
        ArgumentNullException.ThrowIfNull(input);
        XDocument document = Load(input.Read(MimMapWorkspacePaths.Stack));
        Dictionary<UInt32, OpenRasterLayerReference> textures = [];
        OpenRasterLayerReference? palette = null;
        OpenRasterLayerReference? sourceMim = null;
        OpenRasterLayerReference? sourceMap = null;
        foreach (XElement layer in document.Descendants("layer"))
        {
            String? name = (String?)layer.Attribute("name");
            String? source = (String?)layer.Attribute("src");
            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(source))
                continue;
            if (String.Equals(name, PaletteLayerName, StringComparison.Ordinal))
            {
                palette = CreateReference(layer, source);
                continue;
            }
            if (String.Equals(name, SourceMimLayerName, StringComparison.Ordinal))
            {
                sourceMim = CreateReference(layer, source);
                continue;
            }
            if (String.Equals(name, SourceMapLayerName, StringComparison.Ordinal))
            {
                sourceMap = CreateReference(layer, source);
                continue;
            }
            if (!name.StartsWith(TextureLayerPrefix, StringComparison.Ordinal) || name.Length < TextureLayerPrefix.Length + 8)
                continue;
            ReadOnlySpan<Char> id = name.AsSpan(TextureLayerPrefix.Length, 8);
            if (UInt32.TryParse(id, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out UInt32 pupuId))
                textures.Add(pupuId, CreateReference(layer, source));
        }
        return new OpenRasterLayerSet(palette, sourceMim, sourceMap, textures);
    }

    private static XElement CreateTextureLayer(MimMapTextureGroup group)
    {
        MimMapTile tile = group.Tiles[0];
        String animation = tile.AnimationId == Byte.MaxValue ? "Static" : $"Animation {tile.AnimationId}, State {tile.AnimationState}";
        return new XElement("layer", new XAttribute("name", $"{TextureLayerPrefix}{group.PupuId:X8} | Layer {tile.Layer} | {animation} | {tile.BlendMode}"), new XAttribute("src", MimMapWorkspacePaths.Texture(group.PupuId)), new XAttribute("visibility", IsVisible(tile) ? "visible" : "hidden"), new XAttribute("composite-op", GetCompositeOperation(tile.BlendMode)), new XAttribute("opacity", GetOpacity(tile.BlendMode).ToString("0.##", CultureInfo.InvariantCulture)));
    }

    private static Boolean IsVisible(MimMapTile tile) => tile.AnimationId == Byte.MaxValue || tile.AnimationState == 0;

    private static String GetCompositeOperation(MapBlendMode blendMode) => blendMode switch
    {
        MapBlendMode.Add or MapBlendMode.Average or MapBlendMode.QuarterAdd => "svg:plus",
        _ => "svg:src-over",
    };

    private static Double GetOpacity(MapBlendMode blendMode) => blendMode switch
    {
        MapBlendMode.Average => 0.5,
        MapBlendMode.QuarterAdd => 0.25,
        _ => 1.0,
    };

    private static OpenRasterLayerReference CreateReference(XElement layer, String source) => new(source, ReadCoordinate(layer, "x"), ReadCoordinate(layer, "y"));

    private static Int32 ReadCoordinate(XElement layer, String name)
    {
        String? value = (String?)layer.Attribute(name);
        if (value is null)
            return 0;
        return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 result) ? result : throw new InvalidDataException($"OpenRaster layer '{(String?)layer.Attribute("name")}' has invalid {name} coordinate '{value}'.");
    }

    private static void Save(XDocument document, Stream output)
    {
        XmlWriterSettings settings = new() { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), Indent = true, CloseOutput = false };
        using XmlWriter writer = XmlWriter.Create(output, settings);
        document.Save(writer);
    }

    private static XDocument Load(Byte[] content)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 4 * 1024 * 1024 };
        using MemoryStream stream = new(content, writable: false);
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }
}

internal sealed record OpenRasterLayerReference(String Path, Int32 X, Int32 Y);

internal sealed record OpenRasterLayerSet(OpenRasterLayerReference? Palette, OpenRasterLayerReference? SourceMim, OpenRasterLayerReference? SourceMap, IReadOnlyDictionary<UInt32, OpenRasterLayerReference> Textures);
