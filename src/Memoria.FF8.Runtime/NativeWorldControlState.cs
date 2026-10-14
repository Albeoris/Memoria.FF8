using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeWorldControlOriginKind
{
    NativeNewGame,
    VerifiedLegacyNewGame
}

public sealed record NativeWorldControlOrigin([property: JsonRequired] int ProofVersion, [property: JsonRequired] NativeWorldControlOriginKind Kind, [property: JsonRequired] string InitializationRule, [property: JsonRequired] string? LegacyCheckpointSha256);

/// <summary>Known source/menu flags and optional script-battle mask20; this does not establish the complete DWORD104.</summary>
public sealed record NativeWorldControlState
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentProofVersion = 1;
    public const string InitializationRule = "ff8-en-world-control-52ca90-522230-v1";
    public const string LegacyCheckpoint172PreinitSha256 = "9d370a2f777cb1994c65e3a060b038a870879e04142e7a0d407a51476a2907d6";

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ScriptBattleDisabled20 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeScriptBattleFlagMigrationProof? ScriptBattleFlagMigration { get; init; }

    public static NativeWorldControlState CreateNewGame()
    {
        // 52CA90 clears event bytes 0..247, then ORs DWORD 104 with 0x18.
        return CreateInitial(new NativeWorldControlOrigin(CurrentProofVersion, NativeWorldControlOriginKind.NativeNewGame, InitializationRule, null), false);
    }

    public static NativeWorldControlState CreateVerifiedLegacyNewGame(string sha256)
    {
        if (!IsApprovedLegacyCheckpoint(sha256))
        {
            throw new InvalidDataException("The legacy checkpoint has no approved native world-control flag provenance.");
        }

        // This exact ordinary lineage executed start0 PHSPOWER 0; destination 172 has not been ticked.
        return CreateInitial(new NativeWorldControlOrigin(CurrentProofVersion, NativeWorldControlOriginKind.VerifiedLegacyNewGame, InitializationRule, sha256.ToLowerInvariant()), true);
    }

    public static bool IsApprovedLegacyCheckpoint(string? sha256)
    {
        return string.Equals(sha256, LegacyCheckpoint172PreinitSha256, StringComparison.OrdinalIgnoreCase);
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || Origin is null || Origin.ProofVersion != CurrentProofVersion || !Enum.IsDefined(Origin.Kind) || Origin.InitializationRule != InitializationRule)
        {
            throw new InvalidDataException("Unsupported or invalid native world-control provenance.");
        }
        ScriptBattleFlagMigration?.Validate();
        if (ScriptBattleFlagMigration is not null && (Origin.Kind != NativeWorldControlOriginKind.VerifiedLegacyNewGame || ScriptBattleDisabled20 is null) || Origin.Kind == NativeWorldControlOriginKind.VerifiedLegacyNewGame && ScriptBattleDisabled20 is not null && ScriptBattleFlagMigration is null)
        {
            throw new InvalidDataException("Legacy script-battle flag requires its explicit retained-route migration provenance.");
        }

        if (Origin.Kind == NativeWorldControlOriginKind.NativeNewGame && Origin.LegacyCheckpointSha256 is not null || Origin.Kind == NativeWorldControlOriginKind.VerifiedLegacyNewGame && !IsApprovedLegacyCheckpoint(Origin.LegacyCheckpointSha256))
        {
            throw new InvalidDataException("Invalid legacy checkpoint identity for native world-control provenance.");
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

    /// <summary>Native SAVEENABLE at 5221B0 sets byte 209 mask 1 for any nonzero DWORD operand, or clears it for zero.</summary>
    public NativeWorldControlState WithSaveEnabled(int argument)
    {
        Validate();
        return this with { Menu209 = argument == 0 ? (byte)(Menu209 & 0xFE) : (byte)(Menu209 | 1) };
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

    private static NativeWorldControlState CreateInitial(NativeWorldControlOrigin origin, bool sourceDisabled)
    {
        return new NativeWorldControlState
        {
            SchemaVersion = CurrentSchemaVersion,
            Origin = origin,
            SourceDisabled = sourceDisabled,
            Menu209 = 0,
            ScriptBattleDisabled20 = origin.Kind == NativeWorldControlOriginKind.NativeNewGame ? false : null
        };
    }
}
