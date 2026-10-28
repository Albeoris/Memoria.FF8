using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class TextureLayoutManifestTests
{
    [Fact]
    public void ExactImageDeclarationsPreserveTheirCanvasWithoutInferringResourceIdentity()
    {
        IReadOnlyDictionary<string, TextureLayoutMetadata> parsed = Parse("""{"version":1,"textures":{"textures/example.dds":{"layout":"whole-native-atlas","canvasWidth":256,"canvasHeight":128},"other/image.PNG":{"layout":"whole-native-atlas","canvasWidth":64,"canvasHeight":64}}}""");
        Assert.Equal(2, parsed.Count);
        Assert.Equal(new(TextureLayoutMode.WholeNativeAtlas, 256, 128), parsed["textures/example.dds"]);
        Assert.Equal(new(TextureLayoutMode.WholeNativeAtlas, 64, 64), parsed["OTHER/IMAGE.png"]);
        Assert.False(parsed is IDictionary<string, TextureLayoutMetadata> mutable && !mutable.IsReadOnly);
        Assert.Empty(Parse("""{"version":1,"textures":{}}"""));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"version":2,"textures":{}}""")]
    [InlineData("""{"version":"1","textures":{}}""")]
    [InlineData("""{"version":1.0,"textures":{}}""")]
    [InlineData("""{"version":1,"version":1,"textures":{}}""")]
    [InlineData("""{"version":1,"textures":{},"future":true}""")]
    [InlineData("""{"version":1,"textures":[]}""")]
    [InlineData("""{"version":1,"textures":{},}""")]
    [InlineData("""{/*comment*/"version":1,"textures":{}}""")]
    public void InvalidRootContractsAreRejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"layout":"component","canvasWidth":256,"canvasHeight":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":256,"canvasHeight":256,"x":0}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":256,"canvasWidth":128,"canvasHeight":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":0,"canvasHeight":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":256,"canvasHeight":-1}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":256.5,"canvasHeight":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":true,"canvasHeight":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":2147483648,"canvasHeight":256}""")]
    [InlineData("""{"layout":"whole-native-atlas","canvasWidth":2147483647,"canvasHeight":256}""")]
    public void InvalidOrUnknownDeclarationsAreRejected(string declaration)
    {
        Assert.Throws<InvalidDataException>(() => Parse("{\"version\":1,\"textures\":{\"image.dds\":" + declaration + "}}"));
    }

    [Theory]
    [InlineData("../image.dds")]
    [InlineData("textures/../image.dds")]
    [InlineData("textures/./image.dds")]
    [InlineData("textures//image.dds")]
    [InlineData("textures\\image.dds")]
    [InlineData("/image.dds")]
    [InlineData("C:/image.dds")]
    [InlineData("image.dds:stream")]
    [InlineData("textures/*.dds")]
    [InlineData("image.jpg")]
    [InlineData("")]
    public void PathsAreCanonicalExactImagesInsideTheirOwnPackage(string path)
    {
        string encoded = System.Text.Json.JsonSerializer.Serialize(path);
        Assert.Throws<InvalidDataException>(() => Parse("{\"version\":1,\"textures\":{" + encoded + ":{\"layout\":\"whole-native-atlas\",\"canvasWidth\":256,\"canvasHeight\":256}}}"));
    }

    [Theory]
    [InlineData("image.dds", "image.dds")]
    [InlineData("image.dds", "IMAGE.DDS")]
    public void DuplicateAddressesCannotDefineAmbiguousLayouts(string first, string second)
    {
        const string declaration = "{\"layout\":\"whole-native-atlas\",\"canvasWidth\":256,\"canvasHeight\":256}";
        Assert.Throws<InvalidDataException>(() => Parse("{\"version\":1,\"textures\":{\"" + first + "\":" + declaration + ",\"" + second + "\":" + declaration + "}}"));
    }

    [Fact]
    public void MetadataCannotExceedItsByteOrNestingBudget()
    {
        Assert.Throws<InvalidDataException>(() => TextureLayoutManifest.Parse(new byte[TextureLayoutManifest.MaximumBytes + 1]));
        Assert.Throws<InvalidDataException>(() => Parse(new string('[', 9) + new string(']', 9)));
    }

    private static IReadOnlyDictionary<string, TextureLayoutMetadata> Parse(string json) => TextureLayoutManifest.Parse(Encoding.UTF8.GetBytes(json));
}
