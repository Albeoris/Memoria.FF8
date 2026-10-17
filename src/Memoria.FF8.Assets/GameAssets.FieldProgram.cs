using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private readonly Dictionary<int, string> fieldProgramHashes = [];
    private long fieldProgramGeneration = -1;

    public string ReadFieldProgramHash(int fieldId)
    {
        long generation = resources?.Generation ?? 0;
        if (fieldProgramGeneration != generation)
        {
            fieldProgramHashes.Clear();
            fieldProgramGeneration = generation;
        }
        if (overrides is null && fieldProgramHashes.TryGetValue(fieldId, out string? cached))
        {
            return cached;
        }
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        string name = fieldNames[fieldId];
        using IFlArchive nested = OpenNested(name);
        string hash = Convert.ToHexString(SHA256.HashData(Read(nested, name + ".jsm")));
        fieldProgramHashes[fieldId] = hash;
        return hash;
    }
}
