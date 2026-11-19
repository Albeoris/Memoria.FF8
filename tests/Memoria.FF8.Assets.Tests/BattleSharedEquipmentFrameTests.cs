using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleSharedEquipmentFrameTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(9, 1)]
    [InlineData(0, 9)]
    [InlineData(6, 9)]
    public void Shared_equipment_preserves_native_world_vectors_across_anatomical_joint_frames(int recipient, int source)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel donor = assets.LoadPlayer(source);
        BattleModel original = assets.LoadPlayer(recipient);
        int weaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == source).Id;
        BattleModel body = assets.LoadPlayerForWeapon(recipient, 0, weaponId);
        BattleModel nativeWeapon = assets.ResolvePlayerRenderWeapon(source, weaponId, donor)!;
        BattleModel transferredWeapon = assets.ResolvePlayerRenderWeapon(recipient, weaponId, body)!;
        var nativeVertices = nativeWeapon.Meshes.SelectMany(mesh => mesh.Vertices).ToArray();
        var transferredVertices = transferredWeapon.Meshes.SelectMany(mesh => mesh.Vertices).ToArray();
        Assert.Equal(nativeVertices.Length, transferredVertices.Length);

        NativeBattleDecodedPose sourcePose = new(default, Enumerable.Repeat(new NativeBattleBonePose(default, new(1024, 1024, 1024)), donor.Bones.Count).ToImmutableArray(), false);
        NativeBattleDecodedPose targetPose = BattleHumanoidRigProfiles.CreateRetargeter(source, donor, recipient, original).Transfer(sourcePose, false);
        ImmutableArray<NativeBattleActionMatrix> sourceBones = NativeBattlePoseMatrices.StageBoneMatrices(donor.NativePoseSource!, sourcePose);
        ImmutableArray<NativeBattleActionMatrix> targetBones = NativeBattlePoseMatrices.StageBoneMatrices(body.NativePoseSource!, targetPose);
        var joints = BattleHumanoidRigProfiles.SemanticPairs(source, donor, recipient, original).ToDictionary(pair => pair.Source);
        float units = donor.NativePoseSource!.ReadModelScale5027D0() / (float)body.NativePoseSource!.ReadModelScale5027D0();
        double oldWorstError = 0;
        bool differentFrame = false;
        for (int index = 0; index < nativeVertices.Length; index++)
        {
            var native = nativeVertices[index];
            var transferred = transferredVertices[index];
            NativeBattleSemanticBonePair pair = joints[native.Bone];
            differentFrame |= pair.SourceBindAxes != pair.RecipientBindAxes;
            Assert.Equal(pair.Recipient, transferred.Bone);
            NativeBattleActionMatrix sourceBone = sourceBones[native.Bone];
            NativeBattleActionMatrix targetBone = targetBones[transferred.Bone];
            NativeBattleFullMacVertex expected = NativeBattleGfMatrices.TransformMac460840(sourceBone, native.NativePosition!.Value);
            NativeBattleFullMacVertex actual = NativeBattleGfMatrices.TransformMac460840(targetBone, transferred.NativePosition!.Value);
            Assert.InRange(Math.Abs((expected.X - sourceBone.X) - (actual.X - targetBone.X)), 0, 2);
            Assert.InRange(Math.Abs((expected.Y - sourceBone.Y) - (actual.Y - targetBone.Y)), 0, 2);
            Assert.InRange(Math.Abs((expected.Z - sourceBone.Z) - (actual.Z - targetBone.Z)), 0, 2);
            NativeBattleVector point = native.NativePosition!.Value;
            NativeBattleVector oldPoint = new(checked((short)MathF.Round(point.X * units)), checked((short)MathF.Round(point.Y * units)), checked((short)MathF.Round(point.Z * units)));
            NativeBattleFullMacVertex old = NativeBattleGfMatrices.TransformMac460840(targetBone, oldPoint);
            oldWorstError = Math.Max(oldWorstError, Math.Max(Math.Abs((expected.X - sourceBone.X) - (old.X - targetBone.X)), Math.Abs((expected.Y - sourceBone.Y) - (old.Y - targetBone.Y))));
        }
        Assert.Equal(differentFrame, oldWorstError > 2);
    }
}
