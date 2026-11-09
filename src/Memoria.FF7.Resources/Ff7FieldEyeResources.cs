using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7FieldEyeResources(int Role, int HeadPart, int FirstGroup, PcTexture? ClosedLeft, PcTexture? ClosedRight)
{
    public static PcRenderState AlternateMaterial { get; } = new(0x2820E, 0x20002, 1, uint.MaxValue, 2, 1, 0, 4, 0, 255, ReadOnlyMemory<byte>.Empty);

    public static Ff7FieldEyeResources Load(LgpArchive characters, PcFieldModelDescriptor descriptor, HrcHierarchy hierarchy, IReadOnlyList<Ff7BoneGeometry> geometry, Ff7PcFieldPresentationData native)
    {
        int head = hierarchy.Bones.FirstOrDefault(bone => bone.Name.Equals("head", StringComparison.OrdinalIgnoreCase))?.Index ?? -1;
        int partIndex = Enumerable.Range(0, geometry.Count).FirstOrDefault(index => geometry[index].BoneIndex == head, -1);
        int firstGroup = partIndex < 0 ? -1 : Enumerable.Range(0, geometry[partIndex].Polygon.Groups.Count).FirstOrDefault(index => geometry[partIndex].Polygon.Groups[index].TexturesUsed != 0, -1);
        Ff7NativeEyeProfile profile = native.Eyes(descriptor.Hrc);
        PcTexture? Texture(string? name)
        {
            if (!profile.Enabled || name is null)
            {
                return null;
            }
            string resource = Path.ChangeExtension(name, ".tex");
            IReadOnlyList<LgpEntry> entries = characters.Find(resource);
            if (entries.Count > 1)
            {
                throw new InvalidDataException("A native alternate-eye resource has ambiguous archive ownership.");
            }
            return entries.Count == 0 ? null : PcTexture.Read(characters.ReadEntry(entries[0]));
        }
        return new(native.Role(descriptor.Hrc), partIndex, firstGroup, Texture(profile.ClosedLeft), Texture(profile.ClosedRight));
    }

    public PcTexture? Alternate(int part, int group, byte left, byte right)
    {
        if (part != HeadPart || FirstGroup < 0 || left is not (1 or 2))
        {
            return null;
        }
        // Missing left replacement returns from the native setter before it visits the second group.
        if (left == 2 && ClosedLeft is null)
        {
            return null;
        }
        if (group == FirstGroup)
        {
            return left == 2 ? ClosedLeft : null;
        }
        return group == FirstGroup + 1 && right == 2 ? ClosedRight : null;
    }
}
