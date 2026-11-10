using Memoria.FF7.Formats;

namespace Memoria.FF8;

/// <summary>Source-owned anatomical semantics. Composite bodies retain an unbound target knee where the original has no corresponding articulation.</summary>
internal sealed record Ff7FieldAnatomy(IReadOnlyDictionary<string, string> Joints, IReadOnlyList<string> Feet, IReadOnlyList<string> Hands)
{
    internal IReadOnlyList<string> FootGeometryOwners(Memoria.FF7.Resources.Ff7FieldModelResources original)
    {
        return Feet.Select(name =>
        {
            HrcBone bone = original.Hierarchy.Bones.Single(bone => bone.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            while (!original.Geometry.Any(part => part.BoneIndex == bone.Index && part.Polygon.Vertices.Count > 0))
            {
                bone = original.Hierarchy.Bones.SingleOrDefault(parent => parent.Name.Equals(bone.ParentName, StringComparison.OrdinalIgnoreCase)) ?? throw new NotSupportedException("Source foot has no mesh-bearing anatomical attachment owner.");
            }
            return bone.Name;
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static Ff7FieldAnatomy Resolve(HrcHierarchy hierarchy)
    {
        HashSet<string> names = hierarchy.Bones.Select(bone => bone.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] semantics = ["hip", "chest", "head", "l_chest", "l_collar", "l_uparm", "l_foarm", "l_hand", "r_chest", "r_collar", "r_uparm", "r_foarm", "r_hand", "l_hip", "l_femur", "l_tibia", "l_foot", "r_hip", "r_femur", "r_tibia", "r_foot"];
        Dictionary<string, string> joints = semantics.ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);
        bool composite = names.Contains("dchest") && names.Contains("dl_hip") && names.Contains("dr_hip");
        if (composite)
        {
            joints["hip"] = "dchest";
            foreach (string side in new[] { "l", "r" })
            {
                joints[side + "_hip"] = "d" + side + "_hip";
                joints[side + "_femur"] = "d" + side + "_femur";
                joints[side + "_foot"] = "d" + side + "_foot";
                joints.Remove(side + "_tibia");
            }
        }
        foreach (string side in new[] { "l", "r" })
        {
            string hand = side + "_hand";
            if (!names.Contains(hand) && names.Contains(hand + "1"))
            {
                joints[hand] = hand + "1";
            }
        }
        string[] missing = joints.Values.Where(name => !names.Contains(name)).ToArray();
        if (missing.Length > 0)
        {
            throw new NotSupportedException("Source anatomy has no binding for " + string.Join(", ", missing) + ".");
        }
        return new(joints, [joints["l_foot"], joints["r_foot"]], [joints["l_hand"], joints["r_hand"]]);
    }
}
