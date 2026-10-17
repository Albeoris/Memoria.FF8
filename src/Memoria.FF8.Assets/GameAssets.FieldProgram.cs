using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private readonly Dictionary<int, string> fieldProgramHashes = [];
    private readonly Dictionary<int, string> fieldWalkmeshHashes = [];
    private long fieldResourceHashGeneration = -1;

    public string ReadFieldProgramHash(int fieldId)
    {
        return ReadFieldResourceHash(fieldId, ".jsm", fieldProgramHashes);
    }

    public string ReadFieldWalkmeshHash(int fieldId)
    {
        return ReadFieldResourceHash(fieldId, ".id", fieldWalkmeshHashes);
    }

    private string ReadFieldResourceHash(int fieldId, string extension, Dictionary<int, string> hashes)
    {
        long generation = resources.Generation;
        if (fieldResourceHashGeneration != generation)
        {
            fieldProgramHashes.Clear();
            fieldWalkmeshHashes.Clear();
            fieldResourceHashGeneration = generation;
        }
        if (overrides is null && hashes.TryGetValue(fieldId, out string? cached))
        {
            return cached;
        }
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        string name = fieldNames[fieldId];
        string hash;
        if (overrides is null)
        {
            using Stream stream = resources.Get(new Memoria.FF8.Resources.ResourceKey("field/" + name + "/" + name + extension)).OpenRead();
            hash = Convert.ToHexString(SHA256.HashData(stream));
        }
        else
        {
            using IFlArchive nested = OpenNested(name);
            hash = Convert.ToHexString(SHA256.HashData(Read(nested, name + extension)));
        }
        if (overrides is null)
        {
            hashes[fieldId] = hash;
        }
        return hash;
    }
}
