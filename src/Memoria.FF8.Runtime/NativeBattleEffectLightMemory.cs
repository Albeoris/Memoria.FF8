using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore]
    public ImmutableArray<byte>? EffectLight1D99AB0
    {
        get => SharedCustomMatrix?.Bytes;
        init
        {
            SharedCustomMatrix = value is { } bytes ? new(bytes, SharedCustomMatrix?.Known ?? Enumerable.Repeat(true, 32).ToImmutableArray()) : null;
        }
    }

    [JsonIgnore]
    public ImmutableArray<bool>? EffectLightKnown1D99AB0
    {
        get => SharedCustomMatrix is { } matrix && !matrix.Known.All(value => value) ? matrix.Known : null;
        init
        {
            if (SharedCustomMatrix is { } matrix)
            {
                SharedCustomMatrix = matrix with { Known = value ?? Enumerable.Repeat(true, 32).ToImmutableArray() };
            }
            else if (value is not null)
            {
                throw new InvalidDataException("Native matrix knowledge requires its retained physical bytes.");
            }
        }
    }

    [JsonPropertyName("EffectLight1D99AB0")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? LegacyEffectLightBytes
    {
        get => null;
        init
        {
            if (value is not null)
            {
                throw new JsonException("The separate effect-light matrix requires an explicit snapshot migration to the shared physical matrix owner.");
            }
        }
    }

    [JsonPropertyName("EffectLightKnown1D99AB0")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<bool>? LegacyEffectLightKnown
    {
        get => null;
        init
        {
            if (value is not null)
            {
                throw new JsonException("The separate effect-light knowledge mask requires an explicit snapshot migration to the shared physical matrix owner.");
            }
        }
    }

    public NativeBattlePackedMatrix32 CaptureEffectLight1D99AB0()
    {
        ValidateEffectLight1D99AB0();
        return SharedCustomMatrix is { } matrix ? new(matrix.Bytes, matrix.Known) : new(new byte[32].ToImmutableArray(), new bool[32].ToImmutableArray());
    }

    public NativeBattleEffectMemory PublishEffectLight1D99AB0(NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        return this with { SharedCustomMatrix = new(matrix.Bytes, matrix.Known) };
    }

    public void ValidateEffectLight1D99AB0() => SharedCustomMatrix?.Validate();
}
