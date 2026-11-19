using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Albeoris.Games.FF8.Imaging.MimMap;
using Albeoris.Games.FF8.Imaging.Workspace;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class MimMapRuntimeWorkspaceTests
{
    [Fact]
    public void EditorResizeAndMetadataStrippingPreservePhysicalPixelsNativeFlagsAndAnimationMembers()
    {
        WithWorkspace((path, source) =>
        {
            MimMapRuntimeWorkspace.Export(source, new(new(path, WorkspaceContainerFormat.OpenRaster)));
            RewriteEditorDocument(path, scale: 2, stripPayload: true);
            var tiles = MimMapRuntimeWorkspace.Import(new(new(path, WorkspaceContainerFormat.OpenRaster)), source, TestContext.Current.CancellationToken);
            Assert.Equal(2, tiles.Count);
            Assert.All(tiles, tile => Assert.Equal((32, 32), (tile.Image.Width, tile.Image.Height)));
            Assert.Equal(new Rgba32(255, 0, 0, 128), tiles.Single(tile => tile.Index == 0).Image[0, 0]);
            Assert.Equal(new Rgba32(0, 255, 0, 255), tiles.Single(tile => tile.Index == 1).Image[0, 0]);
            Assert.Equal((byte)0, source.Map.Tiles[0].State);
            Assert.Equal((byte)1, source.Map.Tiles[1].State);
        });
    }

    [Fact]
    public void ResolvedHdWorkspaceRetainsEditedPixelsWithoutNativeReconstruction()
    {
        WithWorkspace((path, source) =>
        {
            PixelImage hd = new(48, 48, Enumerable.Repeat(new Rgba32(13, 29, 43, 255), 48 * 48).ToArray());
            MimMapRuntimeWorkspace.Export(source, new(new(path, WorkspaceContainerFormat.OpenRaster)), _ => hd, TestContext.Current.CancellationToken);
            var tiles = MimMapRuntimeWorkspace.Import(new(new(path, WorkspaceContainerFormat.OpenRaster)), source, TestContext.Current.CancellationToken);
            Assert.All(tiles, tile => Assert.Equal((48, 48), (tile.Image.Width, tile.Image.Height)));
            Assert.Equal(new Rgba32(13, 29, 43, 128), tiles[0].Image[4, 4]);
            Assert.Equal(new Rgba32(13, 29, 43, 255), tiles[1].Image[4, 4]);
        });
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("duplicate")]
    [InlineData("move")]
    [InlineData("opacity")]
    [InlineData("scale")]
    [InlineData("unknown")]
    public void StructuralEditsCannotSilentlyRewriteNativeComposition(string change)
    {
        WithWorkspace((path, source) =>
        {
            MimMapRuntimeWorkspace.Export(source, new(new(path, WorkspaceContainerFormat.OpenRaster)));
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                XDocument stack = ReadXml(zip);
                XElement layer = stack.Descendants("layer").First(layer => ((string?)layer.Attribute("name"))?.StartsWith("Pupu ", StringComparison.Ordinal) == true);
                switch (change)
                {
                    case "delete":
                        layer.Remove();
                        break;
                    case "duplicate":
                        layer.AddAfterSelf(new XElement(layer));
                        break;
                    case "move":
                        layer.SetAttributeValue("x", 1);
                        break;
                    case "opacity":
                        layer.SetAttributeValue("opacity", "0.4");
                        break;
                    case "scale":
                        stack.Root!.SetAttributeValue("w", 17);
                        break;
                    case "unknown":
                        XElement extra = new(layer);
                        extra.SetAttributeValue("name", "New painted layer without native ownership");
                        layer.AddAfterSelf(extra);
                        break;
                }
                WriteXml(zip, stack);
            }
            Assert.ThrowsAny<Exception>(() => MimMapRuntimeWorkspace.Import(new(new(path, WorkspaceContainerFormat.OpenRaster)), source, TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public void TransparentRepaintSurvivesAnEditorTrimmingEmptyLayerImages()
    {
        WithWorkspace((path, source) =>
        {
            MimMapRuntimeWorkspace.Export(source, new(new(path, WorkspaceContainerFormat.OpenRaster)));
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                XDocument stack = ReadXml(zip);
                foreach (XElement layer in stack.Descendants("layer").Where(layer => ((string?)layer.Attribute("name"))?.StartsWith("Pupu ", StringComparison.Ordinal) == true))
                {
                    string name = (string)layer.Attribute("src")!;
                    zip.GetEntry(name)!.Delete();
                    using Stream output = zip.CreateEntry(name).Open();
                    output.Write(LocalizationArtworkExporter.EncodePng(new PixelImage(1, 1)));
                }
            }
            var tiles = MimMapRuntimeWorkspace.Import(new(new(path, WorkspaceContainerFormat.OpenRaster)), source, TestContext.Current.CancellationToken);
            Assert.Equal(2, tiles.Count);
            Assert.All(tiles, tile => Assert.All(tile.Image.Pixels, pixel => Assert.Equal((byte)0, pixel.A)));
        });
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("C:/escape.png")]
    [InlineData("DATA/collision.png")]
    public void UnsafeAndCaseCollidingArchiveMembersFailBeforeDecode(string unsafeName)
    {
        WithWorkspace((path, source) =>
        {
            MimMapRuntimeWorkspace.Export(source, new(new(path, WorkspaceContainerFormat.OpenRaster)));
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                zip.CreateEntry("data/collision.png");
                zip.CreateEntry(unsafeName);
            }
            Assert.ThrowsAny<Exception>(() => MimMapRuntimeWorkspace.Import(new(new(path, WorkspaceContainerFormat.OpenRaster)), source, TestContext.Current.CancellationToken));
        });
    }

    private static void RewriteEditorDocument(string path, int scale, bool stripPayload)
    {
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update);
        XDocument stack = ReadXml(zip);
        foreach (string dimension in new[] { "w", "h" })
        {
            stack.Root!.SetAttributeValue(dimension, (int)stack.Root.Attribute(dimension)! * scale);
        }
        foreach (XElement layer in stack.Descendants("layer"))
        {
            string name = (string)layer.Attribute("src")!;
            ZipArchiveEntry entry = zip.GetEntry(name)!;
            byte[] bytes;
            using (Stream input = entry.Open())
            using (MemoryStream copy = new())
            {
                input.CopyTo(copy);
                bytes = copy.ToArray();
            }
            PixelImage original = TextureImageDecoder.Decode(bytes, name).Image;
            PixelImage resized = new(original.Width * scale, original.Height * scale);
            for (int y = 0; y < resized.Height; y++)
            {
                for (int x = 0; x < resized.Width; x++)
                {
                    resized[x, y] = original[x / scale, y / scale];
                }
            }
            entry.Delete();
            using Stream output = zip.CreateEntry(name).Open();
            output.Write(LocalizationArtworkExporter.EncodePng(resized));
        }
        if (stripPayload)
        {
            foreach (ZipArchiveEntry entry in zip.Entries.Where(entry => entry.Name.EndsWith(".mim") || entry.Name.EndsWith(".map") || entry.Name.EndsWith(".json")).ToArray())
            {
                entry.Delete();
            }
        }
        WriteXml(zip, stack);
    }

    private static XDocument ReadXml(ZipArchive zip)
    {
        using Stream input = zip.GetEntry("stack.xml")!.Open();
        return XDocument.Load(input);
    }

    private static void WriteXml(ZipArchive zip, XDocument document)
    {
        zip.GetEntry("stack.xml")!.Delete();
        using Stream output = zip.CreateEntry("stack.xml").Open();
        using StreamWriter writer = new(output, new UTF8Encoding(false));
        document.Save(writer);
    }

    private static void WithWorkspace(Action<string, MimMapFile> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-field-exchange-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ushort[] palette = new ushort[4096];
            palette[1] = 0x801F;
            palette[2] = 0x03E0;
            byte[] pixels = new byte[MimFile.AtlasStride * 256];
            for (int y = 0; y < 16; y++)
            {
                pixels.AsSpan(y * MimFile.AtlasStride, 8).Fill(0x11);
                pixels.AsSpan(y * MimFile.AtlasStride + 8, 8).Fill(0x22);
            }
            MimMapFile source = new(new MimFile(new byte[MimFile.HeaderSize], palette, pixels), new MapFile([
                new MapTile { PackedTexture = 0x10, Parameter = 0, State = 0 },
                new MapTile { PackedTexture = 0x10, SourceX = 16, Parameter = 0, State = 1 }
            ]));
            test(Path.Combine(directory, "field.ora"), source);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
