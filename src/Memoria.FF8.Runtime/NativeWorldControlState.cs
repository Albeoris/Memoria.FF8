using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeWorldControlOriginKind
{
    NativeNewGame
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeWorldControlOrigin([property: JsonRequired] int ProofVersion, [property: JsonRequired] NativeWorldControlOriginKind Kind, [property: JsonRequired] string InitializationRule);

/// <summary>The single event BYTE103, DWORD104 and menu BYTE209, produced by executed native New Game initialization.</summary>
public sealed record NativeWorldControlState
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentProofVersion = 1;
    public const string InitializationRule = "ff8-en-world-control-52ca90-522230-v1";

    [JsonRequired]
    public required int SchemaVersion { get; init; }
    [JsonRequired]
    public required NativeWorldControlOrigin Origin { get; init; }
    [JsonRequired]
    public required byte EventFlags103 { get; init; }
    [JsonRequired]
    public required uint EventFlags104 { get; init; }
    [JsonIgnore]
    public bool SourceDisabled => (EventFlags104 & 0x200) != 0;
    [JsonIgnore]
    public bool ScriptBattleDisabled20 => (EventFlags104 & 0x20) != 0;
    [JsonIgnore]
    public bool SalarySuppressed => (EventFlags104 & 8) != 0;
    [JsonIgnore]
    public bool SalaryDisplaySuppressed => (EventFlags104 & 0x10) != 0;
    /// <summary>The complete known event byte 209, with unrelated bits preserved by updates.</summary>
    [JsonRequired]
    public required byte Menu209 { get; init; }
    [JsonRequired]
    public required byte MusicKeep207 { get; init; }

    public int ReadEventBytes(int address, int width)
    {
        ValidateEventBytes(address, width);
        ulong bank = EventFlags103 | ((ulong)EventFlags104 << 8);
        ulong mask = (1UL << (width * 8)) - 1;
        return unchecked((int)((bank >> ((address - 103) * 8)) & mask));
    }

    public NativeWorldControlState WriteEventBytes(int address, int width, int value)
    {
        ValidateEventBytes(address, width);
        int shift = (address - 103) * 8;
        ulong mask = ((1UL << (width * 8)) - 1) << shift;
        ulong bank = EventFlags103 | ((ulong)EventFlags104 << 8);
        ulong next = (bank & ~mask) | (((ulong)unchecked((uint)value) << shift) & mask);
        return this with { EventFlags103 = unchecked((byte)next), EventFlags104 = unchecked((uint)(next >> 8)) };
    }

    private void ValidateEventBytes(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || address < 103 || (long)address + width > 108)
        {
            throw new NotSupportedException("Native control memory supports only executed known bytes103..107 at BYTE/WORD/DWORD width.");
        }
    }

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
    }

    /// <summary>Native PHSPOWER at 522230 updates source mask 0x200; operand zero also clears byte 209 mask 2.</summary>
    public NativeWorldControlState WithPartyPower(int argument)
    {
        Validate();
        return this with
        {
            EventFlags104 = argument == 0 ? EventFlags104 | 0x200u : EventFlags104 & ~0x200u,
            Menu209 = argument == 0 ? (byte)(Menu209 & 0xFD) : Menu209
        };
    }

    /// <summary>Native SAVEENABLE at 5221B0 sets byte 209 mask 1 for any nonzero DWORD operand, or clears it for zero.</summary>
    public NativeWorldControlState WithSaveEnabled(int argument)
    {
        Validate();
        return this with { Menu209 = argument == 0 ? (byte)(Menu209 & 0xFE) : (byte)(Menu209 | 1) };
    }

    /// <summary>Native PHSENABLE at522280 updates byte209 mask2 only when DWORD104 mask200 is clear; it consumes a full DWORD in both branches.</summary>
    public NativeWorldControlState WithPartyMenuEnabled(int argument)
    {
        Validate();
        if (SourceDisabled)
        {
            return this;
        }
        return this with { Menu209 = argument == 0 ? (byte)(Menu209 & 0xFD) : (byte)(Menu209 | 2) };
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
        return this with { Menu209 = (byte)(Menu209 & 0xFC), EventFlags104 = EventFlags104 & ~0x400u, MusicKeep207 = 0 };
    }

    public NativeWorldControlState WithSalaryEnabled(bool enabled)
    {
        Validate();
        return this with { EventFlags104 = enabled ? EventFlags104 & ~0x18u : EventFlags104 | 0x18u };
    }

    public NativeWorldControlState WithSalaryDisplayEnabled(bool enabled)
    {
        Validate();
        return this with { EventFlags104 = enabled ? EventFlags104 & ~0x10u : EventFlags104 | 0x10u };
    }

    public NativeWorldControlState WithGuestMember(int guestIdentity, bool enabled)
    {
        Validate();
        if (guestIdentity is < 8 or > 10)
        {
            throw new NotSupportedException("Native guest acquisition requires identity8..10.");
        }
        uint mask = 1u << (guestIdentity - 8);
        return this with { EventFlags104 = enabled ? EventFlags104 | mask : EventFlags104 & ~mask };
    }

    private static NativeWorldControlState CreateInitial(NativeWorldControlOrigin origin)
    {
        return new NativeWorldControlState
        {
            SchemaVersion = CurrentSchemaVersion,
            Origin = origin,
            EventFlags103 = 0,
            EventFlags104 = 0x18,
            Menu209 = 0,
            MusicKeep207 = 0,
        };
    }
}
