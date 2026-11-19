using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutSemanticMatrixTests
{
    [Fact]
    public void Every_installed_base_motion_preserves_semantic_global_axes_on_both_extremal_rigs()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int pairsChecked = 0;
        long framesChecked = 0;
        for (int sourceCharacter = 0; sourceCharacter <= 10; sourceCharacter++)
        {
            BattleModel source = assets.LoadPlayer(sourceCharacter);
            NativeBattlePoseSource sourceMotion = source.NativePoseSource!;
            var sourceFrames = NativeBattlePoseDecoder.DecodeFrames(sourceMotion.Animations.AsSpan(), sourceMotion.BoneCount);
            foreach (int recipientCharacter in new[] { 0, 6 })
            {
                BattleModel original = assets.LoadPlayer(recipientCharacter);
                BattleModel recipient = assets.LoadPlayerForStyle(recipientCharacter, 0, sourceCharacter);
                NativeBattlePoseSource compiled = recipient.NativePoseSource!;
                Assert.Equal(original.NativePoseSource!.Skeleton.ToArray(), compiled.Skeleton.ToArray());
                Assert.Equal(original.Name, recipient.Name);
                Assert.Equal(original.Meshes[0].Vertices.ToArray(), recipient.Meshes[0].Vertices.ToArray());
                var compiledFrames = NativeBattlePoseDecoder.DecodeFrames(compiled.Animations.AsSpan(), compiled.BoneCount);
                Assert.Equal(sourceFrames.Length, compiledFrames.Length);
                NativeBattleActionMatrix sourceOrigin = NativeBattlePoseMatrices.StageBoneMatrices(sourceMotion, sourceMotion.Sample(0, 1, 0))[0];
                NativeBattleActionMatrix recipientOrigin = NativeBattlePoseMatrices.StageBoneMatrices(original.NativePoseSource, original.NativePoseSource.Sample(0, 1, 0))[0];
                float rootCoordinateBound = compiled.ReadModelScale5027D0() / 2048f + 2;
                var joints = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, source, recipientCharacter, recipient)
                    .Where(joint => joint.Joint is "root" or "spine" or "pelvis" or "head" or "neck" or "hip-a" or "thigh-a" or "shin-a" or "foot-a" or "hip-b" or "thigh-b" or "shin-b" or "foot-b")
                    .Select(joint => (Pair: joint, Depth: Depth(source, joint.Source) + Depth(recipient, joint.Recipient) + 2)).ToArray();
                for (int animation = 0; animation < sourceFrames.Length; animation++)
                {
                    Assert.Equal(sourceFrames[animation].Length, compiledFrames[animation].Length);
                    for (int frame = 0; frame < sourceFrames[animation].Length; frame++)
                    {
                        var expected = NativeBattlePoseMatrices.StageBoneMatrices(sourceMotion, sourceFrames[animation][frame]);
                        var actual = NativeBattlePoseMatrices.StageBoneMatrices(compiled, compiledFrames[animation][frame]);
                        int deltaX = Math.Abs((actual[0].X - recipientOrigin.X) - (expected[0].X - sourceOrigin.X));
                        int deltaZ = Math.Abs((actual[0].Z - recipientOrigin.Z) - (expected[0].Z - sourceOrigin.Z));
                        if (deltaX > rootCoordinateBound || deltaZ > rootCoordinateBound)
                        {
                            Assert.Fail($"Source {sourceCharacter}, recipient {recipientCharacter}, animation {animation}, frame {frame + 1}: native world-root delta error X={deltaX}, Z={deltaZ} exceeds coordinate rounding bound {rootCoordinateBound}.");
                        }
                        foreach (var joint in joints)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                Vector3 sourceAxis = Axis(expected[joint.Pair.Source], axis);
                                Vector3 recipientAxis = Axis(actual[joint.Pair.Recipient], axis);
                                // Bound WORD-product rounding and angle-WORD quantization along both actual ancestor chains.
                                float bound = joint.Depth * (4 / sourceAxis.Length() + 4 / recipientAxis.Length() + 3 * MathF.Tau / 4096);
                                float difference = Vector3.Distance(Vector3.Normalize(sourceAxis), Vector3.Normalize(recipientAxis));
                                if (!float.IsFinite(difference) || difference > bound)
                                {
                                    Assert.Fail($"Source {sourceCharacter}, recipient {recipientCharacter}, animation {animation}, frame {frame + 1}, {joint.Pair.Joint}, source bone {joint.Pair.Source}, recipient bone {joint.Pair.Recipient}, axis {axis}: native global-axis difference {difference} exceeds accumulated quantization bound {bound}.");
                                }
                            }
                        }
                        framesChecked++;
                    }
                }
                pairsChecked++;
            }
        }
        Assert.Equal(22, pairsChecked);
        Assert.True(framesChecked > 0);
    }

    private static Vector3 Axis(NativeBattleActionMatrix matrix, int axis)
    {
        return new(matrix.Rotation[axis], matrix.Rotation[3 + axis], matrix.Rotation[6 + axis]);
    }

    private static int Depth(BattleModel model, int bone)
    {
        int depth = 0;
        for (int parent = model.Bones[bone].Parent; parent >= 0; parent = model.Bones[parent].Parent)
        {
            depth++;
        }
        return depth;
    }
}
