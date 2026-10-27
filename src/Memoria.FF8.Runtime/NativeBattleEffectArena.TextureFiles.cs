using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureFileSource(int Offset, int Length, NativeBattleTextureFileIdentity Identity, string Sha256);

public sealed partial record NativeBattleEffectArena
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleTextureFileSource>? TextureFiles { get; init; }
    [JsonIgnore]
    internal ImmutableArray<NativeBattleTextureFileSource>? CurrentTextureFiles => Transaction is { } transaction ? transaction.TextureFiles : TextureFiles;

    public NativeBattleEffectArena RememberTextureFile(int offset, ReadOnlySpan<byte> bytes, NativeBattleTextureFileIdentity identity)
    {
        RequireRange(offset, bytes.Length);
        if (string.IsNullOrWhiteSpace(identity.Name) || !Enum.IsDefined(identity.Domain))
        {
            throw new InvalidDataException("Native loaded texture file requires its original name and loader namespace.");
        }
        if (bytes.IsEmpty)
        {
            return this;
        }
        int end = offset + bytes.Length;
        var files = (CurrentTextureFiles ?? []).Where(file => file.Offset + file.Length <= offset || file.Offset >= end).ToImmutableArray().Add(new(offset, bytes.Length, identity, Convert.ToHexString(SHA256.HashData(bytes))));
        if (Transaction is { } transaction)
        {
            transaction.SetTextureFiles(files);
            return this;
        }
        return this with { TextureFiles = files };
    }

    private void ValidateTextureFiles()
    {
        if (TextureFiles is not { } files)
        {
            return;
        }
        if (files.IsDefault)
        {
            throw new InvalidDataException("Native loaded texture files require explicit retained descriptors.");
        }
        foreach (NativeBattleTextureFileSource file in files)
        {
            if (file.Offset < 0 || file.Length <= 0 || file.Offset > Length - file.Length || string.IsNullOrWhiteSpace(file.Identity.Name) || !Enum.IsDefined(file.Identity.Domain) || file.Sha256 is not { Length: 64 })
            {
                throw new InvalidDataException("Native loaded texture file metadata leaves its bounded source owner.");
            }
        }
    }

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
