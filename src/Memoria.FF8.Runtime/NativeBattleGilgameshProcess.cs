using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshProcess([property: JsonRequired] NativeBattleGilgameshModelMemory Models, [property: JsonRequired] NativeBattleGilgameshEntryState? Entry, [property: JsonRequired] NativeBattleActionMatrix? WorldView, [property: JsonRequired] NativeBattleGuardianCameraTask? CameraTask, [property: JsonRequired] int? VibrationHandle, [property: JsonRequired] NativeBattleGilgameshOpeningFlareState OpeningFlare)
{
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
}

public sealed record NativeBattleGilgameshEffectResources(NativeBattleGilgameshResources Files, NativeBattleGilgameshModelResources Models, NativeBattleGilgameshDirectorResources Director, NativeBattleGilgameshFlareResources Flare, NativeBattleGilgameshOpeningResources Opening, NativeBattleGilgameshParticleResources Particles, NativeBattleGilgameshMeshAuxiliaryResources MeshAuxiliaries, NativeBattleGilgameshCommonParticleResources CommonParticles, NativeBattleGilgameshPointResources Points, NativeBattleStaticMeshResource FinalRing, NativeBattleGilgameshOpeningFlareResources OpeningFlare, int InitialStreamNumber);
