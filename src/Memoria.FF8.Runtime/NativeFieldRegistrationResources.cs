using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeFieldModelSource([property: JsonRequired] string Member, [property: JsonRequired] string Sha256, [property: JsonRequired] ImmutableArray<byte> Data)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Member) || Data.IsDefaultOrEmpty || Sha256 != Convert.ToHexString(SHA256.HashData(Data.AsSpan())))
        {
            throw new InvalidDataException("Native field registration requires its complete original member bytes and exact hash.");
        }
    }
}

public sealed record NativeFieldOneModelEntry([property: JsonRequired] int Offset, [property: JsonRequired] int Size, [property: JsonRequired] uint Flag, [property: JsonRequired] int ModelOffset, [property: JsonRequired] ImmutableArray<int> Textures, [property: JsonRequired] string Name)
{
    [JsonRequired]
    public ImmutableArray<uint> RawTextureWords { get; init; }
}

/// <summary>Original532A40 inputs beside decoded models; these bytes are not a completed registration or VRAM upload.</summary>
public sealed record NativeFieldRegistrationResources([property: JsonRequired] int FieldId, [property: JsonRequired] string FieldName, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] string ProgramHash, [property: JsonRequired] NativeFieldModelSource One, [property: JsonRequired] ImmutableArray<NativeFieldOneModelEntry> Directory, [property: JsonRequired] ImmutableArray<NativeFieldModelSource> MainCharacters)
{
    public void Validate()
    {
        if (FieldId is < 72 or > ushort.MaxValue || string.IsNullOrWhiteSpace(FieldName) || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || One is null || Directory.IsDefault || MainCharacters.IsDefault || MainCharacters.Any(source => source is null) || MainCharacters.Select(source => source.Member).Distinct(StringComparer.Ordinal).Count() != MainCharacters.Length)
        {
            throw new InvalidDataException("Native kind3 registration requires original field identity, program and actual ONE inputs.");
        }
        One.Validate();
        if (One.Data.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(One.Data.AsSpan()) != Directory.Length)
        {
            throw new InvalidDataException("Native ONE registration directory does not match its actual source count.");
        }
        foreach (NativeFieldOneModelEntry entry in Directory)
        {
            if (entry is null || entry.Offset < 0 || entry.Size < 0 || entry.Offset > One.Data.Length - entry.Size || entry.Textures.IsDefault || entry.RawTextureWords.IsDefault || string.IsNullOrEmpty(entry.Name))
            {
                throw new InvalidDataException("Native ONE directory is outside its original member.");
            }
        }
        foreach (NativeFieldModelSource source in MainCharacters)
        {
            source.Validate();
        }
    }
}
