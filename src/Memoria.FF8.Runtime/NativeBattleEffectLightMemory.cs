using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<bool>? EffectLightKnown1D99AB0 { get; init; }

    public NativeBattlePackedMatrix32 CaptureEffectLight1D99AB0()
    {
        ValidateEffectLight1D99AB0();
        return new(EffectLight1D99AB0 ?? new byte[32].ToImmutableArray(), EffectLightKnown1D99AB0 ?? Enumerable.Repeat(EffectLight1D99AB0.HasValue, 32).ToImmutableArray());
    }

    public NativeBattleEffectMemory PublishEffectLight1D99AB0(NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        return this with { EffectLight1D99AB0 = matrix.Bytes, EffectLightKnown1D99AB0 = matrix.Known.All(value => value) ? null : matrix.Known };
    }

    public void ValidateEffectLight1D99AB0()
    {
        if (EffectLight1D99AB0 is { } bytes && (bytes.IsDefault || bytes.Length != 32) || EffectLightKnown1D99AB0 is { } known && (known.IsDefault || known.Length != 32 || EffectLight1D99AB0 is null))
        {
            throw new InvalidDataException("Native effect lighting requires its retained32 bytes and matching knownness.");
        }
    }
}
