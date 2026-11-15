using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class BattleRigidPoseResourcesTests
{
    [Fact]
    public void RootOnlyAnimationRetainsHierarchyLengthsWorldPlacementAndOneRootRotation()
    {
        BattleModelData model = Hierarchy();
        BattleClipFrame frame = new(3, 5, 7, [new(0, 0, 1024)]);
        Matrix4x4 world = Matrix4x4.CreateScale(2) * Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(100, 20, -40);
        Matrix4x4 root = Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(3, 5, 7) * world;
        Matrix4x4[] actual = new Matrix4x4[3];
        string before = JsonSerializer.Serialize(frame);
        BattleRigidPose.WriteTransforms(model, frame, world, actual);
        MatrixNear(root, actual[0]);
        MatrixNear(Matrix4x4.CreateTranslation(0, 0, 12) * root, actual[1]);
        MatrixNear(Matrix4x4.CreateTranslation(0, 0, 7) * actual[1], actual[2]);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
        Assert.Single(frame.Rotations);
    }

    [Fact]
    public void RootOnlyLocalEulerStillReceivesNativeExtraAnglesTranslationAndScale()
    {
        BattleModelData model = Hierarchy();
        BattleClipFrame frame = new(3, -5, 7, [new(256, -512, 768)]);
        BattleActorPoseTransform extra = new(30, -15, 45, 2, -3, 4, 1.5f);
        Vector3 actorPosition = new(100, 20, -40);
        const float facing = 0.3f;
        Matrix4x4 world = Matrix4x4.CreateScale(1.5f) * Matrix4x4.CreateRotationY(facing) * Matrix4x4.CreateTranslation(actorPosition);
        Matrix4x4 rootRotation = EulerDegrees(22.5f + 30, -45 - 15, 67.5f + 45);
        Matrix4x4 root = rootRotation * Matrix4x4.CreateTranslation(5, -8, 11) * world;
        Matrix4x4 local = EulerDegrees(30, -15, 45);
        Matrix4x4[] expected = new Matrix4x4[3];
        expected[0] = local * root;
        expected[1] = local * Matrix4x4.CreateTranslation(2, -3, 16) * expected[0];
        expected[2] = local * Matrix4x4.CreateTranslation(2, -3, 11) * expected[1];
        Matrix4x4[] actual = new Matrix4x4[3];
        BattleRigidPose.WriteTransforms(model, frame, world, actual, extra);
        PointNear(Vector3.Transform(Vector3.Zero, root), BattleRigidPose.BonePosition(model, frame, actorPosition, facing, 0, extra));
        for (int bone = 0; bone < actual.Length; bone++)
        {
            MatrixNear(expected[bone], actual[bone]);
            PointNear(Vector3.Transform(Vector3.Zero, expected[bone]), BattleRigidPose.BonePosition(model, frame, actorPosition, facing, bone + 1, extra));
            Vector3 localPoint = new(5, -7, 11);
            PointNear(Vector3.Transform(localPoint, expected[bone]), BattleRigidPose.BonePoint(model, frame, actorPosition, facing, bone + 1, localPoint, extra));
        }
        BattleClipFrame explicitZeroLocals = frame with { Rotations = [frame.Rotations[0], default, default, default] };
        Matrix4x4[] full = new Matrix4x4[3];
        BattleRigidPose.WriteTransforms(model, explicitZeroLocals, world, full, extra);
        for (int bone = 0; bone < full.Length; bone++)
        {
            MatrixNear(full[bone], actual[bone]);
        }
        Assert.Single(frame.Rotations);
    }

    [Fact]
    public void PositiveNativeChannelMismatchesAndWrongTransformSpansRemainErrors()
    {
        BattleModelData model = Hierarchy();
        foreach (BattleRotationWords[] rotations in new BattleRotationWords[][] { [], [default, default], [default, default, default] })
        {
            BattleClipFrame malformed = new(0, 0, 0, rotations);
            Assert.Throws<InvalidDataException>(() => BattleRigidPose.WriteTransforms(model, malformed, Matrix4x4.Identity, new Matrix4x4[3]));
        }
        BattleClipFrame rootOnly = new(0, 0, 0, [default]);
        foreach (int length in new[] { 0, 2, 4 })
        {
            Assert.Throws<InvalidDataException>(() => BattleRigidPose.WriteTransforms(model, rootOnly, Matrix4x4.Identity, new Matrix4x4[length]));
        }
    }

    [Fact]
    public void FullChannelNativeAnimationRetainsItsAuthoredLocalRotations()
    {
        BattleModelData model = new(0, [new(-1, 12, true), new(0, 7, true)], [], []);
        BattleClipFrame frame = new(3, 5, 7, [new(0, 0, 1024), new(0, 1024, 0), new(1024, 0, 0)]);
        Matrix4x4 root = Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(3, 5, 7);
        Matrix4x4 first = Matrix4x4.CreateRotationY(MathF.PI / 2) * root;
        Matrix4x4 second = Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0, 12) * first;
        Matrix4x4[] actual = new Matrix4x4[2];
        BattleRigidPose.WriteTransforms(model, frame, Matrix4x4.Identity, actual);
        MatrixNear(first, actual[0]);
        MatrixNear(second, actual[1]);
        Assert.Equal(3, frame.Rotations.Count);
    }

    [Fact]
    public void InstalledOriginalRootOnlyEnemyClipsReachTheOrdinaryBodyJointConsumerWithoutPadding()
    {
        string? rootDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(rootDirectory), "Select original FFVII resources for native root-only battle pose checks.");
        Ff7Installation installation = Ff7Installation.Discover(rootDirectory!);
        using LgpArchive battle = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        byte[] skeleton = battle.ReadEntry(Assert.Single(battle.Find("edaa")));
        byte[] animation = battle.ReadEntry(Assert.Single(battle.Find("edda")));
        byte[] originalSkeleton = skeleton.ToArray();
        byte[] originalAnimation = animation.ToArray();
        BattleModelData model = BattleModelData.Read(skeleton, animation);
        Assert.Equal(new BattleBone(-1, 0, true), Assert.Single(model.Bones));
        Assert.Equal(10, model.BodyClips.Count);
        Assert.Empty(model.WeaponClips);
        Matrix4x4[] transforms = new Matrix4x4[1];
        foreach (BattlePackedClip packed in model.BodyClips)
        {
            Assert.Equal(1u, packed.RotationCount);
            IReadOnlyList<BattleClipFrame> frames = BattleClipDecoder.Decode(packed);
            Assert.Equal((int)packed.FrameCount, frames.Count);
            foreach (BattleClipFrame frame in frames)
            {
                Assert.Single(frame.Rotations);
                BattleRigidPose.WriteTransforms(model, frame, Matrix4x4.Identity, transforms);
                MatrixNear(BattleRigidPose.FrameTransform(frame), transforms[0]);
            }
            Assert.Equal(1u, packed.RotationCount);
        }
        BattleClipFrame first = BattleClipDecoder.Decode(model.BodyClips[0])[0];
        Assert.Equal((short)0, first.X);
        Assert.Equal((short)-703, first.Y);
        Assert.Equal((short)0, first.Z);
        Assert.Equal(new BattleRotationWords(110, -417, -81), Assert.Single(first.Rotations));
        Ff7BattlePlacement placement = new(new(13, 7, -29), 512);
        Matrix4x4 world = Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(placement.Position);
        const float degreesPerWord = 360f / 4096;
        Matrix4x4 expected = EulerDegrees(110 * degreesPerWord, -417 * degreesPerWord, -81 * degreesPerWord) * Matrix4x4.CreateTranslation(0, -703, 0) * world;
        BattleRigidPose.WriteTransforms(model, first, world, transforms);
        MatrixNear(expected, transforms[0]);
        Ff7BattleAnimationResources resources = new(battle, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, [], []);
        Assert.Equal(2, resources.BoneCount("ed"));
        PointNear(Vector3.Transform(Vector3.Zero, expected), resources.BonePosition("ed", 0, 0, placement, 0));
        PointNear(Vector3.Transform(Vector3.Zero, expected), resources.BonePosition("ed", 0, 0, placement, 1));
        Vector3 point = new(31, -11, 17);
        PointNear(Vector3.Transform(point, expected), resources.BaseBonePoint("ed", 0, 0, placement, 1, point));
        Assert.True(originalSkeleton.AsSpan().SequenceEqual(skeleton));
        Assert.True(originalAnimation.AsSpan().SequenceEqual(animation));
        Assert.True(originalSkeleton.AsSpan().SequenceEqual(battle.ReadEntry(Assert.Single(battle.Find("edaa")))));
        Assert.True(originalAnimation.AsSpan().SequenceEqual(battle.ReadEntry(Assert.Single(battle.Find("edda")))));
    }

    private static BattleModelData Hierarchy()
    {
        return new(0, [new(-1, 12, true), new(0, 7, true), new(1, 3, true)], [], []);
    }

    private static Matrix4x4 EulerDegrees(float x, float y, float z)
    {
        const float radiansPerDegree = MathF.PI / 180;
        return Matrix4x4.CreateRotationZ(z * radiansPerDegree) * Matrix4x4.CreateRotationX(x * radiansPerDegree) * Matrix4x4.CreateRotationY(y * radiansPerDegree);
    }

    private static void MatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        foreach (Vector3 point in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            PointNear(Vector3.Transform(point, expected), Vector3.Transform(point, actual));
        }
    }

    private static void PointNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 0.002f, $"Native battle point differs: expected {expected}, actual {actual}.");
    }
}
