using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Assets;

/// <summary>Original field SFX DWORD identities, independently of the named EFFECTLOAD bank.</summary>
public sealed record FieldSoundEffectTable(string MemberName, string Sha256, ImmutableArray<uint> EffectIds)
{
    public static FieldSoundEffectTable Read(string memberName, ReadOnlySpan<byte> bytes)
    {
        if (string.IsNullOrWhiteSpace(memberName) || !memberName.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase) || bytes.Length % sizeof(uint) != 0)
        {
            throw new InvalidDataException("A field SFX table requires its member name and complete DWORD identities.");
        }
        ImmutableArray<uint>.Builder effects = ImmutableArray.CreateBuilder<uint>(bytes.Length / sizeof(uint));
        for (int offset = 0; offset < bytes.Length; offset += sizeof(uint))
        {
            effects.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]));
        }
        return new FieldSoundEffectTable(memberName, Convert.ToHexString(SHA256.HashData(bytes)), effects.MoveToImmutable());
    }
}
