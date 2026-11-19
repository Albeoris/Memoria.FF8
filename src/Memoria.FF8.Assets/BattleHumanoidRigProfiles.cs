using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Semantic limb chains for standard party battle rigs, independently of weapon animation ownership.</summary>
public static class BattleHumanoidRigProfiles
{
    private static readonly string[] Joints = ["root", "spine", "pelvis", "head", "shoulder-a", "upper-arm-a", "forearm-a", "hand-a", "shoulder-b", "upper-arm-b", "forearm-b", "hand-b", "hip-a", "thigh-a", "shin-a", "foot-a", "hip-b", "thigh-b", "shin-b", "foot-b", "fingers-a", "fingers-b"];
    private static readonly int[][] Fingers = [[21, 22], [21, 22], [31, 33], [25, 26], [26, 28], [23, 24], [39, 40], [33, 34], [27, 28], [25, 29], [29, 30]];
    private static readonly int[][] Bones =
    [
        [0, 1, 2, 9, 3, 8, 13, 17, 5, 10, 14, 18, 6, 11, 15, 19, 7, 12, 16, 20],
        [0, 1, 2, 9, 3, 8, 13, 17, 5, 10, 14, 18, 6, 11, 15, 19, 7, 12, 16, 20],
        [0, 1, 2, 10, 3, 9, 17, 24, 6, 14, 21, 28, 7, 15, 22, 29, 8, 16, 23, 30],
        [0, 1, 2, 9, 3, 8, 13, 19, 5, 10, 16, 22, 6, 11, 17, 23, 7, 12, 18, 24],
        [0, 1, 2, 10, 3, 9, 15, 21, 5, 11, 17, 23, 7, 13, 19, 24, 8, 14, 20, 25],
        [0, 1, 2, 9, 3, 8, 13, 18, 5, 10, 15, 20, 6, 11, 16, 21, 7, 12, 17, 22],
        [0, 1, 2, 17, 4, 16, 27, 35, 7, 19, 28, 36, 9, 21, 29, 37, 10, 22, 30, 38],
        [0, 1, 2, 14, 4, 12, 21, 27, 6, 15, 22, 28, 7, 17, 23, 29, 8, 18, 24, 30],
        [0, 1, 2, 10, 3, 8, 15, 22, 5, 11, 18, 24, 6, 13, 20, 25, 7, 14, 21, 26],
        [0, 1, 2, 9, 3, 8, 13, 18, 5, 10, 15, 22, 6, 11, 16, 23, 7, 12, 17, 24],
        [0, 1, 2, 9, 3, 8, 15, 25, 5, 10, 16, 26, 6, 11, 17, 27, 7, 13, 21, 28]
    ];
    private static readonly int[] BoneCounts = [23, 23, 34, 27, 32, 25, 41, 35, 29, 35, 31];

    public static IReadOnlyList<NativeBattleSemanticBonePair> SemanticPairs(int sourceCharacter, BattleModel source, int recipientCharacter, BattleModel recipient)
    {
        Validate(sourceCharacter, source);
        Validate(recipientCharacter, recipient);
        int[] sourceBones = [.. Bones[sourceCharacter], .. Fingers[sourceCharacter]];
        int[] recipientBones = [.. Bones[recipientCharacter], .. Fingers[recipientCharacter]];
        return Joints.Select((joint, index) => new NativeBattleSemanticBonePair(joint, sourceBones[index], recipientBones[index])).ToArray();
    }

    public static NativeBattlePoseRetargeter CreateRetargeter(int sourceCharacter, BattleModel source, int recipientCharacter, BattleModel recipient)
    {
        IReadOnlyList<NativeBattleSemanticBonePair> pairs = SemanticPairs(sourceCharacter, source, recipientCharacter, recipient);
        float sourceLeg = LegLength(sourceCharacter, source);
        float recipientLeg = LegLength(recipientCharacter, recipient);
        return new(source.NativePoseSource!, recipient.NativePoseSource!, pairs, recipientLeg / sourceLeg);
    }

    private static void Validate(int character, BattleModel model)
    {
        if ((uint)character >= Bones.Length || model.EditableOverride is not null || model.Bones.Count != BoneCounts[character] || model.NativePoseSource is null || !model.Name.StartsWith($"d{character:X}c", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("This body has no admitted standard battle semantic rig profile.");
        }
        int[] map = Bones[character];
        foreach (int start in new[] { 4, 8, 12, 16 })
        {
            for (int offset = 1; offset < 4; offset++)
            {
                if (model.Bones[map[start + offset]].Parent != map[start + offset - 1])
                {
                    throw new NotSupportedException("The selected battle rig does not preserve the profile's semantic limb chain.");
                }
            }
        }
        if (model.Bones[0].Parent != -1 || model.Bones[1].Parent != 0 || model.Bones[2].Parent != 0)
        {
            throw new NotSupportedException("The selected battle rig does not preserve the profile's root, spine and pelvis ownership.");
        }
    }

    private static float LegLength(int character, BattleModel model)
    {
        int[] map = Bones[character];
        float length = MathF.Abs(model.Bones[map[13]].Length) + MathF.Abs(model.Bones[map[14]].Length);
        return length > 0 ? length : throw new InvalidDataException("The selected semantic leg chain has no calibrated length.");
    }
}
