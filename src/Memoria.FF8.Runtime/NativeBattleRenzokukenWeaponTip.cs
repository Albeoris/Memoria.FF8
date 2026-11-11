using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken160Controller
{
    public static NativeBattleRenzokuken160WeaponTip ReadWeaponTip5FFEE0(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource)
    {
        NativeBattlePoseSource linked = resource.LinkedPoseSource ?? throw new NotSupportedException("Native5FFEE0 requires its actual linked weapon skeleton.");
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native5FFEE0 requires its current linked pose owner.");
        NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linked);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(linked, pose);
        if (bones.Length <= 1)
        {
            throw new InvalidDataException("Native5FFF60 linked weapon lacks reached bone1.");
        }
        NativeBattleActionMatrix bone = bones[1];
        var first = NativeBattleGfMatrices.RotateLong56C600(bone, 0, 0, -4096);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(first.X, first.Y, first.Z);
        NativeBattleLinkedModelTransform local = model.LinkedTransform ?? throw new NotSupportedException("Native5FFEE0 requires its actual linked local transform.");
        NativeBattleActionMatrix localMatrix = NativeBattleGfMatrices.Euler56CD50(local.Rotation) with { X = local.Position.X, Y = local.Position.Y, Z = local.Position.Z };
        NativeBattleActionMatrix parent = NativeBattlePoseMatrices.Compose56C2F0(model.GeometryWorldMatrix ?? throw new NotSupportedException("Native5FFEE0 requires its current model matrix64."), localMatrix);
        var rotated = NativeBattleGfMatrices.RotateLong56C600(parent, direction.X, direction.Y, direction.Z);
        direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(rotated.X, rotated.Y, rotated.Z);
        NativeBattleVector position = NativeBattleGfMatrices.RotateShort56C4F0(parent, new(unchecked((short)bone.X), unchecked((short)bone.Y), unchecked((short)bone.Z)));
        position = new(unchecked((short)(position.X + parent.X)), unchecked((short)(position.Y + parent.Y)), unchecked((short)(position.Z + parent.Z)));
        return new(position, direction.X, direction.Y, direction.Z, null, null);
    }
}
