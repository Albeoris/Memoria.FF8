namespace Memoria.FF8.Assets.Tests;

public sealed class NativeOrdinaryCarModelTests
{
    [Theory]
    [InlineData(32, 74)]
    [InlineData(33, 73)]
    [InlineData(34, 81)]
    [InlineData(35, 82)]
    [InlineData(36, 83)]
    [InlineData(37, 84)]
    [InlineData(38, 85)]
    [InlineData(39, 86)]
    [InlineData(40, 87)]
    public void EveryOrdinaryCarLoadsItsOwnOriginalModel(byte entity, byte shape)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldStaticModel model = assets.GetCarModel(entity);
        Assert.Equal(shape, model.ShapeId);
        Assert.NotEmpty(model.NativeVertices);
        Assert.NotEmpty(model.Faces);
        Assert.NotEmpty(model.Textures);
        Assert.True(assets.ReadNativeContactShape(shape).Length > 0);
    }
}
