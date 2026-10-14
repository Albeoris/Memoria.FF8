using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeCharacterDress([property: JsonRequired] byte Dress);

/// <summary>Original save-record costume BYTE91, initialized only from the authentic New Game template.</summary>
public sealed record NativeCharacterAppearance
{
    public const string InitializationRule = "ff8-en-init-out-character-dress-byte91-v1";
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required string InitOutSha256 { get; init; }
    [JsonRequired]
    public required NativeCharacterDress[] Records { get; init; }

    public void Validate()
    {
        if (Version != 1 || Rule != InitializationRule || InitOutSha256 is not { Length: 64 } || !InitOutSha256.All(Uri.IsHexDigit) || Records is not { Length: 8 } || Records.Where((record, index) => record is null).Any())
        {
            throw new InvalidDataException("Native character appearance lacks its original template and eight character records.");
        }
    }

    public NativeCharacterAppearance SetDressAtRow(byte row, byte dress)
    {
        Validate();
        int index = row;
        if (index >= Records.Length)
        {
            throw new NotSupportedException("SETDRESS resolved outside the eight physical saved costume rows.");
        }
        NativeCharacterDress[] records = Records.ToArray();
        records[index] = records[index] with { Dress = dress };
        return this with { Records = records };
    }
}
