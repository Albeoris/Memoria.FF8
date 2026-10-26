using System.Buffers.Binary;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    /// <summary>Validates a standalone party body against its installed DAT section contract using the current model decoder.</summary>
    public static void ValidateBattleModelResource(ResourceContent candidate, ResourceContent original)
    {
        string[] parts = candidate.Key.Path.Split('/');
        string name = parts[^1];
        if (parts.Length != 2 || parts[0] != "battle" || name.Length != 10 || name[0] != 'd' || !"0123456789abcdef".Contains(name[1]) || name[2] != 'c' || name.AsSpan(3, 3).ContainsAnyExceptInRange('0', '9') || name[6..] != ".dat" || original.Key != candidate.Key)
        {
            throw new ArgumentException("A battle body requires matching canonical player DAT resource identities.", nameof(candidate));
        }
        byte[] replacement = Read(candidate);
        byte[] installed = Read(original);
        _ = NativeBattleDatDirectory.Section(replacement, 0);
        _ = NativeBattleDatDirectory.Section(installed, 0);
        if (BinaryPrimitives.ReadInt32LittleEndian(replacement) != BinaryPrimitives.ReadInt32LittleEndian(installed))
        {
            throw new NotSupportedException("A battle body replacement must preserve the installed native DAT section layout.");
        }
        _ = BattleResourceReader.ReadModel(name, replacement);

        static byte[] Read(ResourceContent content)
        {
            using Stream input = content.OpenRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
