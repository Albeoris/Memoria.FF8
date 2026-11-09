using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class InnerReaderBoundsTests
{
    [Fact]
    public void ScriptsPreserveAbsoluteEntriesAndBoundTextTermination()
    {
        byte[] bytes = new byte[111];
        U16(bytes, 0, 0x0502);
        bytes[2] = 1;
        U16(bytes, 4, 105);
        Encoding.ASCII.GetBytes("entity").CopyTo(bytes, 32);
        for (int i = 0; i < 32; i++)
        {
            U16(bytes, 40 + i * 2, 104);
        }
        U16(bytes, 105, 1);
        U16(bytes, 107, 4);
        bytes[109] = 33;
        bytes[110] = 255;
        PcFieldScripts scripts = PcFieldScripts.Read(bytes);
        Assert.Equal(104, Assert.Single(scripts.Entities).ScriptOffsets[31]);
        Assert.Equal(new byte[] { 33 }, Assert.Single(scripts.Texts).ToArray());
        bytes[110] = 0;
        Assert.Throws<InvalidDataException>(() => PcFieldScripts.Read(bytes));
        bytes[110] = 255;
        U16(bytes, 40, 103);
        Assert.Throws<InvalidDataException>(() => PcFieldScripts.Read(bytes));
    }

    [Fact]
    public void GeometryRejectsTruncationAndInvalidAdjacency()
    {
        Assert.Throws<InvalidDataException>(() => PcFieldCameras.Read(new byte[37]));
        Assert.Throws<InvalidDataException>(() => PcFieldModels.Read(new byte[6] { 0, 0, 255, 255, 0, 0 }));
        Assert.Throws<InvalidDataException>(() => PcFieldTriggers.Read(new byte[739]));
        byte[] walkmesh = new byte[34];
        U32(walkmesh, 0, 1);
        for (int i = 0; i < 3; i++)
        {
            U16(walkmesh, 28 + i * 2, ushort.MaxValue);
        }
        Assert.Single(PcFieldWalkmesh.Read(walkmesh));
        U16(walkmesh, 28, 1);
        Assert.Throws<InvalidDataException>(() => PcFieldWalkmesh.Read(walkmesh));
        U32(walkmesh, 0, uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => PcFieldWalkmesh.Read(walkmesh));
    }

    [Fact]
    public void BackgroundReadsDistinctFourthLayerAndEnabledEmptyLayer()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII, true);
        byte[] header = new byte[52];
        U16(header, 40, 320);
        U16(header, 42, 240);
        U16(header, 44, 1);
        writer.Write(header);
        Tile(1);
        Layer(20, 1);
        Tile(2);
        Layer(14, 0);
        Layer(14, 1);
        Tile(4);
        writer.Write("TEXTURE"u8);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        byte[] pixels = new byte[65536];
        pixels[0] = 7;
        writer.Write(pixels);
        writer.Write(new byte[82]);
        writer.Write("END"u8);
        PcFieldBackground background = PcFieldBackground.Read(stream.ToArray());
        Assert.Empty(background.Layers[2]);
        Assert.Equal(4, Assert.Single(background.Layers[3]).DestinationX);
        Assert.Equal(32, Assert.Single(background.Layers[3]).PixelSize);
        PcFieldTile effect = background.Layers[1][0] with { Blending = 1, TextureId = 4, TextureId2 = 0, SourceX = 16, SourceX2 = 32 };
        Assert.Equal(0, effect.EffectiveTextureId);
        Assert.Equal(32, effect.EffectiveSourceX);
        Assert.Equal(16, effect.PixelSize);
        Assert.Equal(4, (effect with { Blending = 0 }).EffectiveTextureId);
        Assert.Equal(7, Assert.Single(background.Textures).GetIndex(0, 0));
        Assert.Throws<InvalidDataException>(() => PcFieldBackground.Read(stream.ToArray().AsSpan(0, 60)));

        void Layer(int remainingHeaderBytes, ushort count)
        {
            writer.Write((byte)1);
            writer.Write(0);
            writer.Write(count);
            writer.Write(new byte[remainingHeaderBytes]);
        }
        void Tile(ushort destination)
        {
            byte[] tile = new byte[52];
            U16(tile, 0, destination);
            U16(tile, 16, 16);
            U16(tile, 18, 16);
            writer.Write(tile);
        }
    }

    [Fact]
    public void PaletteAndTextureRejectCountsBeforeAllocating()
    {
        byte[] palette = new byte[16];
        U32(palette, 0, 16);
        U16(palette, 8, 2);
        U16(palette, 10, 1);
        U16(palette, 12, 0x8000);
        Assert.Equal(0x8000, PcFieldPalettes.Read(palette).Palettes[0][0]);
        U16(palette, 10, ushort.MaxValue);
        Assert.Throws<InvalidDataException>(() => PcFieldPalettes.Read(palette));
        byte[] texture = new byte[246];
        U32(texture, 0, 1);
        U32(texture, 0x30, 1);
        U32(texture, 0x34, 2);
        U32(texture, 0x3C, 2);
        U32(texture, 0x40, 1);
        U32(texture, 0x58, 2);
        U32(texture, 0x68, 1);
        U32(texture, 0xEC, 0xFF112233);
        Assert.Equal(0xFF112233u, PcTexture.Read(texture).Palettes[0][0]);
        U32(texture, 0x30, int.MaxValue);
        U32(texture, 0x34, 0);
        Assert.Throws<InvalidDataException>(() => PcTexture.Read(texture));
    }

    [Fact]
    public void PolygonModelValidatesGroupLocalIndicesBeforeRendering()
    {
        byte[] model = new byte[360];
        U32(model, 12, 3);
        U32(model, 28, 3);
        U32(model, 36, 1);
        U32(model, 48, 1);
        U32(model, 52, 1);
        U16(model, 182, 0);
        U16(model, 184, 1);
        U16(model, 186, 2);
        U32(model, 312, 1);
        U32(model, 320, 3);
        PcPolygonModel decoded = PcPolygonModel.Read(model);
        Assert.Equal(3, decoded.Vertices.Count);
        Assert.Equal(2, Assert.Single(decoded.Polygons).C);
        U16(model, 186, 3);
        Assert.Throws<InvalidDataException>(() => PcPolygonModel.Read(model));
        U32(model, 12, uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => PcPolygonModel.Read(model));
    }

    [Fact]
    public void RsdPreservesNativeNamesAndRejectsDuplicateProperties()
    {
        RsdDescriptor descriptor = RsdDescriptor.Read("@RSD940102\nPLY=piece.PLY\nNTEX=1\nTEX[0]=skin.TIM\n"u8);
        Assert.Equal("piece.PLY", descriptor.Polygon);
        Assert.Equal("skin.TIM", Assert.Single(descriptor.Textures));
        Assert.Throws<InvalidDataException>(() => RsdDescriptor.Read("@RSD940102\nPLY=x\nNTEX=0\nNTEX=1"u8));
        Assert.Throws<InvalidDataException>(() => RsdDescriptor.Read("@RSD940102\nPLY=x\nNTEX=1"u8));
    }

    [Fact]
    public void KernelUsesDeclaredDecodedSizeAndBoundedGzip()
    {
        byte[] compressed = Gzip([1, 2, 3]);
        byte[] kernel = new byte[6 + compressed.Length + 2];
        U16(kernel, 0, (ushort)compressed.Length);
        U16(kernel, 2, 3);
        U16(kernel, 4, 7);
        compressed.CopyTo(kernel, 6);
        KernelSection section = Assert.Single(KernelArchive.Read(kernel));
        Assert.Equal(7, section.Type);
        Assert.Equal(new byte[] { 1, 2, 3 }, section.Data.ToArray());
        Assert.Throws<InvalidDataException>(() => KernelArchive.Read(kernel, maximumDecodedBytes: 2));
        U16(kernel, 2, 2);
        Assert.Throws<InvalidDataException>(() => KernelArchive.Read(kernel));
        U16(kernel, 2, 4);
        Assert.Throws<InvalidDataException>(() => KernelArchive.Read(kernel));
    }

    [Fact]
    public void ScenePreservesEnemyRewardsAndRejectsInvalidArchiveOffsets()
    {
        byte[] scene = new byte[0x1E80];
        U32(scene, 0x298 + 168, 16);
        U32(scene, 0x298 + 172, 10);
        U16(scene, 0x298 + 158, 2);
        byte[] compressed = Gzip(scene);
        byte[] archive = Enumerable.Repeat((byte)255, 8192).ToArray();
        U32(archive, 0, 16);
        compressed.CopyTo(archive, 64);
        SceneFormation formation = SceneArchive.Read(archive)[0];
        Assert.Equal(16u, formation.EnemyRewards[0].Experience);
        Assert.Equal(10u, formation.EnemyRewards[0].Gil);
        Assert.Equal(2, formation.EnemyRewards[0].AbilityPoints);
        Assert.Throws<InvalidDataException>(() => SceneArchive.Read(archive, maximumScenes: 0));
        U32(archive, 0, 15);
        Assert.Throws<InvalidDataException>(() => SceneArchive.Read(archive));
    }

    private static byte[] Gzip(byte[] data)
    {
        using MemoryStream stream = new();
        using (GZipStream gzip = new(stream, CompressionMode.Compress, true))
        {
            gzip.Write(data);
        }
        return stream.ToArray();
    }
    private static void U16(byte[] bytes, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    }
    private static void U32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
