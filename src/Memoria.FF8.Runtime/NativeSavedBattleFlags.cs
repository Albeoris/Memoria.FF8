using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSavedBattleFlagFieldWrite(int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, int RawOperand);

/// <summary>Saved BYTE1CFE97A, including the checked DreamEnabled bit0 alias.</summary>
// The legacy InitOutSha256 field identifies the original reset producer, independently of selected template contents.
public sealed record NativeSavedBattleFlags([property: JsonRequired] string InitOutSha256, [property: JsonRequired] byte Value, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? KnownMask = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeSavedBattleFlagFieldWrite? FieldWrite = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeSavedBattleFlagFieldWrite? WitchFieldWrite = null)
{
    [JsonIgnore]
    public byte EffectiveKnownMask => KnownMask ?? byte.MaxValue;

    public void Validate()
    {
        if (InitOutSha256 != NativeInputConfiguration.OriginalInitHash)
        {
            throw new InvalidDataException("Native saved battle flags require their authenticated New Game clear provenance.");
        }
        byte provedMask = (byte)(1 | (FieldWrite is not null ? 0x10 : 0) | (WitchFieldWrite is not null ? 0x20 : 0));
        if (KnownMask is not null && (KnownMask is not (0x11 or 0x21 or 0x31) || KnownMask != provedMask || (Value & ~KnownMask.Value) != 0))
        {
            throw new InvalidDataException("Partial saved battle flags require only the independent dream alias and their reached bit4/SETWITCH writers.");
        }
        if (FieldWrite is { } write && (write.FieldId < 72 || write.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || write.ObjectId < 0 || write.ScriptId < 0 || write.Operation < 0 || write.Frame < 1 || ((Value & 0x10) != 0) != (write.RawOperand != 0)))
        {
            throw new InvalidDataException("Saved battle flag bit4 lost its original529EB0 full-DWORD producer.");
        }
        if (WitchFieldWrite is { } witch && (witch.FieldId < 72 || witch.ProgramHash is not { Length: 64 } witchHash || !witchHash.All(Uri.IsHexDigit) || witch.ObjectId < 0 || witch.ScriptId < 0 || witch.Operation < 0 || witch.Frame < 0 || ((Value & 0x20) != 0) != (witch.RawOperand != 0)))
        {
            throw new InvalidDataException("Saved battle flag bit5 lost its original523180 SETWITCH full-DWORD producer.");
        }
    }

    public NativeSavedBattleFlags WithDreamEnabled(bool enabled)
    {
        Validate();
        return this with { Value = (byte)(enabled ? Value | 1 : Value & 0xFE) };
    }

    /// <summary>Native487DF0 opcode54 clears bit1 and sets bit3 of the saved BYTE.</summary>
    public NativeSavedBattleFlags CompleteBattle()
    {
        Validate();
        if (EffectiveKnownMask != byte.MaxValue)
        {
            throw new NotSupportedException("Native battle completion requires the complete saved flag BYTE.");
        }
        return this with { Value = (byte)((Value & 0xF5) | 8) };
    }

    public NativeSavedBattleFlags WithFieldBit4(NativeSavedBattleFlagFieldWrite write)
    {
        Validate();
        NativeSavedBattleFlags result = this with { Value = (byte)(write.RawOperand != 0 ? Value | 0x10 : Value & ~0x10), KnownMask = KnownMask is byte known ? (byte)(known | 0x10) : null, FieldWrite = write };
        result.Validate();
        return result;
    }

    public NativeSavedBattleFlags WithWitch(NativeSavedBattleFlagFieldWrite write)
    {
        Validate();
        NativeSavedBattleFlags result = this with { Value = (byte)(write.RawOperand != 0 ? Value | 0x20 : Value & ~0x20), KnownMask = KnownMask is byte known ? (byte)(known | 0x20) : null, WitchFieldWrite = write };
        result.Validate();
        return result;
    }

    public static byte ApplyBattleEffects(byte before, byte setMask, byte clearMask)
    {
        //48D200 item31 sets0x04;487DF0 opcode54 clears0x02 and sets0x08. These known masks commute.
        if ((setMask & ~0x0C) != 0 || (clearMask & ~0x02) != 0 || ((setMask & 8) != 0) != ((clearMask & 2) != 0))
        {
            throw new InvalidDataException("Saved battle flag effect masks lack their exact native producer contract.");
        }
        return (byte)((before & ~clearMask) | setMask);
    }
}
