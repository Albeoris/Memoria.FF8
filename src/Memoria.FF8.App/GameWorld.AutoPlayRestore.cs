using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    /// <summary>Reloads an ordinary same-field checkpoint through the current loader while retaining its immutable installed resources.</summary>
    internal bool TryRestoreAutoPlayFieldSnapshot(GameState state)
    {
        return TryRestoreAutoPlayFieldSnapshot(state, out _);
    }

    internal bool TryRestoreAutoPlayFieldSnapshot(GameState state, out string? boundary)
    {
        boundary = GetAutoPlayRestoreBoundary(state);
        if (boundary is not null)
        {
            return false;
        }
        JsonElement snapshot = state.EventState!.Value;
        // Use the same allocation and restore path as a cold host. GameAssets owns
        // the cached immutable field resource; LoadField resets VM preparations.
        session.Restore(state);
        LoadField(state.FieldId, state.FieldArrival, snapshot);
        session.SetDepartingFieldState(null);
        Events.ClearDiagnosticHistory();
        return true;
    }

    internal string? GetAutoPlayRestoreBoundary(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (Field is null)
        {
            return "host-field-unloaded";
        }
        if (renderWidth != FieldRenderer.Width || renderHeight != FieldRenderer.Height)
        {
            return "host-render-size";
        }
        if (debugActions.Count != 0)
        {
            return "host-debug-history";
        }
        if (!HasCurrentFieldAllocation)
        {
            return "host-current-allocation";
        }
        if (!Events.CanClearDiagnosticHistory)
        {
            return "vm-active-diagnostic-operation";
        }
        if (Events.NativeRuntimeProfile is not { IsVerified: true } profile)
        {
            return "host-unverified-executable";
        }
        if (worldScene is not null)
        {
            return "host-world-scene";
        }
        if (preparedWorldEntry is not null)
        {
            return "host-prepared-world-entry";
        }
        if (battle is not null)
        {
            return "host-battle";
        }
        if (observingBattle)
        {
            return "host-observing-battle";
        }
        if (LastBattleRewards is not null)
        {
            return "host-battle-rewards";
        }
        if (tripleTriad is not null)
        {
            return "host-triple-triad";
        }
        if (tutorial is not null)
        {
            return "host-tutorial";
        }
        if (tutorialRequest is not null)
        {
            return "host-tutorial-request";
        }
        if (menuTutorialRequest is not null)
        {
            return "host-menu-tutorial-request";
        }
        if (spuAudio is not null)
        {
            return "host-spu-audio";
        }
        if (movieRenderer is not null)
        {
            return "host-movie-renderer";
        }
        if (movieRendererField is not null)
        {
            return "host-movie-renderer-field";
        }
        if (GetOrdinaryAutoPlayFieldBoundary(session.State) is string currentBoundary)
        {
            return "current-" + currentBoundary;
        }
        if (GetOrdinaryAutoPlayFieldBoundary(state) is string candidateBoundary)
        {
            return "candidate-" + candidateBoundary;
        }
        if (state.MoviePlayback != session.State.MoviePlayback)
        {
            return "movie-playback-changed";
        }
        if (state.MovieSource != session.State.MovieSource)
        {
            return "movie-source-changed";
        }
        if (state.FieldId != Field.Id)
        {
            return "candidate-field-changed";
        }
        if (Events.FieldId != Field.Id)
        {
            return "vm-field-changed";
        }
        if (Events.LastFault is not null)
        {
            return "vm-fault";
        }
        if (Events.NativeGameOver is not null)
        {
            return "vm-game-over";
        }
        if (Events.HasPendingBattle)
        {
            return "vm-pending-battle";
        }
        if (Events.PendingTripleTriad is not null)
        {
            return "vm-pending-triple-triad";
        }
        if (Events.PartyMenu is not null)
        {
            return "vm-party-menu";
        }
        if (Events.Scene.NativeTutorial is not null)
        {
            return "vm-tutorial";
        }
        if (Events.Scene.NativeMenuTutorial is not null)
        {
            return "vm-menu-tutorial";
        }
        if (Events.Scene.WorldTransitionRequest is not null)
        {
            return "vm-world-transition";
        }
        if (state.EventState is not JsonElement snapshot)
        {
            return "candidate-vm-absent";
        }
        string hash = Convert.ToHexString(SHA256.HashData(Field.Jsm));
        if (!snapshot.TryGetProperty("FieldId", out JsonElement fieldId) || fieldId.GetInt32() != Field.Id)
        {
            return "snapshot-field-mismatch";
        }
        if (!snapshot.TryGetProperty("ProgramHash", out JsonElement programHash) || programHash.GetString() != hash)
        {
            return "snapshot-program-mismatch";
        }
        if (!snapshot.TryGetProperty("Frame", out JsonElement frame))
        {
            return "snapshot-frame-absent";
        }
        if (!AutoPlayController.HasNeutralScriptedInput(state.FieldButtonInput, state.FieldId, frame.GetInt64()))
        {
            return "candidate-input-not-neutral";
        }
        if (!AutoPlayController.HasNeutralScriptedInput(session.State.FieldButtonInput, Events.FieldId, Events.Frame))
        {
            return "current-input-not-neutral";
        }
        if (!snapshot.TryGetProperty("DefaultsStarted", out JsonElement started) || !started.GetBoolean())
        {
            return "snapshot-defaults-not-started";
        }
        if (!snapshot.TryGetProperty("TransitionRequested", out JsonElement transition) || transition.GetBoolean())
        {
            return "snapshot-transition-requested";
        }
        if (!IsNullSnapshotMember(snapshot, "LastFault"))
        {
            return "snapshot-fault";
        }
        if (!IsNullSnapshotMember(snapshot, "NativeGameOver"))
        {
            return "snapshot-game-over";
        }
        if (!snapshot.TryGetProperty("Scene", out JsonElement scene))
        {
            return "snapshot-scene-absent";
        }
        if (!IsNullSnapshotMember(scene, "NativeTutorial"))
        {
            return "snapshot-tutorial";
        }
        if (!IsNullSnapshotMember(scene, "NativeMenuTutorial"))
        {
            return "snapshot-menu-tutorial";
        }
        if (!IsNullSnapshotMember(scene, "WorldTransitionRequest"))
        {
            return "snapshot-world-transition";
        }
        if (!snapshot.TryGetProperty("PartyMenu", out JsonElement menu) || menu.ValueKind != JsonValueKind.Null)
        {
            return "snapshot-party-menu";
        }
        if (!scene.TryGetProperty("NativeFieldInitialization", out JsonElement initialization) || initialization.ValueKind != JsonValueKind.Object)
        {
            return "snapshot-initialization-absent";
        }
        if (!initialization.TryGetProperty("ExecutableHash", out JsonElement executable) || executable.GetString() != profile.ExecutableHash)
        {
            return "snapshot-executable-mismatch";
        }
        if (!initialization.TryGetProperty("Stage", out JsonElement stage) || stage.GetString() != nameof(NativeFieldInitializationStage.Completed))
        {
            return "snapshot-initialization-incomplete";
        }
        return null;
    }

    private static string? GetOrdinaryAutoPlayFieldBoundary(GameState state)
    {
        if (state.Scene != SceneKind.Field)
        {
            return "scene-not-field";
        }
        if (state.World is not null)
        {
            return "world-state";
        }
        if (state.Battle is not null)
        {
            return "battle-state";
        }
        if (state.PendingBattleEncounter is not null)
        {
            return "battle-encounter";
        }
        if (state.RandomEncounters?.Pending is not null)
        {
            return "random-encounter";
        }
        if (state.TripleTriadPlay is not null)
        {
            return "triple-triad-state";
        }
        if (state.NativeGameOver is not null)
        {
            return "game-over";
        }
        if (state.GlobalSoundProcess?.Spu?.AudioActive == true)
        {
            return "spu-audio-active";
        }
        if (state.MoviePlayback is { IsPlaying: true })
        {
            return "movie-active";
        }
        if (state.PendingEntityNames is { Length: > 0 })
        {
            return "entity-naming";
        }
        if (state.FieldRequestPacket?.Kind is not (null or 0))
        {
            return "request-kind";
        }
        return null;
    }

    private static bool IsNullSnapshotMember(JsonElement element, string name)
    {
        return !element.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null;
    }
}
