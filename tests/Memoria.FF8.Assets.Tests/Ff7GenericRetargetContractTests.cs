using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7GenericRetargetContractTests
{
    [Fact]
    public void ExactRigConverterUsesPoseContractsInsteadOfAnimationNames()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Selected original installations are required.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using GameAssets assets = new(ff8!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        FieldModel target = assets.LoadFieldModelSource(assets.GetFieldModelSource("bghall_1", 0));
        Matrix4x4 calibration = Ff7ModelAppearance.CalibrateNeutralGeometry(target, field.Models.Models[0], original, [19, 20], 21, 22).Transform;
        Matrix4x4 basis = Ff7FieldRenderer.ModelBasis(field.Models.Models[0]);
        Ff7SemanticRetargeter retargeter = new(target, field.Models.Models[0], original, calibration, Ff7ModelAppearance.StandardFieldSquallJoints, Ff7ModelAppearance.StandardFieldSquallFeet);
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        byte[] authored = AuthoredPoses(archive.ReadEntry(Assert.Single(archive.Find("acfe.a"))));
        PcFieldAnimation clip = PcFieldAnimation.Read(authored);
        PcFieldModelDescriptor descriptor = field.Models.Models[0] with { Animations = ["compatible-authored.a"] };
        Ff7FieldModelResources resources = original with { Animations = new Dictionary<string, PcFieldAnimation>(original.Animations, StringComparer.OrdinalIgnoreCase) { ["compatible-authored.a"] = clip } };
        Vector3[] neutral = target.Pose(0, 0);
        var pairs = target.Vertices.Select((vertex, index) => (vertex.Bone, Index: index)).GroupBy(item => item.Bone).Where(group => group.Count() > 1).Select(group => (A: group.First().Index, B: group.Last().Index)).ToArray();
        HashSet<int> feet = original.Hierarchy.Bones.Where(bone => bone.Name is "l_foot" or "r_foot").Select(bone => bone.Index).ToHashSet();
        byte[] translated = (byte[])authored.Clone();
        int stride = 24 + 12 * clip.BoneCount;
        for (int frame = 0; frame < clip.Frames.Count; frame++)
        {
            int offset = 48 + frame * stride;
            BinaryPrimitives.WriteSingleLittleEndian(translated.AsSpan(offset), BinaryPrimitives.ReadSingleLittleEndian(translated.AsSpan(offset)) + 10);
        }
        PcFieldAnimation moved = PcFieldAnimation.Read(translated);
        foreach (int phase in new[] { 0, 7, 16, 23, 32, 39, 48, 55, 63 })
        {
            Vector3[] pose = retargeter.Pose(original, clip, phase);
            Assert.All(pose, vertex => Assert.True(float.IsFinite(vertex.X) && float.IsFinite(vertex.Y) && float.IsFinite(vertex.Z)));
            foreach (var pair in pairs)
            {
                Assert.True(MathF.Abs(Vector3.Distance(neutral[pair.A], neutral[pair.B]) - Vector3.Distance(pose[pair.A], pose[pair.B])) < 0.003f);
            }
            if ((phase & 15) == 0)
            {
                var sourcePose = Ff7FieldRenderer.Pose(original, clip, phase >> 4);
                float sourceFloor = original.Geometry.Where(part => feet.Contains(part.BoneIndex)).SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, sourcePose[part.BoneIndex] * basis).Z)).Min();
                float targetFloor = target.Vertices.Select((vertex, index) => (vertex.Bone, Position: pose[index])).Where(item => item.Bone is 19 or 20).Min(item => Vector3.Transform(item.Position, calibration).Z);
                Assert.True(MathF.Abs(sourceFloor - targetFloor) < 0.003f);
            }
            Vector3[] movedPose = retargeter.Pose(original, moved, phase);
            Vector3 expected = Vector3.TransformNormal(new Vector3(10, 0, 0), basis);
            for (int vertex = 0; vertex < pose.Length; vertex++)
            {
                Assert.True(Vector3.Distance(expected, Vector3.TransformNormal(movedPose[vertex] - pose[vertex], calibration)) < 0.003f);
            }
            Ff7ActorVisual actor = new(0, 0, 123 * 4096, 456 * 4096, 789 * 4096, 73, 0, phase);
            Assert.True(appearance.TryCreate(actor, descriptor, resources, out var first));
            PcFieldModelDescriptor renamed = descriptor with { Animations = ["different-compatible-name.a"] };
            Ff7FieldModelResources renamedResources = resources with { Animations = new Dictionary<string, PcFieldAnimation>(resources.Animations, StringComparer.OrdinalIgnoreCase) { ["different-compatible-name.a"] = clip } };
            Assert.True(appearance.TryCreate(actor, renamed, renamedResources, out var second));
            Assert.Equal(first, second);
        }
        Assert.Throws<InvalidDataException>(() => retargeter.Pose(original, clip, 64));
        byte[] unsupportedOrder = (byte[])authored.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(unsupportedOrder.AsSpan(12), 0x010002);
        Assert.Throws<NotSupportedException>(() => retargeter.Pose(original, PcFieldAnimation.Read(unsupportedOrder), 0));
    }

    private static byte[] AuthoredPoses(byte[] idle)
    {
        int bones = BinaryPrimitives.ReadInt32LittleEndian(idle.AsSpan(8));
        int stride = 24 + bones * 12;
        byte[] bytes = new byte[36 + 4 * stride];
        idle.AsSpan(0, 36).CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        for (int frame = 0; frame < 4; frame++)
        {
            idle.AsSpan(36, stride).CopyTo(bytes.AsSpan(36 + frame * stride));
            float sign = frame == 1 ? 1 : frame == 2 ? -1 : 0;
            int start = 36 + frame * stride;
            for (int bone = 0; bone < bones; bone++)
            {
                Add(start + 24 + bone * 12, sign * (bone % 3 - 1) * 100);
                Add(start + 28 + bone * 12, sign * (bone % 5 - 2) * 60);
                Add(start + 32 + bone * 12, sign * (bone % 7 - 3) * 45);
            }
            Add(start + 12, frame * 7);
            Add(start + 16, frame * -3);
            Add(start + 20, frame * 2);
        }
        return bytes;

        void Add(int offset, float value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset)) + value);
        }
    }
}
