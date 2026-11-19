using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAuxiliaryContactSolverTests
{
    [Fact]
    public void Absent_calibration_preserves_the_existing_compiled_motion_bytes()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleSemanticBonePair[] pairs = [new("root", 0, 0), new("body", 1, 1)];
        NativeBattlePoseSource expected = new NativeBattlePoseRetargeter(rig, rig, pairs, 1, primaryContactJoint: null).Compile(rig);
        NativeBattlePoseSource actual = new NativeBattlePoseRetargeter(rig, rig, pairs, 1, primaryContactJoint: null, auxiliaryContact: null).Compile(rig);
        Assert.Equal(expected.Animations, actual.Animations);
        Assert.Equal(expected.Skeleton, actual.Skeleton);
    }

    [Fact]
    public void Correcting_an_auxiliary_crossing_preserves_every_physical_global_and_all_scales()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleAuxiliaryContactCalibration calibration = Calibration();
        NativeBattleDecodedPose target = Pose(1024);
        target = target with { HasScale = true, Bones = target.Bones.SetItem(1, target.Bones[1] with { Scale = new(1280, 768, 1024) }) };
        NativeBattleAuxiliaryContactReport before = NativeBattleAuxiliaryContactSolver.Inspect(rig, target, calibration);
        Assert.True(before.ClothBodyCrossings > 0 || before.ViolatingLayers > 0);
        NativeBattleDecodedPose corrected = NativeBattleAuxiliaryContactSolver.Solve(rig, target, calibration);
        NativeBattleAuxiliaryContactReport after = NativeBattleAuxiliaryContactSolver.Inspect(rig, corrected, calibration);
        Assert.Equal(0, after.ClothBodyCrossings);
        Assert.Equal(0, after.ViolatingLayers);
        AssertPhysicalUnchanged(rig, target, corrected);
    }

    [Fact]
    public void Cold_solves_are_deterministic_and_independent_of_request_order()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleAuxiliaryContactCalibration calibration = Calibration();
        NativeBattleDecodedPose first = NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(1024), calibration);
        NativeBattleDecodedPose second = NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(-512), calibration);
        NativeBattleDecodedPose rest = NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(0), calibration);
        Assert.Equal(first.Bones, NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(1024), calibration).Bones);
        Assert.Equal(rest.Bones, NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(0), calibration).Bones);
        Assert.Equal(second.Bones, NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(-512), calibration).Bones);
    }

    [Fact]
    public void Auxiliary_correction_preserves_the_decoded_semantic_contact_source_frame()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleDecodedSemanticContact contact = new(new(41, -72, 93), 1, 3, 7, 12, 0);
        NativeBattleDecodedHandContact primary = new(new(-24, 18, 65), 1);
        NativeBattleDecodedPose target = Pose(1024) with { HandAContact = contact, HandContact = primary };
        NativeBattleDecodedPose solved = NativeBattleAuxiliaryContactSolver.Solve(rig, target, Calibration());
        Assert.Same(contact, solved.HandAContact);
        Assert.Equal(target.HandAContact, solved.HandAContact);
        Assert.Same(primary, solved.HandContact);
        AssertPhysicalUnchanged(rig, target, solved);
        NativeBattleAuxiliaryContactReport report = NativeBattleAuxiliaryContactSolver.Inspect(rig, solved, Calibration());
        Assert.Equal(0, report.ClothBodyCrossings);
        Assert.Equal(0, report.ViolatingLayers);
    }

    [Fact]
    public void Native_encoding_preserves_the_solved_endpoint_and_random_access()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleAuxiliaryContactCalibration calibration = Calibration();
        ImmutableArray<NativeBattleDecodedPose> solved = [NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(0), calibration), NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(1024), calibration)];
        NativeBattlePoseSource encoded = rig with { Animations = NativeBattleAnimationEncoder.Encode([solved]) };
        foreach (byte frame in new byte[] { 2, 1, 2 })
        {
            NativeBattleDecodedPose decoded = encoded.Sample(0, frame, 0);
            Assert.Equal(solved[frame - 1].Bones, decoded.Bones);
            AssertPhysicalUnchanged(rig, solved[frame - 1], decoded);
            NativeBattleAuxiliaryContactReport report = NativeBattleAuxiliaryContactSolver.Inspect(rig, decoded, calibration);
            Assert.Equal(NativeBattleAuxiliaryContactSolver.Inspect(rig, solved[frame - 1], calibration), report);
            Assert.Equal(0, report.ClothBodyCrossings);
            Assert.Equal(0, report.ViolatingLayers);
            Assert.True(float.IsFinite(report.MaximumLayerPenetration));
            Assert.True(float.IsFinite(report.SharedEdgeStrain));
        }
    }

    [Fact]
    public void Partial_source_bindings_do_not_make_other_physical_recipient_bones_auxiliary()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattlePoseSource legacy = new NativeBattlePoseRetargeter(rig, rig, [new("root", 0, 0)], 1, primaryContactJoint: null).Compile(rig);
        NativeBattlePoseSource calibrated = new NativeBattlePoseRetargeter(rig, rig, [new("root", 0, 0)], 1, primaryContactJoint: null, auxiliaryContact: Calibration()).Compile(rig);
        AssertPhysicalUnchanged(rig, legacy.Sample(0, 1, 0), calibrated.Sample(0, 1, 0));
    }

    [Fact]
    public void A_mixed_physical_auxiliary_boundary_face_is_not_dropped_from_contact_detection()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleAuxiliaryContactCalibration original = Calibration();
        NativeBattleAuxiliaryContactCalibration mixed = new(original.Vertices.SetItem(3, new(0, new(-50, -50, 120))), original.Triangles, original.PhysicalBones, original.ContactBones, original.BodySegments);
        NativeBattleDecodedPose target = Pose(1024);
        NativeBattleAuxiliaryContactReport before = NativeBattleAuxiliaryContactSolver.Inspect(rig, target, mixed);
        Assert.True(before.ClothBodyCrossings > 0 || before.ViolatingLayers > 0);
        NativeBattleDecodedPose solved = NativeBattleAuxiliaryContactSolver.Solve(rig, target, mixed);
        AssertPhysicalUnchanged(rig, target, solved);
        NativeBattleAuxiliaryContactReport after = NativeBattleAuxiliaryContactSolver.Inspect(rig, solved, mixed);
        Assert.Equal(0, after.ClothBodyCrossings);
        Assert.Equal(0, after.ViolatingLayers);
    }

    [Fact]
    public void Coincident_rest_edges_and_degenerate_faces_do_not_generate_nonfinite_reports()
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleAuxiliaryContactCalibration original = Calibration();
        NativeBattleAuxiliaryContactCalibration coincident = new(original.Vertices.Add(new(3, new(-50, -50, 20))), original.Triangles.Add(new(3, 6, 5)), original.PhysicalBones, original.ContactBones, original.BodySegments);
        NativeBattleDecodedPose solved = NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(1024), coincident);
        NativeBattleAuxiliaryContactReport report = NativeBattleAuxiliaryContactSolver.Inspect(rig, solved, coincident);
        Assert.True(float.IsFinite(report.MaximumLayerPenetration));
        Assert.True(float.IsFinite(report.SharedEdgeStrain));
        AssertPhysicalUnchanged(rig, Pose(1024), solved);
    }

    [Fact]
    public void A_material_point_can_leave_an_open_finite_panel_without_a_false_enclosure()
    {
        NativeBattlePoseSource rig = PanelRig();
        NativeBattleDecodedPose rest = Pose(0);
        NativeBattleDecodedPose rotated = rest with { Bones = rest.Bones.SetItem(1, rest.Bones[1] with { Rotation = new(0, 0, 1024) }) };
        NativeBattleAuxiliaryContactReport restReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, rest, PanelCalibration());
        NativeBattleAuxiliaryContactReport rotatedReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, rotated, PanelCalibration());
        Assert.Equal(0, restReport.ClothBodyCrossings);
        Assert.Equal(0, restReport.ViolatingLayers);
        Assert.Equal(0, rotatedReport.ClothBodyCrossings);
        Assert.Equal(0, rotatedReport.ViolatingLayers);
        Assert.Equal(0, rotatedReport.MaximumLayerPenetration);
    }

    [Fact]
    public void A_body_point_behind_its_finite_outward_panel_remains_a_layer_violation_without_triangle_crossings()
    {
        NativeBattlePoseSource rig = PanelRig();
        NativeBattleDecodedPose rest = Pose(0);
        NativeBattleDecodedPose beyond = rest with { HasScale = true, Bones = rest.Bones.SetItem(1, rest.Bones[1] with { Scale = new(3072, 1024, 1024) }) };
        NativeBattleAuxiliaryContactReport report = NativeBattleAuxiliaryContactSolver.Inspect(rig, beyond, PanelCalibration());
        Assert.Equal(0, report.ClothBodyCrossings);
        Assert.True(report.ViolatingLayers > 0);
        Assert.True(report.MaximumLayerPenetration > 0);
        Assert.Equal(0, report.HardViolatingLayers);
        Assert.Equal(report.ViolatingLayers, report.SoftViolatingLayers);
        NativeBattleDecodedPose solved = NativeBattleAuxiliaryContactSolver.Solve(rig, beyond, PanelCalibration());
        AssertPhysicalUnchanged(rig, beyond, solved);
        NativeBattleAuxiliaryContactReport solvedReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, solved, PanelCalibration());
        Assert.Equal(0, solvedReport.CorrectableCrossings);
        Assert.Equal(0, solvedReport.HardViolatingLayers);
    }

    [Fact]
    public void Leaving_a_closed_covering_panel_remains_a_hard_layer_violation_without_triangle_crossings()
    {
        NativeBattlePoseSource rig = Rig();
        byte[] skeleton = rig.Skeleton.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        rig = rig with { Skeleton = skeleton.ToImmutableArray() };
        NativeBattleAuxiliaryContactCalibration calibration = new(
            [new(1, new(10, -1, -1)), new(1, new(10, 1, -1)), new(1, new(10, 0, 1)), new(2, new(20, 20, 20)), new(2, new(20, -20, -20)), new(2, new(-20, 20, -20)), new(2, new(-20, -20, 20))],
            [new(0, 1, 2), new(3, 5, 4), new(3, 4, 6), new(3, 6, 5), new(4, 5, 6)],
            [0, 1],
            [1],
            [new(0, 1)]);
        NativeBattleDecodedPose rest = Pose(0);
        NativeBattleAuxiliaryContactReport restReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, rest, calibration);
        Assert.Equal(0, restReport.ClothBodyCrossings);
        Assert.Equal(0, restReport.ViolatingLayers);
        NativeBattleDecodedPose beyond = rest with { HasScale = true, Bones = rest.Bones.SetItem(1, rest.Bones[1] with { Scale = new(3072, 1024, 1024) }) };
        NativeBattleAuxiliaryContactReport report = NativeBattleAuxiliaryContactSolver.Inspect(rig, beyond, calibration);
        Assert.Equal(0, report.ClothBodyCrossings);
        Assert.True(report.ViolatingLayers > 0);
        Assert.Equal(report.ViolatingLayers, report.HardViolatingLayers);
        Assert.Equal(0, report.SoftViolatingLayers);
        Assert.True(report.MaximumHardLayerPenetration > 0);
    }

    [Fact]
    public void Outside_face_plane_projection_retains_the_movable_corner_derivative()
    {
        Vector3 a = Vector3.Zero;
        Vector3 b = new(100, 0, 0);
        Vector3 c = new(0, 100, 0);
        Vector3 body = new(-100, -100, -10);
        Vector3 weights = NativeBattleAuxiliaryContactSolver.PlaneProjectionWeights(body, a, b, c);
        Assert.Equal(new Vector3(3, -1, -1), weights);
        Vector3 pivot = new(0, 0, 100);
        Vector3 cornerVelocity = Vector3.Cross(Vector3.UnitX, c - pivot);
        float derivative = Vector3.Dot(weights.Z * cornerVelocity, -Vector3.UnitZ);
        Assert.Equal(100, derivative);
        double SignedPlaneDistance(double angle)
        {
            Vector3 corner = pivot + Vector3.Transform(c - pivot, Matrix4x4.CreateRotationX((float)angle));
            Vector3 normal = -Vector3.Normalize(Vector3.Cross(b - a, corner - a));
            return Vector3.Dot(a - body, normal);
        }
        const double step = 0.001;
        double centralDifference = (SignedPlaneDistance(step) - SignedPlaneDistance(-step)) / (2 * step);
        Assert.InRange(Math.Abs(centralDifference - derivative), 0, 0.01);
    }

    [Fact]
    public void A_crossing_with_a_fixed_nearest_corner_can_be_repaired_by_rotating_the_other_corner()
    {
        NativeBattlePoseSource rig = PanelRig();
        byte[] skeleton = rig.Skeleton.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 1600);
        rig = rig with { Skeleton = skeleton.ToImmutableArray() };
        NativeBattleAuxiliaryContactCalibration calibration = new(
            [new(1, new(-1600, 1600, 1760)), new(1, new(2400, -2400, 1440)), new(1, new(2560, -2240, 1440)), new(0, default), new(0, new(1600, 0, 0)), new(2, new(0, 1600, -1600))],
            [new(0, 1, 2), new(3, 4, 5)],
            [0, 1],
            [1],
            [new(0, 1)]);
        NativeBattleDecodedPose rest = Pose(0);
        NativeBattleDecodedPose target = rest with { Bones = rest.Bones.SetItem(1, rest.Bones[1] with { Rotation = new(2048, 0, 0) }) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, target);
        Vector3[] expectedPoints = [new(-100, -100, -10), new(150, 150, 10), new(160, 140, 10), Vector3.Zero, new(100, 0, 0), new(0, 100, 0)];
        Vector3 translation = new(target.Translation.X, target.Translation.Y, target.Translation.Z);
        for (int vertex = 0; vertex < calibration.Vertices.Length; vertex++)
        {
            NativeBattleAuxiliaryVertex definition = calibration.Vertices[vertex];
            NativeBattleActionMatrix matrix = matrices[definition.Bone];
            NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, definition.Position);
            Assert.Equal(expectedPoints[vertex] + translation, new Vector3(matrix.X + point.X, matrix.Y + point.Y, matrix.Z + point.Z));
        }
        NativeBattleAuxiliaryContactReport before = NativeBattleAuxiliaryContactSolver.Inspect(rig, target, calibration);
        Assert.True(before.ClothBodyCrossings > 0);
        Assert.Equal(0, before.ImmutableBoundaryCrossings);
        Assert.Equal(before.ClothBodyCrossings, before.CorrectableCrossings);
        NativeBattleDecodedPose feasible = target with { Bones = target.Bones.SetItem(2, target.Bones[2] with { Rotation = new(1024, 0, 0) }) };
        NativeBattleAuxiliaryContactReport feasibleReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, feasible, calibration);
        Assert.Equal(0, feasibleReport.ClothBodyCrossings);
        Assert.Equal(0, feasibleReport.ViolatingLayers);
        NativeBattleDecodedPose solved = NativeBattleAuxiliaryContactSolver.Solve(rig, target, calibration);
        NativeBattleAuxiliaryContactReport after = NativeBattleAuxiliaryContactSolver.Inspect(rig, solved, calibration);
        Assert.Equal(0, after.ClothBodyCrossings);
        Assert.Equal(0, after.ViolatingLayers);
        AssertPhysicalUnchanged(rig, target, solved);
    }

    [Fact]
    public void A_crossing_fixed_boundary_edge_is_reported_without_collapsing_the_mixed_face()
    {
        NativeBattlePoseSource rig = Rig();
        byte[] skeleton = rig.Skeleton.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        rig = rig with { Skeleton = skeleton.ToImmutableArray() };
        NativeBattleAuxiliaryContactCalibration calibration = new(
            [new(1, new(-100, -100, 100)), new(1, new(100, -100, 100)), new(1, new(0, 100, 100)), new(0, new(-10, 0, -10)), new(0, new(10, 0, 10)), new(2, new(0, 20, -80))],
            [new(0, 1, 2), new(3, 4, 5)],
            [0, 1],
            [1],
            [new(0, 1)]);
        NativeBattleDecodedPose rest = Pose(0);
        NativeBattleDecodedPose target = rest with { Bones = rest.Bones.SetItem(1, rest.Bones[1] with { Rotation = new(2048, 0, 0) }) };
        NativeBattleAuxiliaryContactReport restReport = NativeBattleAuxiliaryContactSolver.Inspect(rig, rest, calibration);
        Assert.Equal(0, restReport.ClothBodyCrossings);
        NativeBattleAuxiliaryContactReport before = NativeBattleAuxiliaryContactSolver.Inspect(rig, target, calibration);
        Assert.True(before.ClothBodyCrossings > 0);
        Assert.Equal(before.ClothBodyCrossings, before.ImmutableBoundaryCrossings);
        Assert.Equal(0, before.CorrectableCrossings);
        NativeBattleDecodedPose solved = NativeBattleAuxiliaryContactSolver.Solve(rig, target, calibration);
        NativeBattleAuxiliaryContactReport after = NativeBattleAuxiliaryContactSolver.Inspect(rig, solved, calibration);
        Assert.Equal(before.ClothBodyCrossings, after.ClothBodyCrossings);
        Assert.Equal(after.ClothBodyCrossings, after.ImmutableBoundaryCrossings);
        Assert.Equal(0, after.CorrectableCrossings);
        AssertPhysicalUnchanged(rig, target, solved);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, solved);
        Vector3 Point(int vertex)
        {
            NativeBattleAuxiliaryVertex definition = calibration.Vertices[vertex];
            NativeBattleActionMatrix matrix = matrices[definition.Bone];
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, definition.Position);
            return new(matrix.X + rotated.X, matrix.Y + rotated.Y, matrix.Z + rotated.Z);
        }
        Vector3 origin = new(matrices[0].X, matrices[0].Y, matrices[0].Z);
        Assert.Equal(new Vector3(-100, 100, 0), Point(0) - origin);
        Assert.Equal(new Vector3(100, 100, 0), Point(1) - origin);
        Assert.Equal(new Vector3(0, -100, 0), Point(2) - origin);
        Assert.Equal(new Vector3(-10, 0, -10), Point(3) - origin);
        Assert.Equal(new Vector3(10, 0, 10), Point(4) - origin);
        Assert.True(Vector3.Cross(Point(4) - Point(3), Point(5) - Point(3)).LengthSquared() > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Malformed_calibrations_fail_before_pose_publication(int malformed)
    {
        NativeBattlePoseSource rig = Rig();
        NativeBattleAuxiliaryContactCalibration valid = Calibration();
        Assert.Throws<InvalidDataException>(() =>
        {
            NativeBattleAuxiliaryContactCalibration invalid = malformed switch
            {
                0 => new(valid.Vertices.SetItem(0, new(4, default)), valid.Triangles, valid.PhysicalBones, valid.ContactBones, valid.BodySegments),
                1 => new(valid.Vertices, [new(0, 1, 6)], valid.PhysicalBones, valid.ContactBones, valid.BodySegments),
                2 => new(valid.Vertices, valid.Triangles, [0, 1, 1], valid.ContactBones, valid.BodySegments),
                3 => new(valid.Vertices, valid.Triangles, valid.PhysicalBones, [2], valid.BodySegments),
                _ => new(valid.Vertices, valid.Triangles, valid.PhysicalBones, valid.ContactBones, [new(0, 2)])
            };
            NativeBattleAuxiliaryContactSolver.Solve(rig, Pose(0), invalid);
        });
    }

    private static void AssertPhysicalUnchanged(NativeBattlePoseSource rig, NativeBattleDecodedPose before, NativeBattleDecodedPose after)
    {
        Assert.Equal(before.Translation, after.Translation);
        Assert.Equal(before.HasScale, after.HasScale);
        Assert.Equal(before.Bones.Select(bone => bone.Scale), after.Bones.Select(bone => bone.Scale));
        ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.StageBoneMatrices(rig, before);
        ImmutableArray<NativeBattleActionMatrix> actual = NativeBattlePoseMatrices.StageBoneMatrices(rig, after);
        foreach (int bone in new[] { 0, 1 })
        {
            Assert.Equal(expected[bone].Rotation, actual[bone].Rotation);
            Assert.Equal(expected[bone].X, actual[bone].X);
            Assert.Equal(expected[bone].Y, actual[bone].Y);
            Assert.Equal(expected[bone].Z, actual[bone].Z);
        }
    }

    private static NativeBattleAuxiliaryContactCalibration Calibration()
    {
        return new(
            [new(1, new(-100, -100, 0)), new(1, new(100, -100, 0)), new(1, new(0, 100, 0)), new(2, new(-50, -50, 20)), new(2, new(50, -50, 20)), new(3, new(0, 50, 20))],
            [new(0, 1, 2), new(3, 4, 5)],
            [0, 1],
            [1],
            [new(0, 1)]);
    }

    private static NativeBattleDecodedPose Pose(short auxiliaryRotation)
    {
        NativeBattleVector scale = new(1024, 1024, 1024);
        return new(new(17, -9, 23), [new(default, scale), new(default, scale), new(new(auxiliaryRotation, 0, 0), scale), new(default, scale)], false);
    }

    private static NativeBattleAuxiliaryContactCalibration PanelCalibration()
    {
        return new(
            [new(1, new(10, -1, -10)), new(1, new(10, 1, -10)), new(1, new(10, 0, 10)), new(2, new(20, -100, -100)), new(2, new(20, 100, -100)), new(3, new(20, 0, 100))],
            [new(0, 1, 2), new(3, 4, 5)],
            [0, 1],
            [1],
            [new(0, 1)]);
    }

    private static NativeBattlePoseSource PanelRig()
    {
        NativeBattlePoseSource rig = Rig();
        byte[] skeleton = rig.Skeleton.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 256);
        return rig with { Skeleton = skeleton.ToImmutableArray() };
    }

    private static NativeBattlePoseSource Rig()
    {
        byte[] skeleton = new byte[16 + 4 * 48];
        skeleton[0] = 4;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 64);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        short[] parents = [-1, 0, 0, 2];
        for (int bone = 0; bone < parents.Length; bone++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16 + bone * 48), parents[bone]);
        }
        return new(skeleton.ToImmutableArray(), NativeBattleAnimationEncoder.Encode([[Pose(0)]]));
    }
}
