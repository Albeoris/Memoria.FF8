namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    /// <summary>Installs the saved logical menu entry location without running a menu callback or advancing time.</summary>
    public void RestoreGameplayMenuLocation(NativeFieldLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        location.Validate();
        if (location.Origin != NativeFieldLocationOrigin.GameplayContinuation || State.Scene != SceneKind.Field || State.FieldId != location.FieldId || State.FieldLoadContext?.Origin != NativeFieldLoadOrigin.GameplayContinuation || State.EventState is not null || State.Battle is not null || State.PendingBattleEncounter is not null)
        {
            throw new InvalidOperationException("Gameplay menu location requires a fresh matching logical field continuation.");
        }
        State = State with { NativeFieldLocation = location };
    }
}
