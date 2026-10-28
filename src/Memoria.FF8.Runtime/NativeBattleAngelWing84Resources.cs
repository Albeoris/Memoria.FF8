using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84Resources(NativeBattleAngelWing84BodyResources Body, NativeBattleAngelWing84CameraResources Camera, NativeBattleAngelWing84GlowResources Glows, NativeBattleAngelWing84NodeResources Nodes, NativeBattleAngelWing84ProgramResources Programs, NativeBattleEffectHeapAllocation SceneTim);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAngelWing84State? AngelWing84
    {
        get => effectState.AngelWing84;
        init => effectState = effectState with { AngelWing84 = value };
    }
}

public static class NativeBattleAngelWing84Storage
{
    public static NativeBattleAngelWing84State FromColdProcess() => new(0, 0, null, null, null, null, null) { BodyDrawStorage = NativeBattleAngelWing84BodyDrawStorage.FromColdProcess(), BodyShaderAngle = 0 };

    public static void Validate(NativeBattleAngelWing84State state)
    {
        if (state.SourceArenaOffset < 0 || state.SourceArenaOffset > NativeBattleEffectArena.Length - 98304 || state.AuxiliaryCursor < 0 || state.AuxiliaryCursor > NativeBattleEffectArena.Length || state.NodeData is { } data && (data.IsDefault || data.Length != NativeBattleAngelWing84NodeResources.Length) || state.BodyBones is { } bones && (bones.IsDefault || bones.Length != 22) || state.BodyModel is not null && state.BodyBones is null || state.BodyShaderAngle is null)
        {
            throw new InvalidDataException("Native Angel Wing requires its exact retained process and body matrix owners.");
        }
        state.CopiedCamera?.Validate();
        state.Scene?.Validate();
        NativeBattleAngelWing84BodyDrawStorage storage = state.BodyDrawStorage ?? throw new InvalidDataException("Native Angel Wing drawing storage is missing.");
        Check(storage.Context, 84);
        Check(storage.Parameters, 40);
        Check(storage.Vertices, NativeBattleAngelWing84ShaderMesh.VertexCapacityBytes);
        if (storage.OrdinaryProjected.IsDefault || storage.OrdinaryProjected.Length != 2560)
        {
            throw new InvalidDataException("Native Angel Wing requires its original ordinary projected vertex bank.");
        }
        if (state.BodyBones is { } current)
        {
            foreach (NativeBattleActionMatrix matrix in current)
            {
                matrix.Validate();
            }
        }

        static void Check(NativeBattleEffectArena arena, int length)
        {
            arena.Validate();
            arena.ValidateExtent(length);
        }
    }
}
