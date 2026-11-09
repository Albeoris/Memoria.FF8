using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class ModelReaderTests
{
    [Fact]
    public void HrcReadsInvariantHierarchyAndResourceIdentity()
    {
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON example\n:BONES 2\n\nhip\nroot\n1.5\n1 AAAA\n\nhead\nhip\n2\n0\n"));
        Assert.Equal("example", hierarchy.Name);
        Assert.Equal(2, hierarchy.Bones.Count);
        Assert.Equal(1.5f, hierarchy.Bones[0].Length);
        Assert.Equal("AAAA", Assert.Single(hierarchy.Bones[0].ResourceDescriptors));
        Assert.Equal("hip", hierarchy.Bones[1].ParentName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# original hierarchy comment\n")]
    public void HrcPreservesZeroBoneRootAttachmentAndIgnoresComments(string comment)
    {
        byte[] source = Encoding.ASCII.GetBytes($":HEADER_BLOCK 2\n:SKELETON root_attachment\n:BONES 0\n{comment}mesh\nroot\n0\n1 AAAA\n");
        HrcHierarchy hierarchy = HrcHierarchy.Read(source);
        Assert.Equal(0, hierarchy.DeclaredBoneCount);
        Assert.True(hierarchy.IsRootOnly);
        Assert.Equal("mesh", Assert.Single(hierarchy.Bones).Name);
    }

    [Theory]
    [InlineData("root", "1", "0")]
    [InlineData("missing", "1", "0")]
    [InlineData("root", "NaN", "0")]
    [InlineData("root", "-1", "0")]
    [InlineData("root", "1", "2 AAAA")]
    public void HrcRejectsInvalidHierarchyLengthsAndDescriptorCounts(string parent, string length, string descriptors)
    {
        string name = parent == "root" && length == "1" && descriptors == "0" ? "root" : "hip";
        byte[] source = Encoding.ASCII.GetBytes($":HEADER_BLOCK 2\n:SKELETON example\n:BONES 1\n{name}\n{parent}\n{length}\n{descriptors}");
        Assert.Throws<InvalidDataException>(() => HrcHierarchy.Read(source));
    }

    [Fact]
    public void AnimationReadsHeaderAndFrameCoordinatesWithoutRetargeting()
    {
        byte[] source = Animation();
        PcFieldAnimation animation = PcFieldAnimation.Read(source);
        Assert.Equal(0x020001u, animation.RotationOrder);
        Assert.Equal(1, animation.BoneCount);
        PcFieldAnimationFrame frame = Assert.Single(animation.Frames);
        Assert.Equal(new Vector3(1, 2, 3), frame.RootRotation);
        Assert.Equal(new Vector3(4, 5, 6), frame.RootTranslation);
        Assert.Equal(new Vector3(7, 8, 9), Assert.Single(frame.BoneRotations));
    }

    [Fact]
    public void AnimationRejectsZeroFramesButPreservesZeroBoneRootAnimation()
    {
        byte[] source = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 1);
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source));

        source = Animation()[..60];
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 0);
        PcFieldAnimation animation = PcFieldAnimation.Read(source);
        Assert.Equal(0, animation.BoneCount);
        PcFieldAnimationFrame frame = Assert.Single(animation.Frames);
        Assert.Empty(frame.BoneRotations);
        Assert.Equal(new Vector3(4, 5, 6), frame.RootTranslation);
    }

    [Fact]
    public void AnimationRejectsUnboundedDimensionsTruncationAndNonFiniteValues()
    {
        byte[] source = Animation();
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source, maximumFrames: 0));
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source.AsSpan(0, source.Length - 1)));
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source));
        source = Animation();
        BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(36), float.NaN);
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(source));
    }

    [Fact]
    public void OptionalInstalledCloudHierarchyAndAnimationDimensionsMatchOpeningDescriptors()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for read-only installed model checks.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        HrcHierarchy hierarchy = HrcHierarchy.Read(characters.ReadEntry(Assert.Single(characters.Find("AAAA.HRC"))));
        Assert.Equal(21, hierarchy.Bones.Count);
        (string name, int frames)[] expected = [("ACFE.a", 1), ("AAFF.a", 30), ("AAGA.a", 15), ("BVJF.a", 80)];
        foreach ((string name, int frames) in expected)
        {
            PcFieldAnimation animation = PcFieldAnimation.Read(characters.ReadEntry(Assert.Single(characters.Find(name))));
            Assert.Equal(frames, animation.Frames.Count);
            Assert.Equal(hierarchy.Bones.Count, animation.BoneCount);
            Assert.Equal(0x020001u, animation.RotationOrder);
            Console.WriteLine($"Cloud {name}: {animation.Frames.Count} frames, {animation.BoneCount} bones, rotation order {animation.RotationOrder:X6}.");
        }
    }

    private static byte[] Animation()
    {
        byte[] source = new byte[72];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(12), 0x020001);
        for (int i = 0; i < 9; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(source.AsSpan(36 + i * 4), i + 1);
        }
        return source;
    }
}
