using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public GameplaySaveFieldContinuation CaptureGameplaySaveFieldContinuation()
    {
        if (services is not GameSession session || session.State.Scene != SceneKind.Field || session.State.FieldId != FieldId || session.State.Battle is not null || session.State.PendingBattleEncounter is not null || NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Gameplay field continuation requires a current supported field without an active or pending battle.");
        }
        PreparingCheckpoint?.Invoke();
        GameplaySaveFieldScripts scripts = CaptureGameplaySaveScripts();
        GameplaySaveFieldScene scene = CaptureGameplaySaveFieldScene();
        GameplaySaveFieldActors logicalActors = CaptureGameplaySaveFieldActors();
        GameplaySaveFieldContinuation result = new(1, scripts, scene, logicalActors);
        return result.Detach();
    }

    public void RestoreGameplaySaveFieldContinuation(GameplaySaveFieldContinuation continuation, GameplaySaveSceneFacts facts)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(facts);
        continuation.Validate();
        facts.Validate();
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || continuation.Scripts.ProgramSha256 != programHash || continuation.Scripts.FieldId != FieldId || facts.FieldId != FieldId || facts.Origin != GameplaySaveSceneFactsOrigin.Field || defaultsStarted || Frame != 0 || services is not GameSession { State.FieldLoadContext.Origin: NativeFieldLoadOrigin.GameplayContinuation })
        {
            throw new InvalidOperationException("Gameplay continuation requires its matching current program and a fresh explicit continuation allocation.");
        }
        GameplaySaveFieldContinuation saved = continuation.Detach();
        RestoreGameplaySaveFieldScene(saved.Scene, saved.Scripts, facts);
        RestoreGameplaySaveFieldActors(saved.Actors);
        RestoreGameplaySaveScripts(saved.Scripts);
        ValidateSceneMemoryOwners(EventMemory, Scene);
    }
}
