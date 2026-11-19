using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class NativeEulerRotationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void UnorderedEulerAxesAreOmittedIndependently(int unordered)
    {
        Vector3 angles = new((unordered & 1) != 0 ? float.NaN : 30, (unordered & 2) != 0 ? float.NaN : 60, (unordered & 4) != 0 ? float.NaN : 90);
        Matrix4x4 expected = Matrix4x4.CreateRotationZ((unordered & 4) != 0 ? 0 : MathF.PI / 2) * Matrix4x4.CreateRotationX((unordered & 1) != 0 ? 0 : MathF.PI / 6) * Matrix4x4.CreateRotationY((unordered & 2) != 0 ? 0 : MathF.PI / 3);
        Matrix4x4 actual = Ff7NativeFieldPoseMath.Rotate(angles);
        Assert.True(Vector3.Distance(Vector3.Transform(new(1, 2, 3), expected), Vector3.Transform(new(1, 2, 3), actual)) < 0.00001f);
    }

    [Fact]
    public void NativePoseConsumesUnorderedRootAndBoneAxesWithoutChangingTranslation()
    {
        byte[] source = Animation();
        WriteVector(source, 36, new(float.NaN, 0, 90));
        WriteVector(source, 48, new(10, 20, 30));
        WriteVector(source, 60, new(90, float.NaN, float.NaN));
        PcFieldAnimation clip = PcFieldAnimation.Read(source);
        Assert.Throws<InvalidDataException>(() => clip.Frames[0]);

        Ff7NativeFieldPose pose = Ff7NativeFieldPoseMath.Sample(Resources(clip), clip, 0);
        Assert.Equal(new(10, 20, 30), pose.RootTranslation);
        Vector3 point = Vector3.Transform(Vector3.UnitY, Assert.Single(pose.Bones));
        Assert.True(Vector3.Distance(new(10, -20, 31), point) < 0.00001f);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void TranslationDoesNotInheritUnorderedRotationSemantics(float value)
    {
        byte[] source = Animation();
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(48), value);
        PcFieldAnimation clip = PcFieldAnimation.Read(source);
        Assert.Throws<InvalidDataException>(() => Ff7NativeFieldPoseMath.Sample(Resources(clip), clip, 0));
    }

    [Theory]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void OrderedInfiniteAnglesRemainRejected(float value)
    {
        byte[] source = Animation();
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(60), value);
        PcFieldAnimation clip = PcFieldAnimation.Read(source);
        Assert.Throws<InvalidDataException>(() => Ff7NativeFieldPoseMath.Sample(Resources(clip), clip, 0));
        Assert.Throws<InvalidDataException>(() => Ff7NativeFieldPoseMath.Rotate(new(0, value, 0)));
    }

    private static Ff7FieldModelResources Resources(PcFieldAnimation clip)
    {
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON authored\n:BONES 1\nhip\nroot\n1\n0\n"));
        return new("authored.hrc", hierarchy, [], new Dictionary<string, PcFieldAnimation> { ["authored.a"] = clip });
    }

    private static byte[] Animation()
    {
        byte[] source = new byte[72];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(12), 0x020001);
        return source;
    }

    private static void WriteVector(byte[] source, int offset, Vector3 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(offset), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(offset + 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(offset + 8), value.Z);
    }
}
