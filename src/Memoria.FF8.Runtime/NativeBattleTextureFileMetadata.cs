using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleTextureFileMetadata
{
    internal static void Validate(ImmutableArray<NativeBattleTextureFileSource>? files, int length)
    {
        if (files is not { } values)
        {
            return;
        }
        if (values.IsDefault)
        {
            throw new InvalidDataException("Native loaded texture files require explicit retained descriptors.");
        }
        foreach (NativeBattleTextureFileSource file in values)
        {
            if (file is null || file.Offset < 0 || file.Length <= 0 || file.Length > length || file.Offset > length - file.Length || file.Identity is null || string.IsNullOrWhiteSpace(file.Identity.Name) || !Enum.IsDefined(file.Identity.Domain) || file.Sha256 is not { Length: 64 })
            {
                throw new InvalidDataException("Native loaded texture file metadata leaves its bounded source owner.");
            }
        }
    }

    internal static ImmutableArray<NativeBattleTextureFileSource>? Project(ImmutableArray<NativeBattleTextureFileSource>? files, int offset, int length)
    {
        if (files is not { } values)
        {
            return null;
        }
        if (offset == 0)
        {
            bool contained = true;
            foreach (NativeBattleTextureFileSource file in values)
            {
                contained &= file.Offset <= length - file.Length;
            }
            if (contained)
            {
                return files;
            }
        }
        ImmutableArray<NativeBattleTextureFileSource>.Builder result = ImmutableArray.CreateBuilder<NativeBattleTextureFileSource>();
        foreach (NativeBattleTextureFileSource file in values)
        {
            if (file.Offset >= offset && file.Offset - offset <= length - file.Length)
            {
                result.Add(offset == 0 ? file : file with { Offset = file.Offset - offset });
            }
        }
        return result.ToImmutable();
    }

    internal static ImmutableArray<NativeBattleTextureFileSource> Replace(ImmutableArray<NativeBattleTextureFileSource>? files, int offset, int length, ImmutableArray<NativeBattleTextureFileSource> replacements)
    {
        ImmutableArray<NativeBattleTextureFileSource>.Builder result = ImmutableArray.CreateBuilder<NativeBattleTextureFileSource>();
        foreach (NativeBattleTextureFileSource file in files ?? [])
        {
            if (file.Offset + file.Length <= offset || file.Offset >= offset + length)
            {
                result.Add(file);
            }
        }
        result.AddRange(replacements);
        return result.ToImmutable();
    }
}
