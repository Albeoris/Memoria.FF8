using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private long? completedGameplayBattleFiber;

    /// <summary>Projects a completed battle's consumed caller without executing a source-domain callback or claiming a physical resource reload.</summary>
    public GameplaySaveFieldContinuation CaptureCompletedBattleGameplayContinuation(int combatCallbacks, byte nativeResult)
    {
        if (PendingMenuBattle is { SourceScene: SceneKind.Field } menu)
        {
            if (!SupportsNativeFieldReturnResult(nativeResult) || combatCallbacks != menu.Callbacks || Scene.FieldCallbackClock?.MenuBattleCallbacks != menu.CallbackBase + combatCallbacks || services is not GameSession { State.Battle: null, State.PendingBattleEncounter: null })
            {
                throw new InvalidDataException("Gameplay menu battle continuation disagrees with its completed encounter or callbacks.");
            }
            GameplaySaveFieldContinuation menuContinuation = CaptureLegacyGameplayContinuation();
            return menuContinuation with { Scene = menuContinuation.Scene with { BattleResult = nativeResult } };
        }
        if (PendingHostedBattle is null && Scene.NativeRandomFieldBattle is { Result: null, ReturnFrame: null, Abandonment: null } random)
        {
            if (!SupportsNativeFieldReturnResult(nativeResult) || combatCallbacks != random.Callbacks || Scene.FieldCallbackClock?.RandomBattleCallbacks != random.CallbackBase + combatCallbacks || services is not GameSession { State.Battle: null, State.PendingBattleEncounter: null, State.RandomEncounters.Pending: null })
            {
                throw new InvalidDataException("Gameplay random battle continuation disagrees with its completed encounter or callbacks.");
            }
            GameplaySaveFieldContinuation randomContinuation = CaptureLegacyGameplayContinuation();
            return randomContinuation with { Scene = randomContinuation.Scene with { BattleResult = nativeResult } };
        }
        (NativeBattleCompletion request, _) = ValidateHostedBattleReturn(combatCallbacks, nativeResult);
        if (completedGameplayBattleFiber is not null || !SupportsNativeFieldReturnResult(nativeResult) || request.Backend != NativeBattleBackend.Interactive || services is not GameSession { State.Battle: null, State.PendingBattleEncounter: null } || Scene.NativeBattleWaits is not { Count: 1 } waits || Scene.NativeBattleCompletions is null)
        {
            throw new InvalidOperationException("Gameplay battle continuation requires one completed and committed ordinary battle owner.");
        }
        KeyValuePair<long, int> wait = waits.Single();
        Fiber fiber = fibers.Single(item => item.Sequence == wait.Key);
        if (wait.Value != Scene.NativeBattleCompletions.IndexOf(request) || fiber.Complete || fiber.WaitReason != WaitReason.MinimalBattle || fiber.ObjectId != request.ObjectId || fiber.Script.Id != request.ScriptId || fiber.Position != request.Operation + 1 || !fiber.Script.ContainsOperation(request.Operation) || program!.Operations[request.Operation].Opcode != Jsm.Opcode.BATTLE || fiber.WaitUntil != request.Frame + 1)
        {
            throw new InvalidDataException("Gameplay battle continuation lost its consumed BATTLE instruction or original caller wait.");
        }
        completedGameplayBattleFiber = fiber.Sequence;
        try
        {
            GameplaySaveFieldContinuation continuation = CaptureLegacyGameplayContinuation();
            return continuation with { Scene = continuation.Scene with { BattleResult = nativeResult } };
        }
        finally
        {
            completedGameplayBattleFiber = null;
        }
    }
}
