using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutAnatomicalBasisTests
{
    [Fact]
    public void Original_head_and_toe_geometry_uses_the_same_anatomical_facing_after_calibration()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel reference = assets.LoadPlayer(1);
        for (int character = 0; character <= 10; character++)
        {
            BattleModel model = assets.LoadPlayer(character);
            var pairs = BattleHumanoidRigProfiles.SemanticPairs(1, reference, character, model);
            foreach (string name in new[] { "head", "foot-a", "foot-b" })
            {
                if (character == 7 && name != "head")
                {
                    continue;
                }
                NativeBattleSemanticBonePair pair = pairs.Single(pair => pair.Joint == name);
                Vector3 mean = Mean(model, pair.Recipient);
                Vector3 anatomical = Vector3.TransformNormal(mean, pair.RecipientBindAxes);
                if (name == "head")
                {
                    // The original facial centre plane excludes asymmetric hair and Edea's headdress.
                    Vector3[] centre = model.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == pair.Recipient).Select(vertex => vertex.NativePosition!.Value).Select(point => Vector3.TransformNormal(new(point.X, point.Y, point.Z), pair.RecipientBindAxes)).Where(point => MathF.Abs(point.Y) <= 4).ToArray();
                    Assert.NotEmpty(centre);
                    anatomical = new((float)centre.Average(point => point.X), (float)centre.Average(point => point.Y), (float)centre.Average(point => point.Z));
                }
                Assert.True(name == "head" ? anatomical.X > MathF.Abs(anatomical.Y) : -anatomical.X > MathF.Abs(anatomical.Y), $"Native body {character}, {name}: original geometry faces {anatomical} after anatomical calibration.");
            }
        }
    }

    [Fact]
    public void Original_palm_geometry_preserves_bilateral_finger_projection()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        for (int character = 0; character <= 10; character++)
        {
            BattleModel model = assets.LoadPlayer(character);
            var pairs = BattleHumanoidRigProfiles.SemanticPairs(character, model, character, model);
            foreach (string joint in new[] { "hand-a", "hand-b" })
            {
                NativeBattleSemanticBonePair pair = pairs.Single(pair => pair.Joint == joint);
                Vector3 anatomical = Vector3.TransformNormal(Mean(model, pair.Source), pair.SourceBindAxes);
                Assert.True(joint == "hand-a" ? anatomical.X < 0 : anatomical.X > 0, $"Native body {character}, {joint}: original fingers project to the wrong anatomical side {anatomical}.");
            }
        }
    }

    [Fact]
    public void Every_native_rig_preserves_anatomical_elbow_and_knee_bend_polarity()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        for (int character = 0; character <= 10; character++)
        {
            BattleModel model = assets.LoadPlayer(character);
            var pairs = BattleHumanoidRigProfiles.SemanticPairs(character, model, character, model);
            var frames = NativeBattlePoseDecoder.DecodeFrames(model.NativePoseSource!.Animations.AsSpan(), model.Bones.Count);
            foreach (var chain in new[] { (Parent: "upper-arm-a", Child: "forearm-a", Sign: 1), (Parent: "upper-arm-b", Child: "forearm-b", Sign: -1), (Parent: "thigh-a", Child: "shin-a", Sign: -1), (Parent: "thigh-b", Child: "shin-b", Sign: -1) })
            {
                NativeBattleSemanticBonePair parent = pairs.Single(pair => pair.Joint == chain.Parent);
                int child = pairs.Single(pair => pair.Joint == chain.Child).Source;
                Vector3 sum = Vector3.Zero;
                foreach (var animation in frames)
                {
                    foreach (NativeBattleDecodedPose pose in animation)
                    {
                        NativeBattleVector rotation = pose.Bones[child].Rotation;
                        float x = rotation.X * MathF.Tau / 4096;
                        float y = rotation.Y * MathF.Tau / 4096;
                        Vector3 longitudinal = new(MathF.Cos(x) * MathF.Sin(y), -MathF.Sin(x), MathF.Cos(x) * MathF.Cos(y));
                        sum += Vector3.TransformNormal(longitudinal, parent.SourceBindAxes);
                    }
                }
                Assert.True(chain.Sign * sum.X > MathF.Abs(sum.Y), $"Native body {character}, {chain.Parent}: calibrated original articulation bends {sum}, expected signed anatomical X {chain.Sign}.");
            }
        }
    }

    [Fact]
    public void Duel_original_head_front_and_limb_directions_survive_transfer_to_Kiros()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets resources = new(TestResources.GameDirectory);
        BattleModel source = assets.LoadPlayer(1);
        BattleModel recipient = assets.LoadPlayer(9);
        byte[] dat = resources.ReadNativeDuel274Resources().ReadActorDat().ToArray();
        int begin = NativeBattleDatDirectory.Section(dat, 0);
        int end = NativeBattleDatDirectory.Section(dat, 1);
        NativeBattlePoseSource motion = source.NativePoseSource! with { Animations = dat.AsSpan(begin, end - begin).ToArray().ToImmutableArray() };
        NativeBattlePoseRetargeter compiler = BattleHumanoidRigProfiles.CreateRetargeter(1, source, 9, recipient);
        NativeBattlePoseSource compiled = compiler.Compile(motion);
        Assert.Equal(motion.BoneCount, compiled.ActionBoneFrames.Length);
        var pairs = BattleHumanoidRigProfiles.SemanticPairs(1, source, 9, recipient);
        foreach (byte frame in new byte[] { 6, 7, 16, 17, 37 })
        {
            NativeBattleDecodedPose original = motion.Sample(1, frame, 0);
            NativeBattleDecodedPose transferred = compiled.Sample(1, frame, 0);
            var expected = NativeBattlePoseMatrices.StageBoneMatrices(motion, original);
            var actual = NativeBattlePoseMatrices.StageBoneMatrices(recipient.NativePoseSource!, transferred);
            NativeBattleSemanticBonePair head = pairs.Single(pair => pair.Joint == "head");
            Vector3 originalFront = Transform(expected[head.Source], Vector3.UnitX);
            Vector3 recipientFront = Transform(actual[head.Recipient], Vector3.UnitY);
            Assert.True(Vector3.Dot(Vector3.Normalize(originalFront), Vector3.Normalize(recipientFront)) > 0.999f, $"Duel pose1 frame{frame}: Kiros own face does not follow the source face direction.");
            foreach (NativeBattleSemanticBonePair pair in pairs)
            {
                Vector3 originalAxis = Transform(expected[pair.Source], Vector3.UnitZ);
                Vector3 recipientAxis = Transform(actual[pair.Recipient], Vector3.UnitZ);
                Assert.True(Vector3.Dot(Vector3.Normalize(originalAxis), Vector3.Normalize(recipientAxis)) > 0.999f, $"Duel pose1 frame{frame}, {pair.Joint}: own joint longitudinal direction changed.");
            }
            NativeBattleActionMatrix sourceAction = NativeBattleDuel274Body.ReadBoneMatrix68A790(actual, 18, compiled.ActionBoneAliases, compiled.ActionBoneFrames);
            foreach (Vector3 axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            {
                Assert.True(Vector3.Dot(Vector3.Normalize(Transform(expected[18], axis)), Vector3.Normalize(Transform(sourceAction, axis))) > 0.999f, $"Duel pose1 frame{frame}: source-authored forearm18 vector changed its action frame.");
            }
            int physical = compiled.ActionBoneAliases[18];
            Assert.Equal((actual[physical].X, actual[physical].Y, actual[physical].Z), (sourceAction.X, sourceAction.Y, sourceAction.Z));
        }
    }

    private static Vector3 Mean(BattleModel model, int bone)
    {
        NativeBattleVector[] points = model.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == bone).Select(vertex => vertex.NativePosition!.Value).ToArray();
        return new((float)points.Average(point => point.X), (float)points.Average(point => point.Y), (float)points.Average(point => point.Z));
    }

    private static Vector3 Transform(NativeBattleActionMatrix matrix, Vector3 local)
    {
        return new(matrix.Rotation[0] * local.X + matrix.Rotation[1] * local.Y + matrix.Rotation[2] * local.Z, matrix.Rotation[3] * local.X + matrix.Rotation[4] * local.Y + matrix.Rotation[5] * local.Z, matrix.Rotation[6] * local.X + matrix.Rotation[7] * local.Y + matrix.Rotation[8] * local.Z);
    }
}
