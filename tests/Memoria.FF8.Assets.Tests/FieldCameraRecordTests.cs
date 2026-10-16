using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldCameraRecordTests
{
    [Fact]
    public void OriginalCastleStairwayRetainsCompleteCameraAndRejectsIncompleteSelection()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(574);
        Assert.Equal("felfst1", field.Name);
        Assert.Single(field.Cameras);
        Assert.Equal(new Vector3(-158, 969, 2466), field.Camera.Translation);
        Assert.Equal(Vector2.Zero, field.Camera.Pan);
        Assert.Equal(326, field.Camera.FocalLength);
        Assert.Equal(field.Camera, field.GetCamera(0));
        Assert.Throws<InvalidDataException>(() => field.GetCamera(1));
    }

    [Fact]
    public void OriginalClassroomStillSelectsSecondCameraAtNativeStride()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(232);
        Assert.Equal(2, field.Cameras.Count);
        Assert.Equal(new Vector3(-124, 419, 1216), field.GetCamera(0).Translation);
        Assert.Equal(new Vector3(-2237, 546, 284), field.GetCamera(1).Translation);
        Assert.Equal(439, field.GetCamera(1).FocalLength);
    }

    [Theory]
    [InlineData(38, 1)]
    [InlineData(40, 1)]
    [InlineData(56, 1)]
    [InlineData(76, 1)]
    [InlineData(77, 1)]
    [InlineData(78, 2)]
    [InlineData(80, 2)]
    public void CompletePayloadsUseFortyByteStrideWithoutInventingTrailingCamera(int length, int expectedCount)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(36), 326);
        if (length >= 78)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(76), 439);
        }
        IReadOnlyList<FieldCamera> cameras = FieldCamera.ReadAll(bytes);
        Assert.Equal(expectedCount, cameras.Count);
        Assert.Equal(326, cameras[0].FocalLength);
        if (expectedCount == 2)
        {
            Assert.Equal(439, cameras[1].FocalLength);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(37)]
    public void IncompleteFirstPayloadIsRejected(int length)
    {
        Assert.Throws<InvalidDataException>(() => FieldCamera.ReadAll(new byte[length]));
    }
}
