using System.Globalization;
using System.Text;

namespace Memoria.FF7.Formats;

public sealed record HrcBone(int Index, string Name, string ParentName, float Length, IReadOnlyList<string> ResourceDescriptors);

public sealed class HrcHierarchy
{
    private HrcHierarchy(string name, HrcBone[] bones)
    {
        Name = name;
        Bones = Array.AsReadOnly(bones);
    }

    public string Name { get; }
    public IReadOnlyList<HrcBone> Bones { get; }

    public static HrcHierarchy Read(ReadOnlySpan<byte> source, int maximumBones = 4096, int maximumBytes = 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBones);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (source.Length > maximumBytes || source.ContainsAnyInRange((byte)128, byte.MaxValue))
        {
            throw new InvalidDataException("HRC text exceeds its limit or contains non-ASCII bytes.");
        }
        string[] lines = Encoding.ASCII.GetString(source).Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3 || lines[0] != ":HEADER_BLOCK 2" || !lines[1].StartsWith(":SKELETON ", StringComparison.Ordinal) || !lines[2].StartsWith(":BONES ", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported HRC header directives.");
        }
        if (!int.TryParse(lines[2].AsSpan(7), NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0 || count > maximumBones || lines.Length != 3L + 4L * count)
        {
            throw new InvalidDataException("HRC bone count exceeds its limit or does not match its records.");
        }
        HrcBone[] bones = new HrcBone[count];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase) { "root" };
        for (int i = 0; i < count; i++)
        {
            int offset = 3 + i * 4;
            string name = lines[offset];
            string parent = lines[offset + 1];
            if (!names.Contains(parent) || !names.Add(name))
            {
                throw new InvalidDataException($"HRC bone {i} has a duplicate name or a parent not defined earlier.");
            }
            if (!float.TryParse(lines[offset + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float length) || !float.IsFinite(length) || length < 0)
            {
                throw new InvalidDataException($"HRC bone {i} has an invalid length.");
            }
            string[] descriptors = lines[offset + 3].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (!int.TryParse(descriptors[0], NumberStyles.None, CultureInfo.InvariantCulture, out int descriptorCount) || descriptorCount != descriptors.Length - 1)
            {
                throw new InvalidDataException($"HRC bone {i} resource descriptor count is invalid.");
            }
            bones[i] = new HrcBone(i, name, parent, length, Array.AsReadOnly(descriptors[1..]));
        }
        return new HrcHierarchy(lines[1][10..], bones);
    }
}
