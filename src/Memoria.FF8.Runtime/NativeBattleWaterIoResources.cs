using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated external files, separate from the current runtime destination and completion owners.</summary>
public sealed record NativeBattleWaterIoResources([property: JsonRequired] ImmutableArray<NativeBattleArchiveFile> Files)
{
    public void Validate()
    {
        if (Files.IsDefault || Files.Length != 3)
        {
            throw new InvalidDataException("Native Water external reads require their three complete original files.");
        }
        for (int index = 0; index < Files.Length; index++)
        {
            NativeBattleArchiveFile file = Files[index];
            if (file is null || file.FileId != 363 + index || !string.Equals(file.Name, $"ma8def_p.{index}", StringComparison.OrdinalIgnoreCase) || file.Bytes.IsDefault || file.Bytes.Length != 0x8000)
            {
                throw new InvalidDataException("Native Water external file identity or complete extent contradicts its actual profile.");
            }
        }
    }
}
