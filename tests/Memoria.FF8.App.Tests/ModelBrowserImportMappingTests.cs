using System.Buffers.Binary;
using System.Text;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserImportMappingTests
{
    [Theory]
    [InlineData("{\"nodes\":[{\"mesh\":0}],\"skins\":[{}]}")]
    [InlineData("{\"nodes\":[{\"mesh\":0}]}")]
    [InlineData("{\"nodes\":[{}],\"skins\":[{}]}")]
    public void SingleUnidentifiedObjectOrClipRigCanBeExplicitlyMapped(string json)
    {
        RecreateGame.ValidateSingleUnidentifiedModelMapping(Container(json));
    }

    [Theory]
    [InlineData("{\"nodes\":[{\"mesh\":0},{\"mesh\":1}],\"skins\":[{}]}")]
    [InlineData("{\"nodes\":[{\"mesh\":0},{\"mesh\":0}],\"skins\":[{}]}")]
    [InlineData("{\"nodes\":[{\"mesh\":0}],\"skins\":[{},{}]}")]
    [InlineData("{\"nodes\":[{}]}")]
    public void UnidentifiedAssemblyCannotBeRedirectedToOneCatalogTarget(string json)
    {
        Assert.Throws<NotSupportedException>(() => RecreateGame.ValidateSingleUnidentifiedModelMapping(Container(json)));
    }

    private static byte[] Container(string json)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] bytes = new byte[20 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546C67);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4E4F534A);
        payload.CopyTo(bytes.AsSpan(20));
        return bytes;
    }
}
