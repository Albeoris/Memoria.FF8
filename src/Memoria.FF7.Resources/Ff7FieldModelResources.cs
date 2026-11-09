using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BoneGeometry(int BoneIndex, string DescriptorName, RsdDescriptor Descriptor, string PolygonName, PcPolygonModel Polygon, IReadOnlyList<PcTexture> Textures);
public sealed record Ff7FieldModelResources(string HrcName, HrcHierarchy Hierarchy, IReadOnlyList<Ff7BoneGeometry> Geometry, IReadOnlyDictionary<string, PcFieldAnimation> Animations)
{
    public Ff7KnownFieldPoseBinding? KnownPoseBinding { get; init; }
    public Ff7FieldEyeResources? Eyes { get; init; }

    public static Ff7FieldModelResources Load(LgpArchive characters, PcFieldModelDescriptor model, Ff7PcExecutableData? executable = null, LgpArchive? nativeEyeArchive = null)
    {
        string hrcName = Path.GetFileNameWithoutExtension(model.Hrc) + ".hrc";
        byte[] hierarchyBytes = Read(characters, hrcName);
        HrcHierarchy hierarchy = HrcHierarchy.Read(hierarchyBytes);
        List<Ff7BoneGeometry> geometry = new();
        foreach (HrcBone bone in hierarchy.Bones)
        {
            foreach (string resource in bone.ResourceDescriptors)
            {
                string rsdName = Path.GetFileNameWithoutExtension(resource) + ".rsd";
                RsdDescriptor rsd = RsdDescriptor.Read(Read(characters, rsdName));
                string polygonName = Path.GetFileNameWithoutExtension(rsd.Polygon) + ".p";
                PcPolygonModel polygon = PcPolygonModel.Read(Read(characters, polygonName));
                PcTexture[] textures = rsd.Textures.Select(name => PcTexture.Read(Read(characters, Path.GetFileNameWithoutExtension(name) + ".tex"))).ToArray();
                geometry.Add(new Ff7BoneGeometry(bone.Index, rsdName, rsd, polygonName, polygon, Array.AsReadOnly(textures)));
            }
        }
        byte[]? knownMotionBytes = null;
        Dictionary<string, PcFieldAnimation> animations = new(StringComparer.OrdinalIgnoreCase);
        foreach (string descriptor in model.Animations)
        {
            string name = Path.GetFileNameWithoutExtension(descriptor) + ".a";
            byte[] animationBytes = Read(characters, name);
            PcFieldAnimation animation = PcFieldAnimation.Read(animationBytes);
            if (name.Equals("bzbc.a", StringComparison.OrdinalIgnoreCase))
            {
                knownMotionBytes = animationBytes;
            }
            animations.TryAdd(name, animation);
        }
        Ff7KnownFieldPoseBinding? poseBinding = null;
        if (knownMotionBytes is not null && Ff7KnownFieldPoseBinding.IsKnownMotion(knownMotionBytes) && Ff7KnownFieldPoseBinding.IsTargetHierarchy(hierarchyBytes))
        {
            poseBinding = Ff7KnownFieldPoseBinding.TryCreate(Read(characters, "acgd.hrc"), Read(characters, "adcb.a"), knownMotionBytes, hierarchyBytes, Read(characters, "abcd.a"));
            if (poseBinding is not null)
            {
                animations["bzbc.a"] = poseBinding.Motion;
            }
        }
        return new Ff7FieldModelResources(hrcName, hierarchy, geometry.AsReadOnly(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, PcFieldAnimation>(animations))
        {
            KnownPoseBinding = poseBinding,
            Eyes = executable is null ? null : Ff7FieldEyeResources.Load(nativeEyeArchive ?? throw new NotSupportedException("Native model eyes require the selected field texture archive."), model, hierarchy, geometry, executable.NativePresentation)
        };
    }

    private static byte[] Read(LgpArchive archive, string name)
    {
        IReadOnlyList<LgpEntry> entries = archive.Find(name);
        if (entries.Count != 1)
        {
            throw new InvalidDataException($"FF7 model resource '{name}' resolves to {entries.Count} members.");
        }
        return archive.ReadEntry(entries[0]);
    }
}
