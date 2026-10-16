using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved BYTE1CFE97A, including the checked DreamEnabled bit0 alias.</summary>
public sealed record NativeSavedBattleFlags([property: JsonRequired] string InitOutSha256, [property: JsonRequired] byte Value)
{
    public void Validate()
    {
        if (InitOutSha256 != NativeInputConfiguration.OriginalInitHash)
        {
            throw new InvalidDataException("Native saved battle flags require their authenticated New Game clear provenance.");
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
        return this with { Value = (byte)((Value & 0xF5) | 8) };
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
