using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleEffectModel(PcPolygonModel Mesh, IReadOnlyList<PcTexture> Textures)
{
    public static Ff7BattleEffectModel Read(LgpArchive archive, string directory, string descriptorName)
    {
        RsdDescriptor descriptor = RsdDescriptor.Read(ReadMember(archive, directory, descriptorName));
        PcPolygonModel mesh = PcPolygonModel.Read(ReadMember(archive, directory, Path.ChangeExtension(descriptor.Polygon, ".p")));
        PcTexture[] textures = descriptor.Textures.Select(texture => PcTexture.Read(ReadMember(archive, directory, Path.ChangeExtension(texture, ".tex")))).ToArray();
        if (mesh.Groups.Any(group => group.TexturesUsed != 0 && (uint)group.TextureNumber >= textures.Length))
        {
            throw new InvalidDataException("FFVII battle effect P model references a missing RSD texture binding.");
        }
        return new(mesh, Array.AsReadOnly(textures));
    }

    public static byte[] ReadMember(LgpArchive archive, string directory, string name)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        IReadOnlyList<LgpEntry> entries = archive.Find($"{directory.TrimEnd('/')}/{name}");
        if (entries.Count == 0)
        {
            entries = archive.Find(name);
        }
        if (entries.Count != 1)
        {
            throw new InvalidDataException($"FFVII battle effect member {name} is absent or ambiguous in {directory}.");
        }
        return archive.ReadEntry(entries[0]);
    }
}
