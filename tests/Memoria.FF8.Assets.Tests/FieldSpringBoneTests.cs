using System.Numerics;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldSpringBoneTests
{
    private const float Tick = 1f / 30;

    [Fact]
    public void MovingActorLeavesItsTailBehindUntilTheLengthConstraintProjectsIt()
    {
        FieldSpringBoneJoint joint = new(0, Vector3.UnitX, 0, 0, Vector3.Zero);
        FieldSpringBoneSimulation simulation = new([-1, 0], [joint]);
        Matrix4x4[] animated = [Matrix4x4.Identity, Matrix4x4.CreateTranslation(Vector3.UnitX)];
        simulation.Step(animated, Matrix4x4.Identity, Tick);

        Matrix4x4 actorTransform = Matrix4x4.CreateTranslation(Vector3.UnitY);
        Matrix4x4[] moved = simulation.Step(animated, actorTransform, Tick);

        AssertNear(moved[0].Translation, Vector3.Zero);
        AssertNear(moved[1].Translation, Vector3.Normalize(Vector3.UnitX - Vector3.UnitY));

        simulation.Reset();
        Matrix4x4[] reset = simulation.Step(animated, actorTransform, Tick);
        AssertNear(reset[1].Translation, Vector3.UnitX);
    }

    [Fact]
    public void ScaledActorAndUnorderedHierarchyPreserveTheAnimatedLengths()
    {
        Vector3 pivot = new(2, 3, 4);
        Matrix4x4[] animated =
        [
            Matrix4x4.CreateTranslation(pivot + Vector3.UnitX * 2),
            Matrix4x4.CreateTranslation(pivot),
            Matrix4x4.CreateTranslation(pivot + Vector3.UnitX),
        ];
        Matrix4x4 actorTransform = Matrix4x4.CreateScale(-0.27f, 0.83f, 2.1f) * Matrix4x4.CreateRotationZ(0.23f) * Matrix4x4.CreateTranslation(15, -12, 30);
        Vector3 gravity = Vector3.TransformNormal(new Vector3(0, -25, 0), actorTransform);
        FieldSpringBoneSimulation simulation = new([2, -1, 1], [new FieldSpringBoneJoint(1, Vector3.UnitX, 20, 5, gravity)]);

        Matrix4x4[] posed = simulation.Step(animated, actorTransform, Tick);

        Assert.True(posed[2].Translation.Y < pivot.Y - 0.02f);
        AssertNear(posed[1].Translation, pivot);
        AssertNear(posed[2].Translation, Vector3.Transform(Vector3.UnitX, posed[1]));
        AssertNear(posed[0].Translation, Vector3.Transform(Vector3.UnitX * 2, posed[1]));
        Assert.InRange(Vector3.Distance(posed[2].Translation, pivot), 0.9999f, 1.0001f);
        Assert.InRange(Vector3.Distance(posed[0].Translation, posed[2].Translation), 0.9999f, 1.0001f);
    }

    [Fact]
    public void CapturedTailInterpolationIsStableAndDoesNotAdvanceSimulation()
    {
        FieldSpringBoneJoint joint = new(0, Vector3.UnitX, 20, 5, new Vector3(0, -25, 0));
        FieldSpringBoneSimulation simulation = new([-1, 0], [joint]);
        FieldSpringBoneSimulation control = new([-1, 0], [joint]);
        Matrix4x4[] animated = [Matrix4x4.Identity, Matrix4x4.CreateTranslation(Vector3.UnitX)];

        FieldSpringBoneSimulation.FieldSpringBonePose previous = simulation.Capture();
        Matrix4x4[] bent = simulation.Step(animated, Matrix4x4.Identity, Tick);
        FieldSpringBoneSimulation.FieldSpringBonePose current = simulation.Capture();
        Matrix4x4[] midpoint = simulation.Pose(animated, Matrix4x4.Identity, previous, current, 0.5f);
        Vector3 expectedMidpoint = Vector3.Normalize(Vector3.UnitX + bent[1].Translation);

        AssertNear(simulation.Pose(animated, Matrix4x4.Identity, previous, current, 0)[1].Translation, Vector3.UnitX);
        AssertNear(simulation.Pose(animated, Matrix4x4.Identity, previous, current, 1)[1].Translation, bent[1].Translation);
        AssertNear(midpoint[1].Translation, expectedMidpoint);

        control.Step(animated, Matrix4x4.Identity, Tick);
        Matrix4x4[] next = simulation.Step(animated, Matrix4x4.Identity, Tick);
        Matrix4x4[] controlNext = control.Step(animated, Matrix4x4.Identity, Tick);
        AssertNear(next[1].Translation, controlNext[1].Translation);
        AssertNear(simulation.Pose(animated, Matrix4x4.Identity, previous, current, 0.5f)[1].Translation, expectedMidpoint);
    }

    [Fact]
    public void SimulationRejectsInvalidRigAndJointDefinitions()
    {
        FieldSpringBoneJoint joint = new(0, Vector3.UnitX, 20, 5, Vector3.Zero);

        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([1, 0], [joint]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([2, -1], [joint]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([-1], [joint, joint]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([-1], [joint with { Bone = 1 }]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([-1], [joint with { LocalTail = Vector3.Zero }]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([-1], [joint with { Stiffness = -1 }]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([-1], [joint with { Damping = float.NaN }]));
        Assert.Throws<ArgumentException>(() => new FieldSpringBoneSimulation([-1], [joint with { WorldGravity = new Vector3(float.PositiveInfinity) }]));
    }

    [Fact]
    public void ImportedFieldVerticesAndNormalsUseTheSameSpringPose()
    {
        EditableBone[] bones =
        [
            new("root", -1, Vector3.Zero, Quaternion.Identity, Vector3.One),
            new("spring", 0, Vector3.UnitY, Quaternion.Identity, Vector3.One),
        ];
        EditableVertex vertex = new(Vector3.UnitY * 2, Vector3.UnitY, Vector2.Zero, Vector4.UnitX, Vector4.UnitX, Vector4.One);
        EditableMesh mesh = new("skinned", [vertex], [], 0, Matrix4x4.Identity) { Skinned = true };
        EditableModel edited = new("editable", "field", bones, [mesh], [], [], default);
        FieldModel field = new("field", [], [], [], [], []) { EditableOverride = edited };
        Matrix4x4[] animated = field.ReadPresentationBoneTransforms(0, 0, null, 3, null);
        AssertNear(Assert.Single(field.PosePresentationBones(animated, null)), Assert.Single(field.Pose(0, 0, rootTranslationZ: 3)));
        AssertNear(Assert.Single(field.PosePresentationNormals(animated)!), Assert.Single(ModelExchange.PoseNativeNormals(edited, 0, 0)));

        FieldSpringBoneSimulation simulation = new(field.GetSpringBoneParents(), [new(1, Vector3.UnitZ, 20, 5, new(50, 0, 0))]);
        Matrix4x4[] bent = simulation.Step(animated, Matrix4x4.Identity, Tick);
        Vector3 normal = Assert.Single(field.PosePresentationNormals(bent)!);
        Vector3 position = Assert.Single(field.PosePresentationBones(bent, null));
        Assert.True(normal.X > 0.01f);
        AssertNear(position - bent[1].Translation, normal);
    }

    private static void AssertNear(Vector3 actual, Vector3 expected)
    {
        Assert.True(Vector3.Distance(actual, expected) <= 1e-4f, $"Expected {expected}, found {actual}.");
    }
}
