using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BoneGeometry(int BoneIndex, string DescriptorName, RsdDescriptor Descriptor, string PolygonName, PcPolygonModel Polygon, IReadOnlyList<PcTexture> Textures);
public sealed record Ff7FieldModelResources(string HrcName, HrcHierarchy Hierarchy, IReadOnlyList<Ff7BoneGeometry> Geometry, IReadOnlyDictionary<string, PcFieldAnimation> Animations)
{
    public static Ff7FieldModelResources Load(LgpArchive characters, PcFieldModelDescriptor model)
    {
        string hrcName = Path.GetFileNameWithoutExtension(model.Hrc) + ".hrc";
        HrcHierarchy hierarchy = HrcHierarchy.Read(Read(characters, hrcName));
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
        Dictionary<string, PcFieldAnimation> animations = new(StringComparer.OrdinalIgnoreCase);
        foreach (string descriptor in model.Animations)
        {
            string name = Path.GetFileNameWithoutExtension(descriptor) + ".a";
            PcFieldAnimation animation = PcFieldAnimation.Read(Read(characters, name));
            if (animation.BoneCount != hierarchy.DeclaredBoneCount)
            {
                throw new InvalidDataException($"FF7 animation '{name}' does not match HRC '{hrcName}' bone count.");
            }
            animations.TryAdd(name, animation);
        }
        return new Ff7FieldModelResources(hrcName, hierarchy, geometry.AsReadOnly(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, PcFieldAnimation>(animations));
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
