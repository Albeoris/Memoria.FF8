using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class BattleLinkedWeaponRigProfiles
{
    public static NativeBattlePoseRetargeter CreateGunbladeCompiler(BattleModel source, BattleModel recipient)
    {
        Validate(source);
        Validate(recipient);
        float sourceScale = source.NativePoseSource!.ReadModelScale5027D0();
        float targetScale = recipient.NativePoseSource!.ReadModelScale5027D0();
        if (sourceScale <= 0 || targetScale <= 0)
        {
            throw new NotSupportedException("Linked gunblade motion requires positive native skeleton scales.");
        }
        return new(source.NativePoseSource!, recipient.NativePoseSource!, [new("root", 0, 0), new("blade", 1, 1)], sourceScale / targetScale, primaryContactJoint: null, translationMode: NativeBattleRootTranslationMode.SourceAbsolute);
    }

    private static void Validate(BattleModel model)
    {
        bool standard = model.Name.StartsWith("d0w", StringComparison.OrdinalIgnoreCase) || model.Name.StartsWith("d6w", StringComparison.OrdinalIgnoreCase);
        if (!standard || model.EditableOverride is not null || model.NativePoseSource is null || model.Bones.Count != 2 || model.NativePoseSource.BoneCount != 2 || model.Bones[0].Parent != -1 || model.Bones[0].Length != 0 || model.Bones[1].Parent != 0 || model.Bones[1].Length >= 0 || !model.Meshes.Any(mesh => mesh.Vertices.Any(vertex => vertex.Bone == 1)))
        {
            throw new NotSupportedException("The selected weapon has no admitted root/blade gunblade rig contract.");
        }
    }
}
