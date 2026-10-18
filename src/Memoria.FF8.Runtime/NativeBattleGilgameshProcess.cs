using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshProcess([property: JsonRequired] NativeBattleGilgameshModelMemory Models, [property: JsonRequired] NativeBattleGilgameshEntryState? Entry, [property: JsonRequired] NativeBattleActionMatrix? WorldView, [property: JsonRequired] NativeBattleGuardianCameraTask? CameraTask, [property: JsonRequired] int? VibrationHandle, [property: JsonRequired] NativeBattleGilgameshOpeningFlareState OpeningFlare)
{
    [JsonRequired]
    public short? Variant { get; init; }

    public static NativeBattleGilgameshProcess FromColdProcess() => new(NativeBattleGilgameshModelMemory.FromColdProcess(), null, null, null, null, NativeBattleGilgameshOpeningFlareState.FromColdProcess());

    public void Validate()
    {
        if (Models is null)
        {
            throw new InvalidDataException("Native Gilgamesh lost its retained raw model banks.");
        }
        Models.Validate();
        (OpeningFlare ?? throw new InvalidDataException("Native Gilgamesh requires its retained opening-flare matrix.")).Validate();
        Entry?.World.Validate();
        WorldView?.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGilgameshProcess? Gilgamesh { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePcRenderRequest? PcRenderRequest { get; init; }
}

public sealed record NativeBattleGilgameshEffectResources([property: JsonRequired] NativeBattleGilgameshResources Files, [property: JsonRequired] NativeBattleGilgameshModelResources Models, [property: JsonRequired] NativeBattleGilgameshDirectorResources Director, [property: JsonRequired] NativeBattleGilgameshFlareResources Flare, [property: JsonRequired] NativeBattleGilgameshOpeningResources Opening, [property: JsonRequired] NativeBattleGilgameshParticleResources Particles, [property: JsonRequired] NativeBattleGilgameshMeshAuxiliaryResources MeshAuxiliaries, [property: JsonRequired] NativeBattleGilgameshCommonParticleResources CommonParticles, [property: JsonRequired] NativeBattleGilgameshPointResources Points, [property: JsonRequired] NativeBattleStaticMeshResource FinalRing, [property: JsonRequired] NativeBattleGilgameshOpeningFlareResources OpeningFlare, [property: JsonRequired] int InitialStreamNumber, [property: JsonRequired] NativeBattleGilgameshSwordParticleResources SwordParticles);
