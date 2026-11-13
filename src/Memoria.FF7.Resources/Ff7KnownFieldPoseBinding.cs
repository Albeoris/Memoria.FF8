using System.Security.Cryptography;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

/// <summary>An explicit authenticated cross-rig motion repair, separate from native ordinal channel access.</summary>
public sealed class Ff7KnownFieldPoseBinding
{
    private const string MotionHash = "1288B388E6B2FFD008BA439655B440A37842628C6DE70A4320D8124D074FB8F6";
    private const string TargetHierarchyHash = "6F8E8CF73E22634CC1A01D2EF57C563013FA231CFB535F78A4470F2589DF5FF1";

    private Ff7KnownFieldPoseBinding(HrcHierarchy sourceHierarchy, PcFieldAnimation sourceBind, PcFieldAnimation motion, HrcHierarchy targetHierarchy, PcFieldAnimation targetBind, int[] targetToSource)
    {
        SourceHierarchy = sourceHierarchy;
        SourceBind = sourceBind;
        Motion = motion;
        TargetHierarchy = targetHierarchy;
        TargetBind = targetBind;
        TargetToSource = Array.AsReadOnly(targetToSource);
    }

    public string ProfileId => "ff7-acgd21-bzbc-to-aagb24-v1";
    public HrcHierarchy SourceHierarchy { get; }
    public PcFieldAnimation SourceBind { get; }
    public PcFieldAnimation Motion { get; }
    public HrcHierarchy TargetHierarchy { get; }
    public PcFieldAnimation TargetBind { get; }
    public IReadOnlyList<int> TargetToSource { get; }

    public static bool IsKnownMotion(ReadOnlySpan<byte> bytes) => Matches(bytes, MotionHash);

    public static bool IsTargetHierarchy(ReadOnlySpan<byte> bytes) => Matches(bytes, TargetHierarchyHash);

    public static Ff7KnownFieldPoseBinding? TryCreate(ReadOnlySpan<byte> sourceHierarchyBytes, ReadOnlySpan<byte> sourceBindBytes, ReadOnlySpan<byte> motionBytes, ReadOnlySpan<byte> targetHierarchyBytes, ReadOnlySpan<byte> targetBindBytes)
    {
        // TODO(TD-023): The authenticated original motion has fewer channels than its selected target rig.
        if (!Matches(sourceHierarchyBytes, "2C6A3319EE4B8508D7EE9A4F96928AFF6E3E7FCF03CA7E5ED293F8A3BBED20C3") || !Matches(sourceBindBytes, "6057907E474F677FD10C4D091446494AE9D96444872F2B8BA802F456F373C764") || !IsKnownMotion(motionBytes) || !IsTargetHierarchy(targetHierarchyBytes) || !Matches(targetBindBytes, "DB732B0AD3D078C75685039D545BA51555CD15EF86C5D481E3E679C76EEB5D92"))
        {
            return null;
        }
        HrcHierarchy source = HrcHierarchy.Read(sourceHierarchyBytes);
        HrcHierarchy target = HrcHierarchy.Read(targetHierarchyBytes);
        PcFieldAnimation sourceBind = PcFieldAnimation.Read(sourceBindBytes);
        PcFieldAnimation targetBind = PcFieldAnimation.Read(targetBindBytes);
        PcFieldAnimation motion = PcFieldAnimation.Read(motionBytes);
        if (source.Bones.Count != 21 || target.Bones.Count != 24 || sourceBind.BoneCount != 21 || sourceBind.Frames.Count != 1 || targetBind.BoneCount != 24 || targetBind.Frames.Count != 1 || motion.BoneCount != 21 || motion.Frames.Count != 8)
        {
            throw new InvalidDataException("Authenticated field pose binding has inconsistent rig dimensions.");
        }
        Dictionary<string, HrcBone> targetNames = target.Bones.ToDictionary(bone => bone.Name, StringComparer.OrdinalIgnoreCase);
        int[] mapping = Enumerable.Repeat(-1, target.Bones.Count).ToArray();
        foreach (HrcBone bone in source.Bones)
        {
            if (!targetNames.TryGetValue(bone.Name, out HrcBone? counterpart) || !counterpart.ParentName.Equals(bone.ParentName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Authenticated field pose binding requires complete matching body ancestry.");
            }
            mapping[counterpart.Index] = bone.Index;
        }
        string[] extras = target.Bones.Where(bone => mapping[bone.Index] < 0).Select(bone => bone.Name).ToArray();
        if (!extras.SequenceEqual(new[] { "non_1", "kami", "armer" }, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Authenticated field pose binding has unknown nonbody joints.");
        }
        return new(source, sourceBind, motion, target, targetBind, mapping);
    }

    internal static PcFieldAnimation BindIfalnaIdle(PcFieldAnimation idle, ReadOnlySpan<byte> idleBytes, HrcHierarchy target, ReadOnlySpan<byte> hierarchyBytes, LgpArchive characters)
    {
        // TODO(TD-026): The original idle has no Ifalna hair channels or matching channel order.
        if (!Matches(idleBytes, "DE37B1BA4E67D6B4CAF7F1DE1B8A9C0446D8D2DBC3893F6606260FB5E5235EFA") || !Matches(hierarchyBytes, "C1D53FB79FCF90B11B2A8B31F5169160FE6E1B38E46D5EAAB27BE7F8E9AADA82"))
        {
            return idle;
        }
        byte[] sourceBytes = characters.ReadEntry(characters.Find("cogb.hrc").Single());
        byte[] hairBytes = characters.ReadEntry(characters.Find("cqff.a").Single());
        if (!Matches(sourceBytes, "B93DE0DB740F1104AC8753B75C6E854BAF392B676F33A4729A1FA0920B0B8911") || !Matches(hairBytes, "7CE8314EB361FB7DBC3B43E6DB078994CE9739795E42C51C43E6527FC54EACE7"))
        {
            return idle;
        }
        HrcHierarchy source = HrcHierarchy.Read(sourceBytes);
        PcFieldAnimation hair = PcFieldAnimation.Read(hairBytes);
        if (idle.RotationOrder != hair.RotationOrder || source.Bones.Count != idle.BoneCount || target.Bones.Count != hair.BoneCount)
        {
            throw new InvalidDataException("The authenticated field idle and hair poses do not match their animation contracts.");
        }
        Dictionary<string, HrcBone> sourceBones = source.Bones.ToDictionary(bone => bone.Name, StringComparer.OrdinalIgnoreCase);
        System.Numerics.Vector3[] rotations = new System.Numerics.Vector3[target.Bones.Count];
        foreach (HrcBone bone in target.Bones)
        {
            if (sourceBones.TryGetValue(bone.Name, out HrcBone? counterpart) && counterpart.ParentName == bone.ParentName && counterpart.Length == bone.Length)
            {
                rotations[bone.Index] = idle.Frames[0].BoneRotations[counterpart.Index];
            }
            else if (bone.Name is "non_1" or "kami")
            {
                rotations[bone.Index] = hair.Frames[0].BoneRotations[bone.Index];
            }
            else
            {
                throw new InvalidDataException("The authenticated field idle has an unknown joint binding.");
            }
        }
        return idle.BindSingleFrame(rotations);
    }

    private static bool Matches(ReadOnlySpan<byte> bytes, string expected) => Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.Ordinal);
}
