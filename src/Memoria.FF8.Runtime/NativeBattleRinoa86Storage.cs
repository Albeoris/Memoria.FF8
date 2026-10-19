using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The cold image declaration is explicit; imported snapshots missing this owner remain unknown.</summary>
public static class NativeBattleRinoa86Storage
{
    public static NativeBattleRinoa86State FromColdProcess() => new(0, 0, null, null, null) { MorphScratch = new byte[640].ToImmutableArray(), BodyProjected = new byte[2800].ToImmutableArray() };

    public static void Validate(NativeBattleRinoa86State state)
    {
        if (state.SourceArenaOffset < 0 || state.SourceArenaOffset > NativeBattleEffectArena.Length - 114688 || state.AuxiliaryCursor < 0 || state.AuxiliaryCursor > NativeBattleEffectArena.Length || state.MorphScratch.IsDefault || state.MorphScratch.Length != 640 || state.BodyProjected.IsDefault || state.BodyProjected.Length != 2800 || state.BodyBones is { } bones && (bones.IsDefault || bones.Length != 34) || state.BodyModel is not null && state.BodyBones is null)
        {
            throw new InvalidDataException("Native Rinoa86 retained storage lost its original physical workspace or body owner.");
        }
        state.Scene?.Validate();
        state.Io?.Validate();
        state.CopiedCamera?.Validate();
        if (state.BodyBones is { } matrices)
        {
            foreach (NativeBattleActionMatrix matrix in matrices)
            {
                matrix.Validate();
            }
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa86State? Rinoa86 { get; init; }
}
