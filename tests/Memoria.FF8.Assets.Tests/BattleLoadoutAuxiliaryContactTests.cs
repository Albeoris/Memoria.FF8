using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutAuxiliaryContactTests
{
    [Theory]
    [InlineData(7, 0, 13)]
    [InlineData(9, 29, 8)]
    [InlineData(10, 28, 7)]
    public void Ordinary_factory_compiles_the_complete_native_bank_with_recipient_contact_geometry(int owner, byte animation, byte frame)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel source = assets.LoadPlayer(owner);
        BattleModel original = assets.LoadPlayer(6);
        NativeBattlePoseSource rig = original.NativePoseSource!;
        var calibration = Assert.IsType<NativeBattleAuxiliaryContactCalibration>(BattleAuxiliaryContactCalibrations.Create(6, original));
        BattleModel compiled = assets.LoadPlayerForStyle(6, 0, owner);
        NativeBattlePoseSource bank = Assert.IsType<NativeBattlePoseSource>(compiled.NativePoseSource);
        bank.Validate();
        Assert.Equal(rig.Skeleton.ToArray(), bank.Skeleton.ToArray());
        Assert.Equal(original.Name, compiled.Name);
        Assert.Equal(original.Scale, compiled.Scale);
        Assert.Equal(original.Bones.ToArray(), compiled.Bones.ToArray());
        Assert.Equal(original.Meshes[0].Vertices.ToArray(), compiled.Meshes[0].Vertices.ToArray());
        Assert.Equal(NativeBattlePoseDecoder.ReadFrameCounts(source.NativePoseSource!.Animations.AsSpan()).ToArray(), NativeBattlePoseDecoder.ReadFrameCounts(bank.Animations.AsSpan()).ToArray());
        NativeBattleDecodedPose expected = Uncorrected(source, original, owner).Transfer(source.NativePoseSource.Sample(animation, frame, 0));
        NativeBattleDecodedPose actual = bank.Sample(animation, frame, 0);
        var expectedMatrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, expected);
        var actualMatrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, actual);
        Assert.Equal(expected.Translation, actual.Translation);
        Assert.Equal(expected.HasScale, actual.HasScale);
        Assert.Equal(expected.Bones.Select(bone => bone.Scale).ToArray(), actual.Bones.Select(bone => bone.Scale).ToArray());
        foreach (int bone in calibration.PhysicalBones)
        {
            Assert.Equal(expected.Bones[bone].Scale, actual.Bones[bone].Scale);
            Assert.Equal(expectedMatrices[bone].Rotation.ToArray(), actualMatrices[bone].Rotation.ToArray());
            Assert.Equal((expectedMatrices[bone].X, expectedMatrices[bone].Y, expectedMatrices[bone].Z), (actualMatrices[bone].X, actualMatrices[bone].Y, actualMatrices[bone].Z));
        }
        // Inspect against the original recipient's own rest bank, not the imported bank's first pose.
        var originalReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, expected, calibration);
        var report = NativeBattleAuxiliaryContactSolver.Inspect(rig, actual, calibration);
        AssertNonregression(originalReport, report);
        AssertNoCollapsedCorrection(calibration, expectedMatrices, actualMatrices);
    }

    [Theory]
    [InlineData(7, 0, 13)]
    [InlineData(7, 6, 4)]
    [InlineData(7, 14, 15)]
    [InlineData(9, 14, 11)]
    [InlineData(9, 29, 8)]
    [InlineData(10, 28, 7)]
    [InlineData(10, 12, 11)]
    [InlineData(10, 12, 16)]
    public void Cold_original_motion_preserves_physical_joints_and_does_not_regress_the_bounded_objective(int owner, byte animation, byte frame)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel source = assets.LoadPlayer(owner);
        BattleModel recipient = assets.LoadPlayer(6);
        NativeBattlePoseSource rig = recipient.NativePoseSource!;
        NativeBattleAuxiliaryContactCalibration calibration = Assert.IsType<NativeBattleAuxiliaryContactCalibration>(BattleAuxiliaryContactCalibrations.Create(6, recipient));
        NativeBattleDecodedPose sourcePose = source.NativePoseSource!.Sample(animation, frame, 0);
        NativeBattleDecodedPose original = Uncorrected(source, recipient, owner).Transfer(sourcePose);
        var originalReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, original, calibration);
        Assert.True(originalReport.ClothBodyCrossings > 0 || originalReport.ViolatingLayers > 0);
        NativeBattleDecodedPose corrected = BattleHumanoidRigProfiles.CreateRetargeter(owner, source, 6, recipient).Transfer(sourcePose);
        var before = NativeBattlePoseMatrices.StageBoneMatrices(rig, original);
        var after = NativeBattlePoseMatrices.StageBoneMatrices(rig, corrected);
        Assert.Equal(original.Translation, corrected.Translation);
        Assert.Equal(original.HasScale, corrected.HasScale);
        Assert.Equal(original.Bones.Select(bone => bone.Scale).ToArray(), corrected.Bones.Select(bone => bone.Scale).ToArray());
        foreach (int bone in calibration.PhysicalBones)
        {
            Assert.Equal(original.Bones[bone], corrected.Bones[bone]);
            Assert.Equal(before[bone].Rotation.ToArray(), after[bone].Rotation.ToArray());
            Assert.Equal((before[bone].X, before[bone].Y, before[bone].Z), (after[bone].X, after[bone].Y, after[bone].Z));
        }
        var report = NativeBattleAuxiliaryContactSolver.Inspect(rig, corrected, calibration);
        AssertNonregression(originalReport, report);
        AssertNoCollapsedCorrection(calibration, before, after);
        ImmutableArray<byte> encoded = NativeBattleAnimationEncoder.Encode([ImmutableArray.Create(corrected)]);
        NativeBattlePoseSource roundTrip = rig with { Animations = encoded };
        roundTrip.Validate();
        NativeBattleDecodedPose decoded = roundTrip.Sample(0, 1, 0);
        Assert.Equal(corrected.Translation, decoded.Translation);
        Assert.Equal(corrected.HasScale, decoded.HasScale);
        var decodedMatrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, decoded);
        for (int bone = 0; bone < corrected.Bones.Length; bone++)
        {
            Assert.Equal(corrected.Bones[bone].Scale, decoded.Bones[bone].Scale);
            Assert.Equal(after[bone].Rotation.ToArray(), decodedMatrices[bone].Rotation.ToArray());
            Assert.Equal((after[bone].X, after[bone].Y, after[bone].Z), (decodedMatrices[bone].X, decodedMatrices[bone].Y, decodedMatrices[bone].Z));
        }
        var encodedReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, decoded, calibration);
        AssertValidReport(encodedReport);
        Assert.Equal(report.ClothBodyCrossings, encodedReport.ClothBodyCrossings);
        Assert.Equal(report.ImmutableBoundaryCrossings, encodedReport.ImmutableBoundaryCrossings);
        Assert.Equal(report.ViolatingLayers, encodedReport.ViolatingLayers);
        Assert.Equal(report.HardViolatingLayers, encodedReport.HardViolatingLayers);
    }

    private static void AssertNonregression(NativeBattleAuxiliaryContactReport original, NativeBattleAuxiliaryContactReport corrected)
    {
        AssertValidReport(original);
        AssertValidReport(corrected);
        var before = (original.HardResidualSquared, original.CorrectableCrossings, original.CanonicalRestEnergy, original.SoftResidualSquared, original.SharedEdgeStrain);
        var after = (corrected.HardResidualSquared, corrected.CorrectableCrossings, corrected.CanonicalRestEnergy, corrected.SoftResidualSquared, corrected.SharedEdgeStrain);
        Assert.True(after.CompareTo(before) <= 0, $"Bounded auxiliary objective regressed from {before} to {after}.");
    }

    private static void AssertValidReport(NativeBattleAuxiliaryContactReport report)
    {
        Assert.InRange(report.ImmutableBoundaryCrossings, 0, report.ClothBodyCrossings);
        Assert.InRange(report.HardViolatingLayers, 0, report.ViolatingLayers);
        Assert.True(double.IsFinite(report.HardResidualSquared) && report.HardResidualSquared >= 0);
        Assert.True(double.IsFinite(report.CanonicalRestEnergy) && report.CanonicalRestEnergy >= 0);
        Assert.True(double.IsFinite(report.SoftResidualSquared) && report.SoftResidualSquared >= 0);
        Assert.True(float.IsFinite(report.MaximumLayerPenetration) && report.MaximumLayerPenetration >= 0);
        Assert.True(float.IsFinite(report.MaximumHardLayerPenetration) && report.MaximumHardLayerPenetration >= 0);
        Assert.True(float.IsFinite(report.SharedEdgeStrain) && report.SharedEdgeStrain >= 0);
    }

    private static void AssertNoCollapsedCorrection(NativeBattleAuxiliaryContactCalibration calibration, ImmutableArray<NativeBattleActionMatrix> before, ImmutableArray<NativeBattleActionMatrix> after)
    {
        Vector3[] Points(ImmutableArray<NativeBattleActionMatrix> matrices) => calibration.Vertices.Select(vertex =>
        {
            NativeBattleActionMatrix matrix = matrices[vertex.Bone];
            NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, vertex.Position);
            return new Vector3(matrix.X + point.X, matrix.Y + point.Y, matrix.Z + point.Z);
        }).ToArray();
        Vector3[] original = Points(before);
        Vector3[] corrected = Points(after);
        foreach (NativeBattleAuxiliaryTriangle face in calibration.Triangles)
        {
            float originalArea = Vector3.Cross(original[face.B] - original[face.A], original[face.C] - original[face.A]).LengthSquared();
            float correctedArea = Vector3.Cross(corrected[face.B] - corrected[face.A], corrected[face.C] - corrected[face.A]).LengthSquared();
            Assert.True(float.IsFinite(correctedArea));
            if (originalArea > 0)
            {
                Assert.True(correctedArea > 0, $"Auxiliary correction collapsed native triangle {face}.");
            }
        }
    }

    [Fact]
    public void Calibration_uses_full_recipient_anatomy_and_selected_native_geometry()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel recipient = assets.LoadPlayer(6);
        var calibration = Assert.IsType<NativeBattleAuxiliaryContactCalibration>(BattleAuxiliaryContactCalibrations.Create(6, recipient));
        int[] physical = BattleHumanoidRigProfiles.SemanticPairs(6, recipient, 6, recipient).Select(pair => pair.Recipient).Order().ToArray();
        Assert.Equal(physical, calibration.PhysicalBones.ToArray());
        Assert.Equal(23, physical.Length);
        Assert.Equal(8, calibration.ContactBones.Length);
        Assert.All(calibration.ContactBones, bone => Assert.Contains(bone, physical));
        Assert.Equal(8, calibration.BodySegments.Length);
        NativeBattleRawMesh mesh = recipient.NativeRenderGeometry is { IsDefaultOrEmpty: false } geometry ? geometry[0] : NativeBattleGeometryMetadata.FromMeshes([recipient.Meshes[0]])[0];
        Assert.Equal(mesh.Vertices.Length, calibration.Vertices.Length);
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            Assert.Equal(mesh.Vertices[index].Position, calibration.Vertices[index].Position);
            Assert.Equal(mesh.Vertices[index].Bone, calibration.Vertices[index].Bone);
        }
        Assert.Same(calibration, BattleAuxiliaryContactCalibrations.Create(6, recipient));
        Assert.Same(recipient, assets.LoadPlayerForStyle(6, 0, 6));
    }

    private static NativeBattlePoseRetargeter Uncorrected(BattleModel source, BattleModel recipient, int owner)
    {
        var pairs = BattleHumanoidRigProfiles.SemanticPairs(owner, source, 6, recipient);
        float Leg(BattleModel model, bool sourceBones)
        {
            int thigh = sourceBones ? pairs.Single(pair => pair.Joint == "thigh-a").Source : pairs.Single(pair => pair.Joint == "thigh-a").Recipient;
            int shin = sourceBones ? pairs.Single(pair => pair.Joint == "shin-a").Source : pairs.Single(pair => pair.Joint == "shin-a").Recipient;
            return MathF.Abs(model.Bones[thigh].Length) + MathF.Abs(model.Bones[shin].Length);
        }
        var sourceFeet = pairs.Where(pair => pair.Joint is "foot-a" or "foot-b").Select(pair => pair.Source).ToHashSet();
        var recipientFeet = pairs.Where(pair => pair.Joint is "foot-a" or "foot-b").Select(pair => pair.Recipient).ToHashSet();
        ImmutableArray<NativeBattleContactVertex> Vertices(BattleModel model, HashSet<int> feet) => model.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => feet.Contains(vertex.Bone)).Select(vertex => new NativeBattleContactVertex(vertex.Bone, vertex.NativePosition!.Value)).ToImmutableArray();
        float units = source.NativePoseSource!.ReadModelScale5027D0() / (float)recipient.NativePoseSource!.ReadModelScale5027D0();
        return new(source.NativePoseSource, recipient.NativePoseSource, pairs, Leg(recipient, false) / Leg(source, true), new(Vertices(source, sourceFeet), Vertices(recipient, recipientFeet)), horizontalMotionScale: units);
    }
}
