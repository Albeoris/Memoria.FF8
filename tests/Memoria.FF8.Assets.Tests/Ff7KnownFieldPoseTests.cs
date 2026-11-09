using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7KnownFieldPoseTests
{
    [Fact]
    public void AuthenticatedRepairCoversEveryBodyJointAndKeepsTargetExtrasRootAndGeometry()
    {
        Ff7Installation installation = Installed();
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "southmk2");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(archive, field.Models.Models[2]);
        Ff7KnownFieldPoseBinding binding = Assert.IsType<Ff7KnownFieldPoseBinding>(model.KnownPoseBinding);
        Assert.Equal(Enumerable.Range(0, 21), binding.TargetToSource.Where(index => index >= 0).Order());
        Assert.Equal(new[] { 3, 4, 8 }, binding.TargetToSource.Select((source, target) => (source, target)).Where(item => item.source < 0).Select(item => item.target));
        Ff7FieldModelResources source = new("source", binding.SourceHierarchy, [], new Dictionary<string, PcFieldAnimation>());
        var sourceRest = Ff7FieldRenderer.Pose(source, binding.SourceBind, 0);
        var targetRest = Ff7FieldRenderer.Pose(model, binding.TargetBind, 0);
        Dictionary<string, int> targetIndices = model.Hierarchy.Bones.ToDictionary(bone => bone.Name, bone => bone.Index);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [2] = model }, new(50, 32000));
        for (int frame = 0; frame < 8; frame++)
        {
            var pose = Ff7FieldRenderer.Pose(model, binding.Motion, frame);
            var sourceCurrent = Ff7FieldRenderer.Pose(source, binding.Motion, frame);
            Assert.Equal(24, pose.Count);
            Assert.Equal(binding.Motion.Frames[frame].RootTranslation * new Vector3(1, -1, 1), pose[0].Translation);
            foreach (HrcBone bone in model.Hierarchy.Bones)
            {
                Assert.InRange(pose[bone.Index].GetDeterminant(), 0.9999f, 1.0001f);
                int sourceIndex = binding.TargetToSource[bone.Index];
                if (sourceIndex >= 0)
                {
                    Matrix4x4 expected = Rotation(targetRest[bone.Index]) * Matrix4x4.Transpose(Rotation(sourceRest[sourceIndex])) * Rotation(sourceCurrent[sourceIndex]);
                    Near(expected, Rotation(pose[bone.Index]));
                }
                else
                {
                    int parent = targetIndices[bone.ParentName];
                    Assert.True(Matrix4x4.Invert(targetRest[parent], out Matrix4x4 inverseRest));
                    Assert.True(Matrix4x4.Invert(pose[parent], out Matrix4x4 inverseCurrent));
                    Near(Rotation(targetRest[bone.Index] * inverseRest), Rotation(pose[bone.Index] * inverseCurrent));
                }
            }
            foreach (Ff7BoneGeometry geometry in model.Geometry)
            {
                Assert.All(geometry.Polygon.Vertices, vertex =>
                {
                    Vector3 point = Vector3.Transform(vertex, pose[geometry.BoneIndex]);
                    Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
                });
            }
            Vector2[]? first = null;
            for (int fraction = 0; fraction < 16; fraction++)
            {
                Capture capture = new();
                Ff7ActorVisual actor = new(5, 2, -610 * 4096, -2965 * 4096, 481 * 4096, 64, 6, frame * 16 + fraction);
                renderer.Render(capture, 320, 240, new(0, new(32, -32), [actor], new ushort[256], MovieActive: true));
                Assert.NotEmpty(capture.Positions);
                first ??= capture.Positions.ToArray();
                Assert.Equal(first, capture.Positions);
            }
        }
    }

    [Fact]
    public void EveryInputIsAuthenticatedAndUnprofiledBoundsAndCompatiblePosesStayStrict()
    {
        Ff7Installation installation = Installed();
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "southmk2");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        byte[][] original = new[] { "acgd.hrc", "adcb.a", "bzbc.a", "aagb.hrc", "abcd.a" }.Select(name => archive.ReadEntry(Assert.Single(archive.Find(name)))).ToArray();
        Assert.NotNull(Ff7KnownFieldPoseBinding.TryCreate(original[0], original[1], original[2], original[3], original[4]));
        for (int input = 0; input < original.Length; input++)
        {
            byte[][] changed = original.Select(bytes => (byte[])bytes.Clone()).ToArray();
            changed[input][^1] ^= 1;
            Assert.Null(Ff7KnownFieldPoseBinding.TryCreate(changed[0], changed[1], changed[2], changed[3], changed[4]));
        }
        Ff7FieldModelResources target = Ff7FieldModelResources.Load(archive, field.Models.Models[2]);
        Ff7FieldModelResources strict = target with { KnownPoseBinding = null };
        Assert.Throws<NotSupportedException>(() => Ff7FieldRenderer.Pose(strict, target.Animations["BZBC.a"], 7, Ff7FieldRenderer.ModelBasis(field.Models.Models[2]).M11));
        Assert.Equal(Ff7FieldRenderer.Pose(strict, target.Animations["ABCD.a"], 0), Ff7FieldRenderer.Pose(target, target.Animations["ABCD.a"], 0));
        Ff7FieldModelResources barret = Ff7FieldModelResources.Load(archive, field.Models.Models[1]);
        Assert.Null(barret.KnownPoseBinding);
        Assert.Equal(21, Ff7FieldRenderer.Pose(barret, barret.Animations["BZBC.a"], 7).Count);
        Assert.Throws<NotSupportedException>(() => target.Animations["BZBC.a"].ReadNativeBoneRotation(7, 21, 1));
        Assert.Throws<NotSupportedException>(() => Ff7FieldRenderer.Pose(target, target.Animations["BZBC.a"], 8));
    }

    private static Ff7Installation Installed()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Select original resources for the authenticated field pose repair.");
        return Ff7Installation.Discover(directory!);
    }

    private static Matrix4x4 Rotation(Matrix4x4 matrix)
    {
        Assert.True(Matrix4x4.Decompose(matrix, out _, out Quaternion rotation, out _));
        return Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
    }

    private static void Near(Matrix4x4 expected, Matrix4x4 actual)
    {
        foreach (var pair in new[] { (expected.M11, actual.M11), (expected.M12, actual.M12), (expected.M13, actual.M13), (expected.M21, actual.M21), (expected.M22, actual.M22), (expected.M23, actual.M23), (expected.M31, actual.M31), (expected.M32, actual.M32), (expected.M33, actual.M33) })
        {
            Assert.InRange(MathF.Abs(pair.Item1 - pair.Item2), 0, 0.00001f);
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<Vector2> Positions { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Positions.Add(a.Position);
            Positions.Add(b.Position);
            Positions.Add(c.Position);
        }
        public void End()
        {
        }
    }
}
