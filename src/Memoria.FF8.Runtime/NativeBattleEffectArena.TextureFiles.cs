using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureFileSource(int Offset, int Length, NativeBattleTextureFileIdentity Identity, string Sha256);

public sealed partial record NativeBattleEffectArena
{
    private ImmutableArray<NativeBattleTextureFileSource>? initialTextureFiles;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleTextureFileSource>? TextureFiles
    {
        get => snapshot ? initialTextureFiles : CurrentTextureFiles;
        init
        {
            if (working is not null || Transaction is not null)
            {
                throw new InvalidOperationException("Texture provenance import cannot replace a working authority.");
            }
            initialTextureFiles = value;
        }
    }

    [JsonIgnore]
    internal ImmutableArray<NativeBattleTextureFileSource>? CurrentTextureFiles
    {
        get
        {
            if (snapshot)
            {
                return initialTextureFiles;
            }
            if (TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int offset))
            {
                return NativeBattleTextureFileMetadata.Project(transaction!.TextureFiles, offset, bound);
            }
            return NativeBattleTextureFileMetadata.Project(Working.TextureFiles, workingOffset, bound);
        }
    }

    public NativeBattleEffectArena RememberTextureFile(int offset, ReadOnlySpan<byte> bytes, NativeBattleTextureFileIdentity identity)
    {
        RequireWritable();
        RequireRange(offset, bytes.Length);
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.Name) || !Enum.IsDefined(identity.Domain))
        {
            throw new InvalidDataException("Native loaded texture file requires its original name and loader namespace.");
        }
        if (bytes.IsEmpty)
        {
            return this;
        }
        ImmutableArray<NativeBattleTextureFileSource> files = [new(0, bytes.Length, identity, Convert.ToHexString(SHA256.HashData(bytes)))];
        if (TryEffectiveRoute(offset, bytes.Length, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.SetTextureFiles(routedOffset, bytes.Length, files);
        }
        else
        {
            int absolute = checked(workingOffset + offset);
            Working.SetTextureFiles(NativeBattleTextureFileMetadata.Replace(Working.TextureFiles, absolute, bytes.Length, [files[0] with { Offset = absolute }]));
        }
        return this;
    }

    private void ValidateTextureFiles() => NativeBattleTextureFileMetadata.Validate(initialTextureFiles, bound);

    internal NativeBattleTextureFileSource? FindTextureFile(int offset, ReadOnlySpan<byte> bytes)
    {
        foreach (NativeBattleTextureFileSource file in CurrentTextureFiles ?? [])
        {
            if (file.Offset == offset && file.Length == bytes.Length && string.Equals(file.Sha256, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
    }

    internal NativeBattleTextureFileSource? FindContainingTextureFile(int offset, int length)
    {
        foreach (NativeBattleTextureFileSource file in CurrentTextureFiles ?? [])
        {
            if (offset < file.Offset || (long)offset + length > (long)file.Offset + file.Length)
            {
                continue;
            }
            (byte[] bytes, bool[] known) = ReadSnapshot(file.Offset, file.Length);
            if (known.All(value => value) && string.Equals(file.Sha256, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
    }
}
