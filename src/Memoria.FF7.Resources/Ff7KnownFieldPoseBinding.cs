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

    private static bool Matches(ReadOnlySpan<byte> bytes, string expected) => Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.Ordinal);
}
