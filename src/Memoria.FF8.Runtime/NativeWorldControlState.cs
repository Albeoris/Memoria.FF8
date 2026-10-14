using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeWorldControlOriginKind
{
    NativeNewGame
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeWorldControlOrigin([property: JsonRequired] int ProofVersion, [property: JsonRequired] NativeWorldControlOriginKind Kind, [property: JsonRequired] string InitializationRule);

/// <summary>Known source/menu flags and optional script-battle mask20; this does not establish the complete DWORD104.</summary>
public sealed record NativeWorldControlState
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentProofVersion = 1;
    public const string InitializationRule = "ff8-en-world-control-52ca90-522230-v1";

    [JsonRequired]
    public required int SchemaVersion { get; init; }
    [JsonRequired]
    public required NativeWorldControlOrigin Origin { get; init; }
    /// <summary>Event byte 105 bit value 2, equivalently native DWORD 104 mask 0x200.</summary>
    [JsonRequired]
    public required bool SourceDisabled { get; init; }
    /// <summary>The complete known event byte 209, with unrelated bits preserved by updates.</summary>
    [JsonRequired]
    public required byte Menu209 { get; init; }

    [JsonRequired]
    public bool? ScriptBattleDisabled20 { get; init; }

    public static NativeWorldControlState CreateNewGame()
    {
        // 52CA90 clears event bytes 0..247, then ORs DWORD 104 with 0x18.
        return CreateInitial(new NativeWorldControlOrigin(CurrentProofVersion, NativeWorldControlOriginKind.NativeNewGame, InitializationRule));
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || Origin is null || Origin.ProofVersion != CurrentProofVersion || !Enum.IsDefined(Origin.Kind) || Origin.InitializationRule != InitializationRule)
        {
            throw new InvalidDataException("Unsupported or invalid native world-control provenance.");
        }
        if (ScriptBattleDisabled20 is null)
        {
            throw new InvalidDataException("Native world-control state requires its recorded script-battle flag.");
        }
    }

    /// <summary>Native PHSPOWER at 522230 updates source mask 0x200; operand zero also clears byte 209 mask 2.</summary>
    public NativeWorldControlState WithPartyPower(int argument)
    {
        Validate();
        return this with
        {
            SourceDisabled = argument == 0,
            Menu209 = argument == 0 ? (byte)(Menu209 & 0xFD) : Menu209
        };
    }

    /// <summary>Native WORLDMAPJUMP at 521820 copies the inverse source mask into byte 209 mask 2.</summary>
    public NativeWorldControlState WithWorldEntryMenuFlag()
    {
        Validate();
        return this with { Menu209 = SourceDisabled ? (byte)(Menu209 & 0xFD) : (byte)(Menu209 | 2) };
    }

    /// <summary>Only for the native 52BC00 entry-kind 1 or 3 branch; other field-load kinds must not call this method.</summary>
    public NativeWorldControlState WithNormalFieldLoad()
    {
        Validate();
        return this with { Menu209 = (byte)(Menu209 & 0xFC) };
    }

    private static NativeWorldControlState CreateInitial(NativeWorldControlOrigin origin)
    {
        return new NativeWorldControlState
        {
            SchemaVersion = CurrentSchemaVersion,
            Origin = origin,
            SourceDisabled = false,
            Menu209 = 0,
            ScriptBattleDisabled20 = false
        };
    }
}
