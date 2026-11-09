using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldLightingTests
{
    [Fact]
    public void AmbientUsesDescriptorRgbOrderAndPreservesNativeAlpha()
    {
        Ff7FieldLighting lighting = new(Descriptor(0x204080, [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)]));
        Vector4 color = lighting.Shade(Vector3.UnitX, 0x71FFFFFF);
        Assert.Equal(new Vector4(128 / 255f, 64 / 255f, 32 / 255f, 113 / 255f), color);
    }

    [Fact]
    public void DirectionalContributionsCombineBeforeTheAmbientFloorAndDoNotNormalize()
    {
        Ff7FieldLighting lighting = new(Descriptor(0x202020, [new(0x0000FF, new(-4096, 0, 0)), new(0x0000FF, new(2048, 0, 0)), new(0, Vector3.Zero)]));
        Vector4 lit = lighting.Shade(Vector3.UnitX, 0xFFFFFFFF);
        Assert.Equal(160 / 255f, lit.X);
        Assert.Equal(32 / 255f, lit.Y);
        Vector4 opposed = lighting.Shade(-Vector3.UnitX, 0xFFFFFFFF);
        Assert.Equal(32 / 255f, opposed.X);
        Vector4 halfNormal = lighting.Shade(Vector3.UnitX / 2, 0xFFFFFFFF);
        Assert.Equal(96 / 255f, halfNormal.X);
    }

    [Fact]
    public void LitVertexColorsSaturateAfterMultiplicationInsteadOfClampingTheLightFactor()
    {
        Ff7FieldLighting lighting = new(Descriptor(0, [new(0xFFFFFF, new(-6144, 0, 0)), new(0, Vector3.Zero), new(0, Vector3.Zero)]));
        Vector4 color = lighting.Shade(Vector3.UnitX, 0x7F80C840);
        Assert.Equal(new Vector4(192 / 255f, 1, 96 / 255f, 127 / 255f), color);
        Assert.Throws<InvalidDataException>(() => lighting.Shade(new(float.NaN, 0, 0), 0xFFFFFFFF));
        Assert.Throws<InvalidDataException>(() => new Ff7FieldLighting(Descriptor(0, [])));
    }

    [Fact]
    public void GroupVerticesKeepTheirFirstCornerNormalAndAbsentChannelsStayUnlit()
    {
        PcModelGroup group = new(0, 0, 2, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcPolygonModel model = new([Vector3.Zero, Vector3.Zero, Vector3.Zero], [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ], [], [], [], [new(0, 0, 1, 2, 0, 1, 2, ReadOnlyMemory<byte>.Empty), new(0, 0, 2, 1, 2, 0, 1, ReadOnlyMemory<byte>.Empty)], [], [group], ReadOnlyMemory<byte>.Empty);
        Assert.Equal(new[] { 0, 1, 2 }, Ff7FieldLighting.BindNormals(model, group));
        Assert.Equal(new[] { -1, -1, -1 }, Ff7FieldLighting.BindNormals(model with { Normals = [] }, group));
        Assert.Throws<InvalidDataException>(() => Ff7FieldLighting.BindNormals(model, group with { VertexStart = 1 }));
        Assert.Throws<InvalidDataException>(() => Ff7FieldLighting.BindNormals(model with { Normals = [Vector3.UnitX] }, group));
    }
    [Fact]
    public void OptionalInstalledOpeningModelsKeepNativeCornerNormalOwnership()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF7_GAME_DIR for the bounded original opening-lighting check.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        int normals = 0;
        foreach (PcFieldModelDescriptor descriptor in field.Models.Models)
        {
            Ff7FieldLighting lighting = new(descriptor);
            Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, descriptor);
            foreach (Ff7BoneGeometry part in model.Geometry)
            {
                foreach (PcModelGroup group in part.Polygon.Groups)
                {
                    int[] bindings = Ff7FieldLighting.BindNormals(part.Polygon, group);
                    foreach (int index in bindings.Where(index => index >= 0))
                    {
                        Vector4 color = lighting.Shade(part.Polygon.Normals[index], 0xFFFFFFFF);
                        Assert.True(float.IsFinite(color.X) && float.IsFinite(color.Y) && float.IsFinite(color.Z));
                        Assert.InRange(color.X, 0, 1);
                        Assert.InRange(color.Y, 0, 1);
                        Assert.InRange(color.Z, 0, 1);
                        normals++;
                    }
                }
            }
        }
        Assert.True(normals > 0);
    }
    private static PcFieldModelDescriptor Descriptor(uint ambient, IReadOnlyList<PcFieldLight> lights)
    {
        return new("fixture", 0, "fixture.hrc", "512", lights, ambient, []);
    }
}
