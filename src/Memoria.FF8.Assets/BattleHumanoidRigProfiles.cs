using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Semantic limb chains for standard party battle rigs, independently of weapon animation ownership.</summary>
public static class BattleHumanoidRigProfiles
{
    private static readonly string[] Joints = ["root", "spine", "pelvis", "head", "shoulder-a", "shoulder-tip-a", "upper-arm-a", "forearm-a", "shoulder-b", "shoulder-tip-b", "upper-arm-b", "forearm-b", "hip-a", "thigh-a", "shin-a", "foot-a", "hip-b", "thigh-b", "shin-b", "foot-b", "hand-a", "hand-b", "neck"];
    private static readonly int[][] Hands = [[21, 22], [21, 22], [31, 33], [25, 26], [26, 28], [23, 24], [39, 40], [33, 34], [27, 28], [25, 29], [29, 30]];
    private static readonly string[] PrimaryEquipmentContacts = ["hand-b", "hand-a", "hand-b", "hand-a", "forearm-a", "hand-b", "hand-b", "hand-a", "hand-b", "hand-a", "hand-b"];
    private static readonly int[][] Bones =
    [
        [0, 1, 2, 9, 3, 8, 13, 17, 5, 10, 14, 18, 6, 11, 15, 19, 7, 12, 16, 20],
        [0, 1, 2, 9, 3, 8, 13, 17, 5, 10, 14, 18, 6, 11, 15, 19, 7, 12, 16, 20],
        [0, 1, 2, 10, 3, 9, 17, 24, 6, 14, 21, 28, 7, 15, 22, 29, 8, 16, 23, 30],
        [0, 1, 2, 9, 3, 8, 13, 19, 5, 10, 16, 22, 6, 11, 17, 23, 7, 12, 18, 24],
        [0, 1, 2, 10, 3, 9, 15, 21, 5, 11, 17, 23, 7, 13, 19, 24, 8, 14, 20, 25],
        [0, 1, 2, 9, 3, 8, 13, 18, 5, 10, 15, 20, 6, 11, 16, 21, 7, 12, 17, 22],
        [0, 1, 2, 18, 4, 16, 27, 35, 7, 19, 28, 36, 9, 21, 29, 37, 10, 22, 30, 38],
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
        int[] sourceBones = [.. Bones[sourceCharacter], .. Hands[sourceCharacter], source.Bones[Bones[sourceCharacter][3]].Parent];
        int[] recipientBones = [.. Bones[recipientCharacter], .. Hands[recipientCharacter], recipient.Bones[Bones[recipientCharacter][3]].Parent];
        return Joints.Select((joint, index) => new NativeBattleSemanticBonePair(joint, sourceBones[index], recipientBones[index])).ToArray();
    }

    public static NativeBattlePoseRetargeter CreateRetargeter(int sourceCharacter, BattleModel source, int recipientCharacter, BattleModel recipient)
    {
        IReadOnlyList<NativeBattleSemanticBonePair> pairs = SemanticPairs(sourceCharacter, source, recipientCharacter, recipient);
        float sourceLeg = LegLength(sourceCharacter, source);
        float recipientLeg = LegLength(recipientCharacter, recipient);
        HashSet<int> sourceFeet = pairs.Where(pair => pair.Joint is "foot-a" or "foot-b").Select(pair => pair.Source).ToHashSet();
        HashSet<int> recipientFeet = pairs.Where(pair => pair.Joint is "foot-a" or "foot-b").Select(pair => pair.Recipient).ToHashSet();
        ImmutableArray<NativeBattleContactVertex> sourceVertices = source.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => sourceFeet.Contains(vertex.Bone)).Select(vertex => new NativeBattleContactVertex(vertex.Bone, vertex.NativePosition ?? throw new NotSupportedException("Foot contact requires native signed source foot coordinates."))).ToImmutableArray();
        ImmutableArray<NativeBattleContactVertex> recipientVertices = recipient.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => recipientFeet.Contains(vertex.Bone)).Select(vertex => new NativeBattleContactVertex(vertex.Bone, vertex.NativePosition ?? throw new NotSupportedException("Foot contact requires native signed recipient foot coordinates."))).ToImmutableArray();
        NativeBattleTwoHandContactCalibration? hands = null;
        if (sourceCharacter is 2 or 8)
        {
            NativeBattleSemanticBonePair Joint(string name) => pairs.Single(pair => pair.Joint == name);
            hands = new(Joint("hand-b").Source, Joint("hand-a").Source, Joint("hand-b").Recipient, Joint("upper-arm-a").Recipient, Joint("forearm-a").Recipient, Joint("hand-a").Recipient, 4);
        }
        return new(source.NativePoseSource!, recipient.NativePoseSource!, pairs, recipientLeg / sourceLeg, new(sourceVertices, recipientVertices), hands, PrimaryEquipmentContacts[sourceCharacter]);
    }

    private static void Validate(int character, BattleModel model)
    {
        if ((uint)character >= Bones.Length || model.EditableOverride is not null || model.Bones.Count != BoneCounts[character] || model.NativePoseSource is null || !model.Name.StartsWith($"d{character:X}c", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("This body has no admitted standard battle semantic rig profile.");
        }
        int[] map = Bones[character];
        if (model.NativePoseSource.BoneCount != model.Bones.Count || (model.NativePoseSource.Skeleton[4] & 0x7F) != map[3])
        {
            throw new NotSupportedException("The selected battle rig does not preserve the profile's native head anchor and skeleton ownership.");
        }
        int neck = model.Bones[map[3]].Parent;
        if (neck < 0 || model.Bones[neck].Parent != map[1] || model.Bones[map[4]].Parent != map[1] || model.Bones[map[8]].Parent != map[1] || model.Bones[map[12]].Parent != map[2] || model.Bones[map[16]].Parent != map[2])
        {
            throw new NotSupportedException("The selected battle rig does not preserve its neck, shoulder and hip ownership by the fused spine/chest and pelvis.");
        }
        if (model.Bones[Hands[character][0]].Parent != map[7] || model.Bones[Hands[character][1]].Parent != map[11])
        {
            throw new NotSupportedException("The selected battle rig does not preserve its hand ownership by the admitted forearms.");
        }
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
