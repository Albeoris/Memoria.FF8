using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutGimbalRegressionTests
{
    [Theory]
    [InlineData(5, 0, 31, 1)]
    [InlineData(5, 6, 31, 1)]
    [InlineData(10, 6, 0, 8)]
    public void Adjacent_installed_singular_frames_keep_the_sources_native_leg_axes(int sourceCharacter, int recipientCharacter, byte animation, byte firstFrame)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel source = assets.LoadPlayer(sourceCharacter);
        BattleModel recipient = assets.LoadPlayerForStyle(recipientCharacter, 0, sourceCharacter);
        NativeBattlePoseSource sourceMotion = source.NativePoseSource!;
        NativeBattlePoseSource compiled = recipient.NativePoseSource!;
        IReadOnlyList<NativeBattleSemanticBonePair> joints = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, source, recipientCharacter, recipient);
        foreach (byte frame in new[] { firstFrame, checked((byte)(firstFrame + 1)) })
        {
            var expected = NativeBattlePoseMatrices.StageBoneMatrices(sourceMotion, sourceMotion.Sample(animation, frame, 0));
            var actual = NativeBattlePoseMatrices.StageBoneMatrices(compiled, compiled.Sample(animation, frame, 0));
            foreach (NativeBattleSemanticBonePair joint in joints.Where(joint => joint.Joint is "hip-a" or "thigh-a" or "shin-a" or "foot-a" or "hip-b" or "thigh-b" or "shin-b" or "foot-b"))
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 sourceAxis = Axis(expected[joint.Source], axis);
                    Vector3 recipientAxis = Axis(actual[joint.Recipient], axis);
                    // Native matrix products round WORD lanes; encoded Euler components round to an angle WORD.
                    // Accumulate their resolution along both actual ancestor chains instead of allowing a pose-specific jump.
                    int depth = Depth(source, joint.Source) + Depth(recipient, joint.Recipient) + 2;
                    float bound = depth * (4 / sourceAxis.Length() + 4 / recipientAxis.Length() + 3 * MathF.Tau / 4096);
                    float difference = Vector3.Distance(Vector3.Normalize(sourceAxis), Vector3.Normalize(recipientAxis));
                    Assert.True(difference <= bound, $"Source {sourceCharacter}, recipient {recipientCharacter}, animation {animation}, frame {frame}, {joint.Joint}, axis {axis}: native axis difference {difference} exceeds accumulated quantization bound {bound}.");
                }
            }
        }
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
