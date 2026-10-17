using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native46C020's saved availability and copied voice metadata. Null metadata preserves an unobserved source, rather than clearing it.</summary>
public sealed record NativeBattleMusicEffectCache([property: JsonRequired] uint PlayingVoiceMask, [property: JsonRequired] ImmutableArray<byte>? VoiceRecords)
{
    public void Validate()
    {
        if (VoiceRecords is { } records && (records.IsDefault || records.Length != 0xB40))
        {
            throw new InvalidDataException("Native46C020 voice metadata copy requires its complete B40-byte source or explicit unknown bytes.");
        }
    }
}

/// <summary>Active battle authority for the same transferred music buffers and native return selectors; no second buffer ledger.</summary>
public sealed partial record NativeBattleMusicProcessState([property: JsonRequired] NativeMusicProcessState Buffers, [property: JsonRequired] byte? ReturnBank, [property: JsonRequired] ushort? ReturnChannel)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? CurrentFieldWord { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicEffectCache? EffectCache { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicEntryReceipt? Entry { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMusicReturnReceipt? Return { get; init; }

    public void Validate()
    {
        if (Buffers is null || ReturnBank is > 1)
        {
            throw new InvalidDataException("Native battle music requires its transferred physical buffer owner and known-or-unknown return selector.");
        }
        Buffers.Validate();
        EffectCache?.Validate();
        Entry?.Validate();
        Return?.Validate();
        ValidateCommands();
    }

    partial void ValidateCommands();
}
