using Memoria.FF7.Formats;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

/// <summary>Ordered anatomical attachment chains, independently checked against each selected native field model.</summary>
internal sealed record Ff8FieldAnatomy(int Character, string Library, string Source, int Head, int[] LeftArm, int[] RightArm, int[] LeftLeg, int[] RightLeg)
{
    internal static IReadOnlyList<Ff8FieldAnatomy> Profiles { get; } =
    [
        new(0, "d000", "field/bghall_1/0", 9, [3, 8, 13, 17, 21], [5, 10, 14, 18, 22], [6, 11, 15, 19], [7, 12, 16, 20]),
        new(1, "d009", "field/test3/4", 9, [3, 8, 13, 17, 21], [5, 10, 14, 18, 22], [6, 11, 15, 19], [7, 12, 16, 20]),
        new(2, "d015", "field/bchtl1a/5", 10, [3, 9, 17, 24, 31], [6, 14, 21, 28, 33], [7, 15, 22, 29], [8, 16, 23, 30]),
        new(3, "d018", "field/test1/2", 9, [3, 8, 13, 19, 25], [5, 10, 16, 22, 26], [6, 11, 17, 23], [7, 12, 18, 24]),
        new(4, "d024", "field/test/2", 10, [3, 9, 15, 21, 26], [5, 11, 17, 23, 28], [7, 13, 19, 24], [8, 14, 20, 25]),
        new(5, "d027", "field/test/1", 9, [3, 8, 13, 18, 23], [5, 10, 15, 20, 24], [6, 11, 16, 21], [7, 12, 17, 22]),
        new(6, "d032", "field/test3/2", 18, [4, 16, 27, 35, 39], [7, 19, 28, 36, 40], [9, 21, 29, 37], [10, 22, 30, 38]),
        new(7, "d047", "field/test7/9", 9, [3, 8, 15, 25, 29], [5, 10, 16, 26, 30], [6, 11, 17, 27], [7, 13, 21, 28]),
        new(8, "d041", "field/test3/11", 10, [3, 8, 15, 19, 23], [5, 11, 16, 20, 24], [6, 13, 17, 21], [7, 14, 18, 22]),
        new(9, "d043", "field/test5/8", 10, [3, 8, 15, 22, 27], [5, 11, 18, 24, 28], [6, 13, 20, 25], [7, 14, 21, 26]),
        new(10, "d045", "field/test7/8", 9, [3, 8, 13, 18, 25], [5, 10, 15, 22, 29], [6, 11, 16, 23], [7, 12, 17, 24])
    ];

    internal IReadOnlyList<Ff7SemanticJointBinding> Bindings(FieldModel target, HrcHierarchy source)
    {
        if (target.Bones.Count < 3 || target.Bones[0].Parent != -1 || target.Bones[1].Parent != 0 || target.Bones[2].Parent != 0 || !Descends(target, Head, 1))
        {
            throw new NotSupportedException($"Target {Library} does not retain its anatomical torso/head contract.");
        }
        List<Ff7SemanticJointBinding> bindings = [new("hip", 2), new("chest", 1), new("head", Head)];
        AddChain(LeftArm, 1, ["l_chest", "l_collar", "l_uparm", "l_foarm", "l_hand"]);
        AddChain(RightArm, 1, ["r_chest", "r_collar", "r_uparm", "r_foarm", "r_hand"]);
        AddChain(LeftLeg, 2, ["l_hip", "l_femur", "l_tibia", "l_foot"]);
        AddChain(RightLeg, 2, ["r_hip", "r_femur", "r_tibia", "r_foot"]);
        Ff7FieldAnatomy sourceAnatomy = Ff7FieldAnatomy.Resolve(source);
        bindings = bindings.Where(binding => sourceAnatomy.Joints.ContainsKey(binding.SourceJoint)).Select(binding => binding with { SourceJoint = sourceAnatomy.Joints[binding.SourceJoint] }).ToList();
        return bindings;

        void AddChain(int[] chain, int parent, string[] names)
        {
            for (int index = 0; index < chain.Length; index++)
            {
                int bone = chain[index];
                if ((uint)bone >= target.Bones.Count || target.Bones[bone].Parent != parent)
                {
                    throw new NotSupportedException($"Target {Library} anatomical attachment {bone} has an unsupported parent.");
                }
                bindings.Add(new(names[index], bone));
                parent = bone;
            }
        }
    }

    private static bool Descends(FieldModel model, int bone, int ancestor)
    {
        while ((uint)bone < model.Bones.Count)
        {
            if (bone == ancestor)
            {
                return true;
            }
            bone = model.Bones[bone].Parent;
        }
        return false;
    }
}
