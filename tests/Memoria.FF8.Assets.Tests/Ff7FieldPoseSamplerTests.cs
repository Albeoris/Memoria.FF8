using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldPoseSamplerTests
{
    [Fact]
    public void SamplesLocalRotationsAndRootTranslationAtExactEndpointsAndMidpoint()
    {
        Ff7FieldModelResources model = Model();
        PcFieldAnimation animation = Animation();
        Ff7FieldPose first = Ff7FieldPoseSampler.Sample(model, animation, new(16));
        Ff7FieldPose second = Ff7FieldPoseSampler.Sample(model, animation, new(32));
        Ff7FieldPose midpoint = Ff7FieldPoseSampler.Sample(model, animation, new(24));

        Assert.Equal(new Vector3(10, 5, 2), first.RootTranslation);
        Assert.Equal(new Vector3(20, 10, 4), second.RootTranslation);
        Assert.Equal(new Vector3(15, 7.5f, 3), midpoint.RootTranslation);
        Assert.Equal(new Vector3(15, -7.5f, 3), midpoint.Bones[0].Translation);
        Assert.Equal(Ff7FieldRenderer.Pose(model, animation, 1).ToArray(), first.Bones.ToArray());
        Assert.Equal(Matrix4x4.CreateTranslation(10, -5, 2), first.Bones[0]);
        Vector3 expected = Vector3.TransformNormal(Vector3.UnitX, Matrix4x4.CreateRotationY(MathF.PI / 4));
        Assert.InRange(Vector3.Distance(expected, Vector3.TransformNormal(Vector3.UnitX, midpoint.Bones[0])), 0, 0.0001f);
        Assert.InRange(Vector3.Distance(Vector3.Transform(new(0, 0, -2), midpoint.Bones[0]), midpoint.Bones[1].Translation), 0, 0.0001f);
    }

    [Fact]
    public void PartialClipsWrapToTheirFirstFrameOrHoldAtTheirTerminalFrame()
    {
        Ff7FieldModelResources model = Model();
        PcFieldAnimation animation = Animation();
        Ff7FieldPose loop = Ff7FieldPoseSampler.Sample(model, animation, new(40, FirstFrame: 1, LastFrame: 2, Loop: true));
        Ff7FieldPose hold = Ff7FieldPoseSampler.Sample(model, animation, new(40, FirstFrame: 1, LastFrame: 2));
        Ff7FieldPose midpoint = Ff7FieldPoseSampler.Sample(model, animation, new(24, FirstFrame: 1, LastFrame: 2));
        Assert.Equal(midpoint.RootTranslation, loop.RootTranslation);
        Assert.InRange(Vector3.Distance(midpoint.Bones[1].Translation, loop.Bones[1].Translation), 0, 0.0001f);
        Assert.Equal(new Vector3(20, 10, 4), hold.RootTranslation);
        Assert.Equal(Ff7FieldRenderer.Pose(model, animation, 2).ToArray(), hold.Bones.ToArray());
    }

    private static Ff7FieldModelResources Model()
    {
        HrcHierarchy hierarchy = HrcHierarchy.Read(":HEADER_BLOCK 2\n:SKELETON test\n:BONES 2\nparent\nroot\n2\n0\nchild\nparent\n1\n0\n"u8);
        return new("test", hierarchy, [], new Dictionary<string, PcFieldAnimation>());
    }

    private static PcFieldAnimation Animation()
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes);
        writer.Write(1);
        writer.Write(4);
        writer.Write(2);
        writer.Write(0x020001);
        writer.Write(new byte[20]);
        for (int frame = 0; frame < 4; frame++)
        {
            foreach (Vector3 vector in new[] { Vector3.Zero, new Vector3(frame * 10, frame * 5, frame * 2), new Vector3(0, frame == 2 ? 90 : 0, 0), Vector3.Zero })
            {
                writer.Write(vector.X);
                writer.Write(vector.Y);
                writer.Write(vector.Z);
            }
        }
        return PcFieldAnimation.Read(bytes.ToArray());
    }
}
