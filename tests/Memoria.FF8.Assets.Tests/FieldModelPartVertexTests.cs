using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldModelPartVertexTests
{
    [Fact]
    public void EachPartMapsItsOwnConsecutiveScratchVerticesThroughOrderedSkinRanges()
    {
        FieldModel model = Decode(CreateOne());
        Assert.Equal(new[] { 6, 7, 0, 1 }, model.Faces[0].Vertices);
        Assert.Equal(new[] { 1, 6, 0 }, model.Faces[1].Vertices.Take(3));
        Assert.Equal(new[] { 10, 11, 4, 5 }, model.Faces[2].Vertices);
        Assert.Equal(new[] { 3, 10, 5 }, model.Faces[3].Vertices.Take(3));
        Assert.Equal(new Vector3(-106, 206, -306), model.Vertices[model.Faces[0].Vertices[0]].Position);
        Assert.Equal(0, model.Vertices[6].SkinBinding);
        Assert.Equal(1, model.Vertices[0].SkinBinding);
        Assert.Equal(2, model.Vertices[10].SkinBinding);
        Assert.Equal(3, model.Vertices[3].SkinBinding);
        Assert.Equal(new[] { true, false, true, false }, model.Faces.Select(face => face.Quad));
    }

    [Fact]
    public void SinglePartStillMapsReorderedDisjointSkinRanges()
    {
        byte[] one = CreateOne();
        const int model = 40 + 24;
        const int part = model + 64 + 64 + 12 * 8 + 4 * 64;
        BinaryPrimitives.WriteInt32LittleEndian(one.AsSpan(model + 16), 1);
        BinaryPrimitives.WriteInt16LittleEndian(one.AsSpan(part + 2), 4);
        BinaryPrimitives.WriteInt16LittleEndian(one.AsSpan(part + 18), 2);
        BinaryPrimitives.WriteInt16LittleEndian(one.AsSpan(part + 22), 2);
        FieldModel decoded = Decode(one);
        Assert.Equal(new[] { 6, 7, 0, 1 }, decoded.Faces[0].Vertices);
        Assert.Equal(new[] { 1, 6, 0 }, decoded.Faces[1].Vertices.Take(3));
        Assert.Equal(new[] { 6, 7, 1, 10 }, decoded.Faces[2].Vertices);
        Assert.Equal(new[] { 0, 6, 10 }, decoded.Faces[3].Vertices.Take(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FaceIndexMustFitItsPartEvenWhenItFitsTheCompleteSourceVertexArray(int face)
    {
        Assert.Throws<InvalidDataException>(() => Decode(CreateOne(face)));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(6, -1)]
    public void SignedSkinRangesCannotAddressBeforeTheirSourceOrHaveNegativeLength(short first, short count)
    {
        byte[] one = CreateOne();
        const int firstSkin = 40 + 24 + 64 + 64 + 12 * 8 + 4 * 64 + 2 * 32;
        BinaryPrimitives.WriteInt16LittleEndian(one.AsSpan(firstSkin), first);
        BinaryPrimitives.WriteInt16LittleEndian(one.AsSpan(firstSkin + 2), count);
        Assert.Throws<InvalidDataException>(() => Decode(one));
    }

    [Fact]
    public void InstalledCarryingModelSecondPartUsesItsOwnSourceVertices()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed field resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldModel model = Assert.Single(assets.LoadField(623).Models, model => model.Name.StartsWith("d075", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(model.NativeParts);
        Assert.Equal(2, model.NativeParts.Parts.Count);
        int firstTriangles = model.NativeParts.Parts[0].Words[9];
        int firstQuads = model.NativeParts.Parts[0].Words[11];
        ModelFace[] secondPart = model.Faces.Where(face => !face.Quad).Skip(firstTriangles).Concat(model.Faces.Where(face => face.Quad).Skip(firstQuads)).ToArray();
        Assert.NotEmpty(secondPart);
        Assert.All(secondPart, face => Assert.All(face.Vertices.Take(face.Quad ? 4 : 3), vertex => Assert.InRange(vertex, 286, 591)));
    }

    [Fact]
    public void InstalledTrainDoorLeavesUseTheirSeparateEightVertexRanges()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed field resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldModel model = assets.LoadField(919).Models[13];
        ModelFace[] quads = model.Faces.Where(face => face.Quad).ToArray();
        Assert.Equal(617, quads.Length);
        Assert.All(quads.Skip(613).Take(2), face => Assert.All(face.Vertices, vertex => Assert.InRange(vertex, 988, 995)));
        Assert.All(quads.Skip(615).Take(2), face => Assert.All(face.Vertices, vertex => Assert.InRange(vertex, 996, 1003)));
    }

    private static FieldModel Decode(byte[] one)
    {
        using SelectedModelResourceSource source = new(new FixtureSource(one), new FixtureSource(one));
        return source.ReadModel(new ResourceKey("field/fixture/chara.one"), one, _ => throw new InvalidOperationException("The embedded fixture needs no main-character resource."), 0);
    }

    private static byte[] CreateOne(int invalidFace = -1)
    {
        const int container = 40;
        const int modelOffset = 24;
        const int bones = 64;
        const int vertices = bones + 64;
        const int faces = vertices + 12 * 8;
        const int parts = faces + 4 * 64;
        const int skins = parts + 2 * 32;
        const int animation = skins + 4 * 8;
        using MemoryStream stream = new(new byte[container + modelOffset + animation + 16], true);
        using BinaryWriter writer = new(stream);
        writer.Write(1);
        writer.Write(container - 4);
        writer.Write((int)stream.Length - container);
        writer.Write(0);
        writer.Write(0u);
        writer.Write(uint.MaxValue);
        writer.Write(modelOffset);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("o999???\0"));
        writer.Write(0);
        stream.Position = container;
        writer.Write(0x10u);
        writer.Write(2u);
        writer.Write(14u);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write((ushort)0x7FFF);
        stream.Position = container + modelOffset;
        foreach (int value in new[] { 1, 12, 0, 4, 2, 4, 0, 0, bones, vertices, faces, faces, parts, skins, animation, -1 })
        {
            writer.Write(value);
        }
        for (int vertex = 0; vertex < 12; vertex++)
        {
            stream.Position = container + modelOffset + vertices + vertex * 8;
            writer.Write((short)(-100 - vertex));
            writer.Write((short)(200 + vertex));
            writer.Write((short)(-300 - vertex));
            writer.Write((short)0);
        }
        ushort[][] indices = [[0, 1, 2, 3], [3, 0, 2, ushort.MaxValue], [0, 1, 3, 4], [2, 0, 4, ushort.MaxValue]];
        for (int face = 0; face < 4; face++)
        {
            stream.Position = container + modelOffset + faces + face * 64;
            writer.Write(face % 2 == 0 ? 0x2D010709u : 0x25010607u);
            stream.Position += 8;
            for (int corner = 0; corner < 4; corner++)
            {
                writer.Write(face == invalidFace && corner == 0 ? (ushort)4 : indices[face][corner]);
            }
        }
        for (int part = 0; part < 2; part++)
        {
            stream.Position = container + modelOffset + parts + part * 32;
            short[] words = [(short)(part * 2), 2, 0, 0, 0, 0, 0, 0, -17, 1, 99, 1, 0, 0, 0, 0];
            foreach (short word in words)
            {
                writer.Write(word);
            }
        }
        stream.Position = container + modelOffset + skins;
        foreach ((short first, short count) in new (short, short)[] { (6, 2), (0, 2), (10, 2), (3, 3) })
        {
            writer.Write(first);
            writer.Write(count);
            writer.Write((short)1);
            writer.Write((short)0);
        }
        stream.Position = container + modelOffset + animation;
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        return stream.ToArray();
    }

    private sealed class FixtureSource(byte[] one) : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("fixture", GameDataEdition.Classic, []);
        public ResourceContent? Find(ResourceKey key) => new(key, "one", 1, "fixture", () => new MemoryStream(one, false));

        public void Dispose()
        {
        }
    }
}
