using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TextureTransformRevisionTests
{
    [Fact]
    public void RevisionRefreshPublishesWholeGraphAndInvalidatesPreparedTransactions()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        ResourceService service = new(source);
        TextureKey original = new(new("test/source"), "original");
        TextureKey derived = new(original.Owner, "derived");
        TextureKey consumer = new(original.Owner, "consumer");
        service.GetTexture(new(original, source.Descriptor, 0, "test", 1, 1, () => new(1, 1)));
        TextureValue first = new(new(2, 2), 1, 1, "first");
        TextureValue second = new(new(3, 3), 1, 1, "second");
        AssetHandle<TextureValue> handle = service.GetTransformedTexture(new(derived, [original], _ => first));
        AssetHandle<TextureValue> downstream = service.GetTransformedTexture(new(consumer, [derived], values => values[0]));
        var prepared = service.Prepare(null);
        Assert.Same(handle, service.GetTransformedTexture(new(derived, [original], _ => second) { Revision = 1 }));
        Assert.Same(second, handle.Value);
        Assert.Same(second, downstream.Value);
        Assert.Equal(1, service.Generation);
        Assert.Throws<InvalidOperationException>(() => prepared.Commit());
        Assert.Same(handle, service.GetTransformedTexture(new(derived, [original], _ => throw new Exception("must remain cached")) { Revision = 1 }));
        service.Prepare(null).Commit();
        Assert.Same(second, downstream.Value);
    }

    [Fact]
    public void FailedRefreshPreservesBuilderPublicationAndOwnership()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        ResourceService service = new(source);
        TextureKey original = new(new("test/source"), "original");
        TextureKey derived = new(original.Owner, "derived");
        TextureKey consumer = new(original.Owner, "consumer");
        service.GetTexture(new(original, source.Descriptor, 0, "test", 1, 1, () => new(1, 1)));
        TextureValue valid = new(new(2, 2), 1, 1, "valid");
        AssetHandle<TextureValue> handle = service.GetTransformedTexture(new(derived, [original], _ => valid));
        AssetHandle<TextureValue> downstream = service.GetTransformedTexture(new(consumer, [derived], values => values[0]));
        Assert.Throws<InvalidDataException>(() => service.GetTransformedTexture(new(derived, [original], _ => throw new InvalidDataException("broken")) { Revision = 1 }, retain: true));
        Assert.Equal(0, service.Generation);
        Assert.Same(valid, handle.Value);
        Assert.Same(valid, downstream.Value);
        service.Prepare(null).Commit();
        Assert.Same(valid, downstream.Value);
        Assert.Throws<InvalidOperationException>(() => service.GetTransformedTexture(new(derived, [], _ => valid) { Revision = 1 }));
        service.Release([consumer, derived, original]);
        Assert.Equal(0, service.ResidentTextureCount);
    }

    [Fact]
    public void DownstreamFailureRejectsTheNewBuilderWithEveryDependentValue()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        ResourceService service = new(source);
        TextureKey original = new(new("test/source"), "original");
        TextureKey derived = new(original.Owner, "derived");
        TextureKey consumer = new(original.Owner, "consumer");
        service.GetTexture(new(original, source.Descriptor, 0, "test", 1, 1, () => new(1, 1)));
        TextureValue valid = new(new(2, 2), 1, 1, "valid");
        TextureValue invalid = new(new(3, 3), 1, 1, "invalid");
        AssetHandle<TextureValue> handle = service.GetTransformedTexture(new(derived, [original], _ => valid));
        AssetHandle<TextureValue> downstream = service.GetTransformedTexture(new(consumer, [derived], values => values[0] == invalid ? throw new InvalidDataException("downstream rejected input") : values[0]));
        Assert.Throws<InvalidDataException>(() => service.GetTransformedTexture(new(derived, [original], _ => invalid) { Revision = 1 }));
        Assert.Equal(0, service.Generation);
        Assert.Same(valid, handle.Value);
        Assert.Same(valid, downstream.Value);
        service.Prepare(null).Commit();
        Assert.Same(valid, downstream.Value);
    }
}
