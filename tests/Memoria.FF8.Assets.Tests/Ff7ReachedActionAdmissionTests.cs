using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7ReachedActionAdmissionTests
{
    private static readonly string[] actions = ["auee.a", "ajdd.a", "bxib.a", "byaa.a", "bxif.a", "byad.a", "apjf.a"];

    [Fact]
    public void ReachedOpeningActionsPreserveRigidityFootContactsRootMotionAndNativePhases()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Selected FF7 and FF8 installations are required for the reached-action admission check.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using GameAssets assets = new(ff8!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        FieldModel target = assets.LoadFieldModelSource(assets.GetFieldModelSource("bghall_1", 0));
        Matrix4x4 calibration = Ff7ModelAppearance.CalibrateNeutralGeometry(target, field.Models.Models[0], original, [19, 20], 21, 22).Transform;
        Matrix4x4 basis = Ff7FieldRenderer.ModelBasis(field.Models.Models[0]);
        Ff7SemanticRetargeter retargeter = new(target, field.Models.Models[0], original, calibration, Ff7ModelAppearance.StandardFieldSquallJoints, Ff7ModelAppearance.StandardFieldSquallFeet);
        HashSet<int> sourceFeet = original.Hierarchy.Bones.Where(bone => bone.Name is "l_foot" or "r_foot").Select(bone => bone.Index).ToHashSet();
        Vector3[] neutral = target.Pose(0, 0);
        var rigidPairs = target.Vertices.Select((vertex, index) => (vertex.Bone, Index: index)).GroupBy(vertex => vertex.Bone).Where(group => group.Count() > 1).Select(group => (First: group.First().Index, Last: group.Last().Index)).ToArray();
        List<object> measurements = [];
        List<object> controlledPoses = [];
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        foreach (string name in actions)
        {
            byte[] bytes = archive.ReadEntry(Assert.Single(archive.Find(name)));
            PcFieldAnimation clip = PcFieldAnimation.Read(bytes);
            Dictionary<string, PcFieldAnimation> admittedAnimations = new(original.Animations, StringComparer.OrdinalIgnoreCase) { [name] = clip };
            Ff7FieldModelResources admittedOriginal = original with { Animations = admittedAnimations };
            PcFieldModelDescriptor admittedDescriptor = field.Models.Models[0] with { Animations = [.. field.Models.Models[0].Animations, name] };
            Assert.Equal(original.Hierarchy.DeclaredBoneCount, clip.BoneCount);
            Assert.Equal(0x020001u, clip.RotationOrder);
            float maximumFootError = 0;
            float maximumRigidError = 0;
            float maximumDisplacement = 0;
            for (int frame = 0; frame < clip.Frames.Count; frame++)
            {
                Vector3[] pose = retargeter.Pose(original, clip, frame * 16);
                Assert.All(pose, vertex => Assert.True(Finite(vertex), $"{name} frame {frame} has a nonfinite target vertex."));
                foreach (var pair in rigidPairs)
                {
                    float expected = Vector3.Distance(neutral[pair.First], neutral[pair.Last]);
                    float actual = Vector3.Distance(pose[pair.First], pose[pair.Last]);
                    maximumRigidError = MathF.Max(maximumRigidError, MathF.Abs(expected - actual));
                    Assert.True(MathF.Abs(expected - actual) < 0.003f, $"{name} frame {frame} deformed a rigid target bone attachment.");
                }
                IReadOnlyList<Matrix4x4> sourcePose = Ff7FieldRenderer.Pose(original, clip, frame);
                float sourceFloor = original.Geometry.Where(part => sourceFeet.Contains(part.BoneIndex)).SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, sourcePose[part.BoneIndex] * basis).Z)).Min();
                float targetFloor = target.Vertices.Select((vertex, index) => (vertex.Bone, Position: pose[index])).Where(vertex => vertex.Bone is 19 or 20).Min(vertex => Vector3.Transform(vertex.Position, calibration).Z);
                maximumFootError = MathF.Max(maximumFootError, MathF.Abs(sourceFloor - targetFloor));
                Assert.True(MathF.Abs(sourceFloor - targetFloor) < 0.003f, $"{name} frame {frame} lost the source foot-height contact.");
                maximumDisplacement = MathF.Max(maximumDisplacement, pose.Select((vertex, index) => Vector3.Distance(vertex, neutral[index])).Max());
            }
            int[] samples = new[] { 0, clip.Frames.Count / 4, clip.Frames.Count / 2, clip.Frames.Count * 3 / 4, clip.Frames.Count - 1 }.Distinct().ToArray();
            foreach (int frame in samples)
            {
                int phase = Math.Min(clip.Frames.Count * 16 - 1, frame * 16 + 7);
                Vector3[] pose = retargeter.Pose(original, clip, phase);
                Assert.All(pose, vertex => Assert.True(Finite(vertex)));
                Ff7ActorVisual actor = new(0, 0, 123 * 4096, 456 * 4096, 789 * 4096, 73, admittedDescriptor.Animations.Count - 1, phase);
                Assert.True(appearance.TryCreate(actor, admittedDescriptor, admittedOriginal, out IReadOnlyList<Ff7AppearanceTriangle> submitted));
                Assert.NotEmpty(submitted);
                Assert.All(submitted.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), vertex => Assert.True(Finite(vertex.Position) && vertex.Normal is Vector3 normal && MathF.Abs(normal.LengthSquared() - 1) < 0.0001f));
                Matrix4x4 world = calibration * Matrix4x4.CreateRotationZ(actor.Direction * MathF.Tau / 256) * Matrix4x4.CreateTranslation(123, 456, 789);
                List<Vector3> expectedVertices = [];
                foreach (ModelFace face in target.Faces)
                {
                    Add(face.Vertices[0], face.Vertices[1], face.Vertices[2]);
                    if (face.Quad)
                    {
                        Add(face.Vertices[1], face.Vertices[3], face.Vertices[2]);
                    }
                }
                Vector3[] actualVertices = submitted.SelectMany(triangle => new[] { triangle.A.Position, triangle.B.Position, triangle.C.Position }).ToArray();
                Assert.Equal(expectedVertices.Count, actualVertices.Length);
                for (int vertex = 0; vertex < actualVertices.Length; vertex++)
                {
                    Assert.True(Vector3.Distance(expectedVertices[vertex], actualVertices[vertex]) < 0.003f, $"{name} did not submit the requested native phase {phase} at the original actor anchor.");
                }

                void Add(int first, int second, int third)
                {
                    Vector3 a = Vector3.Transform(pose[first], world);
                    Vector3 b = Vector3.Transform(pose[second], world);
                    Vector3 c = Vector3.Transform(pose[third], world);
                    if (Vector3.Cross(b - a, c - a).LengthSquared() != 0)
                    {
                        expectedVertices.Add(a);
                        expectedVertices.Add(b);
                        expectedVertices.Add(c);
                    }
                }
                byte[] translatedBytes = (byte[])bytes.Clone();
                int stride = 24 + clip.BoneCount * 12;
                for (int index = 0; index < clip.Frames.Count; index++)
                {
                    int offset = 48 + index * stride;
                    BinaryPrimitives.WriteSingleLittleEndian(translatedBytes.AsSpan(offset), BinaryPrimitives.ReadSingleLittleEndian(translatedBytes.AsSpan(offset)) + 10);
                }
                Vector3[] translated = retargeter.Pose(original, PcFieldAnimation.Read(translatedBytes), phase);
                Vector3 expectedRoot = Vector3.TransformNormal(new Vector3(10, 0, 0), basis);
                for (int vertex = 0; vertex < pose.Length; vertex++)
                {
                    Vector3 actualRoot = Vector3.TransformNormal(translated[vertex] - pose[vertex], calibration);
                    Assert.True(Vector3.Distance(expectedRoot, actualRoot) < 0.003f, $"{name} phase {phase} lost the original root displacement.");
                }
                if (Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTION_POSE_REPORT") is not null)
                {
                    IReadOnlyList<Matrix4x4> sourcePose = Ff7FieldRenderer.Pose(original, clip, frame);
                    controlledPoses.Add(new { Clip = name, Frame = frame, Source = original.Geometry.SelectMany(part => part.Polygon.Vertices.Select(vertex => Coordinates(Vector3.Transform(vertex, sourcePose[part.BoneIndex] * basis)))).ToArray(), Target = retargeter.Pose(original, clip, frame * 16).Select(vertex => Coordinates(Vector3.Transform(vertex, calibration))).ToArray() });
                }
            }
            Assert.True(maximumDisplacement > 0.1f, $"{name} unexpectedly became a static neutral fallback.");
            Ff7ActorVisual invalidPhase = new(0, 0, 0, 0, 0, 0, admittedDescriptor.Animations.Count - 1, clip.Frames.Count * 16);
            Assert.Throws<InvalidDataException>(() => appearance.TryCreate(invalidPhase, admittedDescriptor, admittedOriginal, out _));
            measurements.Add(new { Clip = name, Frames = clip.Frames.Count, RootStart = Coordinates(clip.Frames[0].RootTranslation), RootEnd = Coordinates(clip.Frames[^1].RootTranslation), MaximumRigidError = maximumRigidError, MaximumFootError = maximumFootError, MaximumTargetDisplacement = maximumDisplacement });
            Console.WriteLine($"action={name} frames={clip.Frames.Count} rigidError={maximumRigidError:R} footError={maximumFootError:R} displacement={maximumDisplacement:R}");
        }
        PcFieldAnimation unadmittedClip = original.Animations["acfe.a"];
        Ff7FieldModelResources unadmittedOriginal = original with { Animations = new Dictionary<string, PcFieldAnimation>(original.Animations, StringComparer.OrdinalIgnoreCase) { ["unadmitted-action.a"] = unadmittedClip } };
        PcFieldModelDescriptor unadmittedDescriptor = field.Models.Models[0] with { Animations = [.. field.Models.Models[0].Animations, "unadmitted-action.a"] };
        Assert.Throws<NotSupportedException>(() => appearance.TryCreate(new Ff7ActorVisual(0, 0, 0, 0, 0, 0, unadmittedDescriptor.Animations.Count - 1, 0), unadmittedDescriptor, unadmittedOriginal, out _));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTION_METRICS_REPORT") is string metrics)
        {
            File.WriteAllText(metrics, JsonSerializer.Serialize(measurements));
        }
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTION_POSE_REPORT") is string poses)
        {
            File.WriteAllText(poses, JsonSerializer.Serialize(controlledPoses));
        }
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static float[] Coordinates(Vector3 value) => [value.X, value.Y, value.Z];
}
