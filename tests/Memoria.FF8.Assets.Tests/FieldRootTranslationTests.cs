using System.Numerics;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldRootTranslationTests
{
    [Theory]
    [InlineData(32760, 30, -32746)]
    [InlineData(-32760, -30, 32746)]
    [InlineData(123, -30, 93)]
    [InlineData(32767, 0, 32767)]
    [InlineData(-32768, 0, -32768)]
    [InlineData(1.5f, 2.5f, 4)]
    public void RootPoseAndControlAnchorShareNativeSignedWordTranslation(float animationZ, float offset, float expectedZ)
    {
        FieldModel model = new("root-word", [new(-1, -10), new(0, -20)], [new(Vector3.Zero, 0), new(Vector3.Zero, 1)], [], [], [new([new(new Vector3(1, 2, animationZ), [Vector3.Zero, Vector3.Zero])])]) { ControlBones = new(1, -1, -1, 1) };
        Vector3[] pose = model.Pose(0, 0, rootTranslationZ: offset);
        Assert.Equal(expectedZ, pose[0].Z);
        Assert.Equal(1, pose[0].X);
        Assert.Equal(2, pose[0].Y);
        Assert.Equal(expectedZ, pose[1].Z, 3);
        Assert.Equal(pose[1], model.GetFirstControlBoneAnchor(0, 0, offset));
        Assert.Equal(expectedZ, model.GetFirstControlBoneHeight(0, 0, offset), 3);
    }
}
