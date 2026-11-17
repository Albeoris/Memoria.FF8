using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

/// <summary>Presentation facts inspected by native contract tests and historical fixture readers.</summary>
internal sealed record NativeBattleFixtureState
{
    public NativeBattleGraphicsState? NativeGraphics { get; init; }
    public NativeBattleCameraState? NativeCamera { get; init; }
    public NativeBattleStageState? NativeStage { get; init; }
    public NativeBattleEffectMemory? NativeEffectMemory { get; init; }
    public int? NativeEffectIoResult { get; init; }
    public NativeBattleFileIoState? NativeFileIo { get; init; }
    public NativeBattleFileStaging? NativeFileStaging { get; init; }
    public NativeBattleModelLoader? NativeModelLoader { get; init; }
    public NativeBattleModelTextureCache? NativeModelTextureCache { get; init; }
    public NativeBattleModelCpuResources? NativeModelCpuResources { get; init; }
    public NativeBattleGuardianBoost? NativeGuardianBoost { get; init; }

    internal static T? ReadArchived<T>(JsonElement archive, string property, JsonSerializerOptions? options = null)
    {
        return archive.TryGetProperty(property, out JsonElement field) && field.ValueKind != JsonValueKind.Null
            ? field.Deserialize<T>(options)
            : default;
    }
}

internal static class NativeBattleFixtureObservations
{
    internal static BattleExpandedAiCallbackRequest CreateCallback(string actorId, int originalIndex, int stage)
    {
        BattleActorReference actor = NativeBattleSlots.Decode(originalIndex).Side == BattleSide.Allies
            ? new BattlePartyActorReference(actorId)
            : new BattleEnemyActorReference(actorId);
        return new(actor, checked((byte)stage));
    }

    internal static NativeBattleFixtureState ReadFixturePresentation(this BattleSnapshot snapshot)
    {
        BattlePresentationSnapshot? continuation = snapshot.PresentationContinuation;
        BattlePresentationNativeState? state = continuation?.Native;
        return new()
        {
            NativeGraphics = state?.NativeGraphics,
            NativeCamera = state?.NativeCamera,
            NativeStage = state?.NativeStage,
            NativeEffectMemory = state?.NativeEffectMemory,
            NativeEffectIoResult = state?.NativeEffectIoResult,
            NativeFileIo = state?.NativeFileIo,
            NativeFileStaging = state?.NativeFileStaging,
            NativeModelLoader = state?.NativeModelLoader,
            NativeModelTextureCache = state?.NativeModelTextureCache,
            NativeModelCpuResources = state?.NativeModelCpuResources,
            NativeGuardianBoost = continuation?.GuardianBoost
        };
    }

    internal static BattleSnapshot WithFixturePresentation(this BattleSnapshot snapshot, Func<NativeBattleFixtureState, NativeBattleFixtureState> project)
    {
        BattlePresentationSnapshot continuation = snapshot.PresentationContinuation
            ?? throw new InvalidOperationException("The fixture requires a current typed battle continuation.");
        NativeBattleFixtureState facts = project(snapshot.ReadFixturePresentation());
        return snapshot with
        {
            PresentationContinuation = continuation with
            {
                Native = continuation.Native with
                {
                    NativeGraphics = facts.NativeGraphics,
                    NativeCamera = facts.NativeCamera,
                    NativeStage = facts.NativeStage,
                    NativeEffectMemory = facts.NativeEffectMemory,
                    NativeEffectIoResult = facts.NativeEffectIoResult,
                    NativeFileIo = facts.NativeFileIo,
                    NativeFileStaging = facts.NativeFileStaging,
                    NativeModelLoader = facts.NativeModelLoader,
                    NativeModelTextureCache = facts.NativeModelTextureCache,
                    NativeModelCpuResources = facts.NativeModelCpuResources
                },
                GuardianBoost = facts.NativeGuardianBoost
            }
        };
    }
}
