using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using FieldCharacterCatalog = Memoria.FF8.Assets.FieldCharacterCatalog;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7ModelAppearanceTests
{
    [Fact]
    public void CloseupCloudUsesTheExplicitSquallRigAndRetainsNativeChannelBounds()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Installed source and target field resources are required.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "rootmap");
        PcFieldModelDescriptor descriptor = field.Models.Models[0];
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(characters, descriptor);
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        Assert.StartsWith(Ff7ModelAppearance.StandardFieldSquallProfileVersion, appearance.ProfileId);
        Ff7ActorVisual actor = new(12, 0, 0, 0, 0, 0, 0, 0) { PartyCharacter = 0 };
        Assert.True(appearance.TryCreate(actor, descriptor, original, out var neutral));
        Assert.NotEmpty(neutral);
        foreach (int clipIndex in new[] { 0, 2, 3, 4 })
        {
            PcFieldAnimation clip = original.Animations[Path.ChangeExtension(descriptor.Animations[clipIndex], ".a")];
            int final = clip.BoneCount == 23 ? clip.Frames.Count - 1 : clip.Frames.Count - 2;
            foreach (int frame in new[] { 0, final }.Distinct())
            {
                Assert.True(appearance.TryCreate(actor with { AnimationIndex = clipIndex, AnimationFrame16 = frame * 16 }, descriptor, original, out var triangles));
                Assert.NotEmpty(triangles);
                Assert.All(triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), vertex =>
                {
                    Assert.True(float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y) && float.IsFinite(vertex.Position.Z));
                    Assert.Null(vertex.Normal);
                });
            }
        }
        Assert.Throws<NotSupportedException>(() => appearance.TryCreate(actor with { AnimationIndex = 3, AnimationFrame16 = 29 * 16 }, descriptor, original, out _));
        Assert.Throws<NotSupportedException>(() => appearance.TryCreate(actor, descriptor with { Hrc = "unrecognized.hrc" }, original, out _));
    }

    private static PcFieldAnimation ControlledAnimation(Ff7FieldModelResources original, string joint)
    {
        PcFieldAnimation neutral = original.Animations["acfe.a"];
        int selected = original.Hierarchy.Bones.Single(bone => bone.Name == joint).Index;
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes);
        writer.Write(1);
        writer.Write(1);
        writer.Write(neutral.BoneCount);
        writer.Write(neutral.RotationOrder);
        writer.Write(new byte[20]);
        void V(Vector3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }
        V(neutral.Frames[0].RootRotation);
        V(neutral.Frames[0].RootTranslation);
        for (int bone = 0; bone < neutral.BoneCount; bone++)
        {
            V(neutral.Frames[0].BoneRotations[bone] + (bone == selected ? new Vector3(0, 0, 60) : Vector3.Zero));
        }
        return PcFieldAnimation.Read(bytes.ToArray());
    }
    [Theory]
    [InlineData(0f, 30, 28, true, 0f)]
    [InlineData(15f, 30, 28, true, 14f)]
    [InlineData(29.5f, 30, 28, true, 27.533333f)]
    [InlineData(29f, 30, 28, false, 27f)]
    [InlineData(0f, 1, 28, false, 0f)]
    public void NativePhaseMapsFullLoopsAndFiniteEndpoints(float phase, int sourceFrames, int targetFrames, bool loop, float expected)
    {
        Assert.Equal(expected, Ff7ModelAppearance.MapAnimationPhase(phase, sourceFrames, targetFrames, loop), 4);
        Assert.Throws<InvalidDataException>(() => Ff7ModelAppearance.MapAnimationPhase(sourceFrames, sourceFrames, targetFrames, loop));
    }

    [Fact]
    public void OptionalInstalledSquallUsesNativeWalkWithoutChangingOriginalPositionOrUnsupportedAction()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Explicit FF7 and FF8 installed resources are required for the appearance adapter check.");
        using GameAssets assets = new(ff8);
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([assets.GetFieldModelSource("bghall_1", 0)], assets.LoadFieldModelSource, TestContext.Current.CancellationToken);
        var source = catalog.GetSource("field/bghall_1/0");
        Ff7Installation installation = Ff7Installation.Discover(ff7);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(characters, field.Models.Models[0]);
        FieldModel calibrationTarget = assets.LoadFieldModelSource(source.Entry);
        Ff7AppearanceCalibration measured = Ff7ModelAppearance.CalibrateNeutralGeometry(calibrationTarget, field.Models.Models[0], original, [19, 20], 21, 22);
        Matrix4x4 calibration = measured.Transform;
        var map = new Dictionary<string, string>
        {
            ["acfe.a"] = catalog.GetAnimation(source.Entry.Id, 0).Id,
            ["aaff.a"] = catalog.GetAnimation(source.Entry.Id, 1).Id,
            ["aaga.a"] = catalog.GetAnimation(source.Entry.Id, 2).Id
        };
        Ff7ModelAppearance appearance = new(assets, catalog, source.Entry.Id, "AAAA.HRC", calibration, map);
        Ff7ActorVisual actor = new(0, 0, 100 * 4096, 200 * 4096, 300 * 4096, 0, 1, 7 * 16);
        Assert.True(appearance.TryCreate(actor, field.Models.Models[0], original, out var first));
        Assert.NotEmpty(first);
        Assert.True(appearance.TryCreate(actor with { X4096 = actor.X4096 + 4096 }, field.Models.Models[0], original, out var moved));
        Assert.Equal(first[0].A.Position.X + 1, moved[0].A.Position.X, 4);
        Assert.Equal(first[0].A.Position.Y, moved[0].A.Position.Y);
        Assert.Equal(first[0].A.Position.Z, moved[0].A.Position.Z);
        Assert.Throws<NotSupportedException>(() => appearance.TryCreate(actor with { AnimationIndex = 3 }, field.Models.Models[0], original, out _));
        Assert.Throws<InvalidDataException>(() => appearance.TryCreate(actor with { AnimationFrame16 = 30 * 16 }, field.Models.Models[0], original, out _));
        Assert.False(appearance.TryCreate(actor, field.Models.Models[0] with { Hrc = "OTHER.HRC" }, original, out _));
        FieldModel target = assets.LoadFieldModelSource(source.Entry);
        Ff7SemanticJointBinding[] diagnosticBindings =
        [
            new("hip", 2), new("chest", 1), new("head", 9),
            new("l_chest", 3), new("l_collar", 8), new("l_uparm", 13), new("l_foarm", 17), new("l_hand", 21),
            new("r_chest", 5), new("r_collar", 10), new("r_uparm", 14), new("r_foarm", 18), new("r_hand", 22),
            new("l_hip", 6), new("l_femur", 11), new("l_tibia", 15), new("l_foot", 19),
            new("r_hip", 7), new("r_femur", 12), new("r_tibia", 16), new("r_foot", 20)
        ];
        Ff7SemanticRetargeter retargeter = new(target, field.Models.Models[0], original, calibration, diagnosticBindings);
        Vector3[] neutral = retargeter.Pose(original, original.Animations["acfe.a"], 0);
        Vector3[] targetNeutral = target.Pose(0, 0);
        Assert.Equal(targetNeutral.Length, neutral.Length);
        Assert.All(neutral.Select((vertex, index) => Vector3.Distance(vertex, targetNeutral[index])), distance => Assert.True(distance < 0.01f));
        Vector3[] unique = retargeter.Pose(original, original.Animations["bvjf.a"], 40 * 16);
        Assert.All(unique, vertex => Assert.True(float.IsFinite(vertex.X) && float.IsFinite(vertex.Y) && float.IsFinite(vertex.Z)));
        Assert.Contains(unique.Select((vertex, index) => Vector3.Distance(vertex, neutral[index])), distance => distance > 0.1f);
        Ff7ModelAppearance withRetarget = new(assets, catalog, source.Entry.Id, "AAAA.HRC", calibration, map, retargeter);
        Assert.True(withRetarget.TryCreate(actor with { AnimationIndex = 3, AnimationFrame16 = 40 * 16 }, field.Models.Models[0], original, out var gesture));
        Assert.NotEmpty(gesture);
        Assert.Throws<InvalidDataException>(() => new Ff7SemanticRetargeter(target, field.Models.Models[0], original, calibration, [new("absent", 1)]));
        byte[] movedRootBytes = characters.ReadEntry(Assert.Single(characters.Find("acfe.a")));
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(movedRootBytes.AsSpan(48), System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(movedRootBytes.AsSpan(48)) + 1);
        Vector3[] translated = retargeter.Pose(original, PcFieldAnimation.Read(movedRootBytes), 0);
        Vector3 delta = Vector3.Transform(translated[0], calibration) - Vector3.Transform(neutral[0], calibration);
        Assert.Equal(Vector3.TransformNormal(Vector3.UnitX, Ff7FieldRenderer.ModelBasis(field.Models.Models[0])).X, delta.X, 3);
        Assert.Equal(0, delta.Y, 3);
        Assert.Equal(0, delta.Z, 3);
        Ff7SemanticRetargeter contacted = new(target, field.Models.Models[0], original, calibration, Ff7ModelAppearance.StandardFieldSquallJoints, Ff7ModelAppearance.StandardFieldSquallFeet);
        Matrix4x4 originalBasis = Ff7FieldRenderer.ModelBasis(field.Models.Models[0]);
        foreach (int frame in new[] { 0, 20, 40, 79 })
        {
            Vector3[] result = contacted.Pose(original, original.Animations["bvjf.a"], frame * 16);
            float targetFloor = target.Vertices.Select((vertex, index) => (vertex.Bone, Position: result[index])).Where(vertex => vertex.Bone is 19 or 20).Min(vertex => Vector3.Transform(vertex.Position, calibration).Z);
            var matrices = Ff7FieldRenderer.Pose(original, original.Animations["bvjf.a"], frame);
            HashSet<int> feet = original.Hierarchy.Bones.Where(bone => bone.Name is "l_foot" or "r_foot").Select(bone => bone.Index).ToHashSet();
            float sourceFloor = original.Geometry.Where(part => feet.Contains(part.BoneIndex)).SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, matrices[part.BoneIndex] * originalBasis).Z)).Min();
            Assert.Equal(sourceFloor, targetFloor, 3);
        }
        foreach ((string joint, int affected, int untouched) in new[] { ("l_uparm", 21, 22), ("r_uparm", 22, 21), ("l_femur", 19, 20), ("r_femur", 20, 19) })
        {
            Vector3[] controlled = retargeter.Pose(original, ControlledAnimation(original, joint), 0);
            Assert.Contains(target.Vertices.Select((vertex, index) => (vertex.Bone, Distance: Vector3.Distance(controlled[index], neutral[index]))).Where(vertex => vertex.Bone == affected), vertex => vertex.Distance > 10);
            Assert.All(target.Vertices.Select((vertex, index) => (vertex.Bone, Distance: Vector3.Distance(controlled[index], neutral[index]))).Where(vertex => vertex.Bone == untouched), vertex => Assert.True(vertex.Distance < 0.01f));
        }
        Ff7ModelAppearance standard = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        Assert.StartsWith(Ff7ModelAppearance.StandardFieldSquallProfileVersion, standard.ProfileId);
        Assert.True(standard.TryCreate(actor with { AnimationIndex = 3, AnimationFrame16 = 40 * 16 }, field.Models.Models[0], original, out _));
        string changedHierarchy = System.Text.Encoding.ASCII.GetString(characters.ReadEntry(Assert.Single(characters.Find("aaaa.hrc")))).Replace("l_hand", "x_hand", StringComparison.Ordinal);
        Ff7FieldModelResources incompatible = original with { Hierarchy = HrcHierarchy.Read(System.Text.Encoding.ASCII.GetBytes(changedHierarchy)) };
        Assert.Throws<InvalidDataException>(() => standard.TryCreate(actor, field.Models.Models[0], incompatible, out _));
        Matrix4x4 mirroredCalibration = calibration * Matrix4x4.CreateScale(-1, 1, 1);
        Ff7SemanticRetargeter mirrored = new(target, field.Models.Models[0], original, mirroredCalibration, diagnosticBindings);
        Vector3[] mirroredNeutral = mirrored.Pose(original, original.Animations["acfe.a"], 0);
        Assert.All(mirroredNeutral.Select((vertex, index) => Vector3.Distance(vertex, targetNeutral[index])), distance => Assert.True(distance < 0.01f));
    }
}
