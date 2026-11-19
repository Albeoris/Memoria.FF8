using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class DeferredFieldAnimationTests
{
    [Fact]
    public void DurationAndValidPoseDoNotConsumeMalformedLaterPose()
    {
        byte[] source = Animation();
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(36 + 36 + 24), float.NaN);
        PcFieldAnimation clip = PcFieldAnimation.Read(source, sourceName: "authored.a");

        Assert.Equal(2, clip.Frames.Count);
        Assert.Equal(Vector3.One, clip.Frames[0].BoneRotations[0]);
        Assert.Same(clip.Frames[0], clip.Frames[0]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => clip.Frames[1]);
        Assert.Contains("authored.a", error.Message);
        Assert.Contains("frame 1", error.Message);
        Assert.Contains("bone 0 rotation", error.Message);
        Assert.True(float.IsNaN(clip.ReadNativeBoneRotation(1, 0, 1).X));
    }

    [Fact]
    public void DeferredPoseOwnsItsInputAndExposesImmutableChannels()
    {
        byte[] source = Animation();
        PcFieldAnimation clip = PcFieldAnimation.Read(source);
        source.AsSpan().Clear();

        Assert.Equal(Vector3.One, clip.Frames[1].RootTranslation);
        Assert.Equal(Vector3.One, clip.Frames[1].BoneRotations[0]);
        IList<Vector3> channels = Assert.IsAssignableFrom<IList<Vector3>>(clip.Frames[1].BoneRotations);
        Assert.Throws<NotSupportedException>(() => channels[0] = Vector3.Zero);
    }

    [Fact]
    public void DeferredPoseStillRejectsMalformedAllocationBeforeAnyFrameIsRead()
    {
        byte[] source = Animation();
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source.AsSpan(0, source.Length - 1)));
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 2);
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source));
    }

    private static byte[] Animation()
    {
        byte[] source = new byte[36 + 2 * 36];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(12), 0x020001);
        for (int offset = 36; offset < source.Length; offset += 4)
        {
            BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(offset), 1);
        }
        return source;
    }
}
