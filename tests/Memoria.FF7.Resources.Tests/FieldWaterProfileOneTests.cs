using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class FieldWaterProfileOneTests
{
    [Theory]
    [InlineData(34, 1.4299999475479126f)]
    [InlineData(35, 1.4299999475479126f)]
    [InlineData(36, 1.4359999895095825f)]
    [InlineData(70, 1.6399999856948853f)]
    public void FirstOriginalVertexOwnsTheScalarAcrossAllParts(float height, float scalar)
    {
        Ff7FieldWaterGeometry captured = Capture(new(0, 0, height), Vector3.Zero, -40, secondPart: true);
        Assert.Equal(scalar, captured.ColorRegion!.ProfileOne!.FirstScalar);
        Assert.Equal(2, captured.Parts.Count);
        Assert.Equal(captured.ColorRegion.Contains(new(0, 0, height)), captured.Parts[1].ModifiedVertices[0]);
    }

    [Theory]
    [InlineData(1, 51, -74.08200073242188)]
    [InlineData(1, 52, -57.28199768066406)]
    [InlineData(-1, 51, 74.0780029296875)]
    [InlineData(-40, 70, 69.43999767303467)]
    public void CurrentHeightSecondBranchAndSignedLevelRetainNativeFloatStores(short level, float height, double plane)
    {
        Ff7FieldWaterProfileOneRegion profile = new(level, 1.43f);
        Assert.Equal(plane, profile.PlaneHeight(height));
    }

    [Fact]
    public void CapturedPredicateCannotBeReplacedByOneConstantPlaneAndColdRestoresExactly()
    {
        Ff7FieldWaterGeometry captured = Capture(Vector3.Zero, Vector3.Zero, -40);
        Ff7FieldWaterColorRegion region = captured.ColorRegion!;
        Assert.False(region.Contains(new(0, 0, 51)));
        Assert.True(region.Contains(new(0, 0, 70)));
        Assert.True(70 < region.Height);
        string saved = JsonSerializer.Serialize(captured);
        Ff7FieldWaterGeometry restored = JsonSerializer.Deserialize<Ff7FieldWaterGeometry>(saved)!;
        restored.ColorRegion!.Validate();
        Assert.Equal(saved, JsonSerializer.Serialize(restored));
        Assert.True(restored.ColorRegion.Contains(new(0, 0, 70)));
        Ff7FieldWaterColorRegion older = JsonSerializer.Deserialize<Ff7FieldWaterColorRegion>("{\"HasThreshold\":true,\"Height\":10,\"UseY\":false}")!;
        older.Validate();
        Assert.Null(older.ProfileOne);
        Assert.True(older.Contains(new(0, 0, 11)));
    }

    [Fact]
    public void ColorAxisAndFirstColorVertexDoNotReplaceTheIndependentFirstJointSplashScalar()
    {
        Ff7FieldWaterGeometry original = Capture(Vector3.Zero, Vector3.Zero, 1);
        Ff7FieldWaterGeometry differentFirstVertex = Capture(Vector3.Zero, new(0, 0, 70), 1);
        Assert.NotEqual(original.ColorRegion!.ProfileOne!.FirstScalar, differentFirstVertex.ColorRegion!.ProfileOne!.FirstScalar);
        Assert.NotEmpty(original.Splashes);
        Assert.Equal(original.Splashes, differentFirstVertex.Splashes);
        Ff7FieldWaterGeometry z = Capture(new(0, 70, 0), Vector3.Zero, 1);
        Ff7FieldWaterGeometry y = Capture(new(0, 70, 0), Vector3.Zero, 1, flags: 4);
        Assert.False(z.ColorRegion!.UseY);
        Assert.True(y.ColorRegion!.UseY);
        Assert.NotEqual(z.ColorRegion.ProfileOne!.FirstScalar, y.ColorRegion.ProfileOne!.FirstScalar);
        Assert.Equal(z.Splashes, y.Splashes);
        Assert.All(y.Splashes, splash => Assert.Equal(12, splash.HalfSize));
    }

    [Fact]
    public void ZeroLevelKeepsUnorderedInternalComparisonsOutOfFiniteSavedMetadata()
    {
        Ff7FieldWaterGeometry captured = Capture(Vector3.Zero, Vector3.Zero, 0);
        Assert.All(captured.Parts, part => Assert.All(part.ModifiedVertices, modified => Assert.False(modified)));
        Assert.Empty(captured.Splashes);
        Assert.False(captured.ColorRegion!.HasThreshold);
        Assert.False(captured.ColorRegion.Contains(new(0, 0, 100)));
        captured.ColorRegion.Validate();
        Assert.DoesNotContain("NaN", JsonSerializer.Serialize(captured));
    }

    private static Ff7FieldWaterGeometry Capture(Vector3 position, Vector3 firstVertex, short level, byte flags = 0, bool secondPart = false)
    {
        string descriptors = secondPart ? "2 body other" : "1 body";
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes($":HEADER_BLOCK 2\n:SKELETON profile\n:BONES 1\nbody\nroot\n3000\n{descriptors}\n"));
        PcPolygonModel polygon = new([firstVertex, new(0, 0, 200), new(0, 0, -300)], [], [], [uint.MaxValue, uint.MaxValue, uint.MaxValue], [], [], [], [new(0, 0, 0, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty)], ReadOnlyMemory<byte>.Empty);
        Ff7BoneGeometry part = new(0, "body.rsd", new("profile", "body.p", [], new Dictionary<string, string>()), "body.p", polygon, []);
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x020001);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(60), 90);
        PcFieldAnimation animation = PcFieldAnimation.Read(bytes);
        IReadOnlyList<Ff7BoneGeometry> parts = secondPart ? [part, part with { DescriptorName = "other.rsd", PolygonName = "other.p" }] : [part];
        Ff7FieldModelResources model = new("profile.hrc", hierarchy, parts, new Dictionary<string, PcFieldAnimation> { ["profile.a"] = animation });
        PcFieldModelDescriptor descriptor = new("profile", 0, "profile.hrc", "128", [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)], uint.MaxValue, ["profile.a"]);
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 10000), 0, 100);
        return Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, 0, position, 0, camera, Vector2.Zero, 50, 32000, new(level, 0, 0, 128, flags, 1), true);
    }
}
