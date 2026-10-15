using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>AF3DN1001F290 selects the requested marker and enters52F9E0 at phase6.</summary>
public sealed record NativeSteamDiscMount([property: JsonRequired] int Version, [property: JsonRequired] byte SelectedDisc, [property: JsonRequired] string WrapperSha256, [property: JsonRequired] long MarkerLength, [property: JsonRequired] string MarkerSha256, [property: JsonRequired] string WorldFieldTableSha256)
{
    public const string VerifiedWrapperSha256 = "539558E4F5CF495735CCC8D5E98A185E3660CEB8D5DDF21EB7558B02FBB21FEC";

    public void Validate(byte expectedDisc)
    {
        if (Version != 1 || SelectedDisc is < 1 or > 4 || SelectedDisc != expectedDisc || WrapperSha256 != VerifiedWrapperSha256 || MarkerLength <= 0 || !ValidHash(MarkerSha256) || !ValidHash(WorldFieldTableSha256))
        {
            throw new InvalidDataException("Steam DISCJUMP requires its verified wrapper and actual selected nonempty marker and wm2field resources.");
        }
    }

    private static bool ValidHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

/// <summary>521B70 mode6 request,4767B0 kind9 dispatch and4706B0 field-entry-kind0.</summary>
public sealed record NativeDiscJumpTransition([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] int SourceFieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] byte PhysicalSlot, [property: JsonRequired] ushort NativeFacingWord, [property: JsonRequired] NativeSteamDiscMount Mount)
{
    public const string CurrentRule = "ff8-en-521b70-mode6-kind9-af3-phase6-v1";
    [JsonIgnore]
    public int NativeMode => 6;
    [JsonIgnore]
    public int NativeTransitionKind => 9;
    [JsonIgnore]
    public int NativeFieldEntryKind => 0;
    [JsonIgnore]
    public byte NativeRequest4900 => 1;
    [JsonIgnore]
    public int NativeReturn => 1;

    public void Validate(byte expectedDisc)
    {
        if (Version != 1 || Rule != CurrentRule || SourceFieldId < 0 || ObjectId < 0 || ScriptId < 0 || Operation < 0 || PhysicalSlot > 7 || ProgramHash is not { Length: 64 } || ProgramHash.Any(character => !Uri.IsHexDigit(character)) || ExecutableHash is not { Length: 64 } || ExecutableHash.Any(character => !Uri.IsHexDigit(character)) || Mount is null)
        {
            throw new InvalidDataException("DISCJUMP requires its executed physical-slot request and native transition contract.");
        }
        Mount.Validate(expectedDisc);
    }
}
