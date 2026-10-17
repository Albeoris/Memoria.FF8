using System.Reflection.PortableExecutable;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

internal static class NamedTableResources
{
    public static (uint Address, int Length)? Find(ResourceKey key) => key.Path switch
    {
        "tables/field-random" => (0xB80A18, 256),
        "tables/world-random" => (0xC75D20, 256),
        "tables/encounter-weights" => (0xC75F10, 8),
        "tables/cosine" => (0xB80D9C, 512),
        "tables/vibration" => (0xB8DCB0, 412),
        "text/draw-points" => (0xB921E4, 320),
        _ => null
    };
}

public sealed partial class ClassicGameDataSource
{
    private ResourceContent? FindNamedTable(ResourceKey key)
    {
        if (NamedTableResources.Find(key) is not { } layout)
        {
            return null;
        }
        Lazy<byte[]> bytes = new(() =>
        {
            byte[] executable = ReadAuthenticatedExecutable();
            using PEReader reader = new(new MemoryStream(executable, false));
            long rva = layout.Address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + layout.Length <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), layout.Length).ToArray();
        });
        return new ResourceContent(key, "bin", 1, $"{Descriptor.Id}:{key}", () =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new MemoryStream(bytes.Value, false);
        });
    }
}
