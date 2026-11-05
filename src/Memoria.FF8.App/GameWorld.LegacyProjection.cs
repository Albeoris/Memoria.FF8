using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly bool legacyGameplayProjection;

    internal GameplaySaveComponentDocument CaptureLegacyGameplayDocument()
    {
        if (!legacyGameplayProjection || session.State.Battle is not null || session.State.PendingBattleEncounter is not null)
        {
            throw new InvalidOperationException("Legacy gameplay projection requires its isolated nonbattle extraction host.");
        }
        if (session.State.Scene == SceneKind.WorldMap)
        {
            return CaptureGameplaySave();
        }
        if (session.State.Scene != SceneKind.Field || !HasCurrentFieldAllocation)
        {
            throw new NotSupportedException("Legacy gameplay projection requires its current logical field or world.");
        }
        GameplaySaveFieldContinuation continuation = Events.CaptureLegacyGameplayContinuation();
        GameState prepared = session.PrepareSnapshot();
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.Field, prepared.FieldId, prepared.PreviousFieldId, continuation.GetLeaderPosition(), null);
        GameplaySaveComponentDocument document = CaptureGameplayDocument(prepared, resume);
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        document.Validate();
        return document;
    }
}
