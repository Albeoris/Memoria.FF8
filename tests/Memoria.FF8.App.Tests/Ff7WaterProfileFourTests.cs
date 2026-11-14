using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7WaterProfileFourTests
{
    [Theory]
    [InlineData(-230f, -233d)]
    [InlineData(-229f, -233d)]
    [InlineData(-228.5f, -232.4394989013672d)]
    [InlineData(-1f, 22.5880126953125d)]
    [InlineData(0f, -200d)]
    [InlineData(1f, -198.88999938964844d)]
    public void PiecewiseNativeFloatStoresRetainLowerZeroAndMiddleBoundaries(float firstHeight, double expectedPlane)
    {
        Ff7FieldWaterGeometry geometry = AuthoredCapture(new(0, -1350, firstHeight), Vector3.Zero, new(1, -32, -64, -32, 0, 4));
        Ff7FieldWaterColorRegion region = Assert.IsType<Ff7FieldWaterColorRegion>(geometry.ColorRegion);
        Assert.True(region.HasThreshold);
        Assert.False(region.UseY);
        Assert.Equal(expectedPlane, region.Height);
        Assert.Equal(new[] { firstHeight > expectedPlane, true, false }, Assert.Single(geometry.Parts).ModifiedVertices);
    }

    [Fact]
    public void ColorAndJointInhibitionLatchesAreIndependentAndLevelOverrideUsesFirstY()
    {
        Ff7FieldWaterParameters parameters = new(1, -32, -64, -32, 0, 4);
        Ff7FieldWaterGeometry colorInhibited = AuthoredCapture(new(0, -1400, -230), new(0, 200, 0), parameters, secondPart: true);
        Assert.False(colorInhibited.ColorRegion!.HasThreshold);
        Assert.Equal(2, colorInhibited.Parts.Count);
        Assert.All(colorInhibited.Parts, part => Assert.All(part.ModifiedVertices, modified => Assert.False(modified)));
        Assert.Single(colorInhibited.Splashes);
        Ff7FieldWaterGeometry jointInhibited = AuthoredCapture(new(0, -1300, -230), new(0, -200, 0), parameters);
        Assert.True(jointInhibited.ColorRegion!.HasThreshold);
        Assert.Contains(true, Assert.Single(jointInhibited.Parts).ModifiedVertices);
        Assert.Empty(jointInhibited.Splashes);
        Ff7FieldWaterGeometry boundary = AuthoredCapture(new(0, -1350, -230), Vector3.Zero, parameters);
        Assert.True(boundary.ColorRegion!.HasThreshold);
        Assert.Single(boundary.Splashes);
        Ff7FieldWaterGeometry aboveBoundary = AuthoredCapture(new(0, -1349.5f, -230), Vector3.Zero, parameters);
        Assert.False(aboveBoundary.ColorRegion!.HasThreshold);
        Assert.Empty(aboveBoundary.Splashes);
        Ff7FieldWaterGeometry levelTwo = AuthoredCapture(new(0, -1350, -230), Vector3.Zero, parameters with { Level = 2 });
        Assert.Equal(-2000, levelTwo.ColorRegion!.Height);
        Assert.All(Assert.Single(levelTwo.Parts).ModifiedVertices, modified => Assert.True(modified));
        Assert.Single(levelTwo.Splashes);
        Ff7FieldWaterGeometry zeroLevel = AuthoredCapture(new(0, -1350, -230), Vector3.Zero, parameters with { Level = 0 });
        Assert.True(zeroLevel.ColorRegion!.HasThreshold);
        Assert.Equal(0, zeroLevel.ColorRegion.Height);
        Ff7FieldWaterGeometry selectedY = AuthoredCapture(new(0, -1350, -230), Vector3.Zero, parameters with { Flags = 4 });
        Assert.True(selectedY.ColorRegion!.UseY);
        Assert.Equal(-233, selectedY.ColorRegion.Height);
        Assert.Equal(new[] { false, true, false }, Assert.Single(selectedY.Parts).ModifiedVertices);
    }

    [Fact]
    public void OriginalCloudAndDolphinRequestsCaptureFiniteColdRegionsAndMappedTargetMasks()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Select original FFVII and FFVIII resources for the reached water profiles and mapped appearance.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon3");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        using GameAssets assets = new(ff8!);
        foreach ((int modelIndex, int requestOffset, short level) in new (int, int, short)[] { (0, 0x0C85, 1), (2, 0x0DC6, 2) })
        {
            byte[] request = field.Scripts.Data.Span.Slice(requestOffset, 14).ToArray();
            Assert.Equal(new byte[] { 0x28, 0x0E, 9 }, request[..3]);
            byte[] payload = request[3..];
            Assert.Equal(0, payload[0]);
            Assert.Equal(level, BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(2)));
            Ff7FieldWaterParameters parameters = new(level, BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(6)), BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8)), payload[10], 4);
            Assert.Equal(new short[] { -32, -64, -32 }, new[] { parameters.Red, parameters.Green, parameters.Blue });
            PcFieldModelDescriptor descriptor = field.Models.Models[modelIndex];
            Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, descriptor);
            PcFieldAnimation animation = model.Animations[Path.ChangeExtension(descriptor.Animations[0], ".a")];
            Vector3 position = new(0, -5000, -230);
            Ff7FieldWaterGeometry geometry = Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, 0, position, 0, Camera(), Vector2.Zero, 50, 32000, parameters, true);
            Ff7FieldWaterColorRegion region = Assert.IsType<Ff7FieldWaterColorRegion>(geometry.ColorRegion);
            Assert.True(region.HasThreshold);
            Assert.True(double.IsFinite(region.Height));
            Assert.Equal(model.Geometry.Count, geometry.Parts.Count);
            foreach (Ff7FieldWaterPartMask part in geometry.Parts)
            {
                Assert.Equal(model.Geometry[part.Part].Polygon.Vertices.Count, part.ModifiedVertices.Length);
            }
            Assert.All(geometry.Splashes, splash =>
            {
                Assert.True(float.IsFinite(splash.X) && float.IsFinite(splash.Y) && float.IsFinite(splash.Depth));
                Assert.Equal(12, splash.HalfSize);
            });
            string saved = JsonSerializer.Serialize(geometry);
            Ff7FieldWaterGeometry cold = JsonSerializer.Deserialize<Ff7FieldWaterGeometry>(saved)!;
            Assert.Equal(saved, JsonSerializer.Serialize(cold));
            if (level == 2)
            {
                Assert.Equal(-2000, region.Height);
                continue;
            }
            Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateFieldCharacter(assets, 0, descriptor, model);
            Ff7ActorVisual actor = new(18, modelIndex, 0, -5000 * 4096, -230 * 4096, 0, 0, 0) { PartyCharacter = 0 };
            Assert.True(appearance.TrySampleVertices(actor, descriptor, model, out IReadOnlyList<Vector3> targetVertices));
            bool[] modified = Ff7FieldRenderer.CaptureAppearanceVertexColorMask(targetVertices, region);
            bool[] coldModified = Ff7FieldRenderer.CaptureAppearanceVertexColorMask(targetVertices, cold.ColorRegion!);
            Assert.NotEmpty(modified);
            Assert.Equal(modified, coldModified);
            for (int vertex = 0; vertex < targetVertices.Count; vertex++)
            {
                Assert.Equal(region.Contains(targetVertices[vertex]), modified[vertex]);
            }
        }
    }

    private static Ff7FieldWaterGeometry AuthoredCapture(Vector3 position, Vector3 firstVertex, Ff7FieldWaterParameters parameters, bool secondPart = false)
    {
        string descriptors = secondPart ? "2 body other" : "1 body";
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes($":HEADER_BLOCK 2\n:SKELETON profile\n:BONES 1\nbody\nroot\n3000\n{descriptors}\n"));
        PcPolygonModel polygon = new([firstVertex, new(0, 2000, 200), new(0, -2000, -300)], [], [], [uint.MaxValue, uint.MaxValue, uint.MaxValue], [], [], [], [new(0, 0, 0, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty)], ReadOnlyMemory<byte>.Empty);
        Ff7BoneGeometry part = new(0, "body.rsd", new("profile", "body.p", [], new Dictionary<string, string>()), "body.p", polygon, []);
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x020001);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(60), 90);
        PcFieldAnimation animation = PcFieldAnimation.Read(bytes);
        IReadOnlyList<Ff7BoneGeometry> parts = [part];
        if (secondPart)
        {
            parts = [part, part with { DescriptorName = "other.rsd", PolygonName = "other.p", Polygon = polygon with { Vertices = [new(0, -200, 0), new(0, -200, 200), new(0, -200, -300)] } }];
        }
        Ff7FieldModelResources model = new("profile.hrc", hierarchy, parts, new Dictionary<string, PcFieldAnimation> { ["profile.a"] = animation });
        PcFieldModelDescriptor descriptor = new("profile", 0, "profile.hrc", "128", [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)], uint.MaxValue, ["profile.a"]);
        return Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, 0, position, 0, Camera(), Vector2.Zero, 50, 32000, parameters, true);
    }

    private static PcFieldCamera Camera()
    {
        return new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 10000), 0, 100);
    }
}
