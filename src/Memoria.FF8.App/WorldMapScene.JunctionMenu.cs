using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    public Func<NativeCharacterVitals, int, int, NativeCharacterVitals>? ResolveNativeAutoJunction { get; set; }

    private PlayerActionResult ApplyJunctionMenuAction(PlayerAction action)
    {
        NativeJunctionMenuInput.Validate(action);
        if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, new PlayerTrigger(HasPendingBattle?.Invoke() == true ? PlayerTriggerKind.Battle : PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, CurrentDialogue?.MessageId, CurrentDialogue?.Text), 0, "The current world interaction suspends the main menu.");
        }
        NativeCharacterVitals source = session.State.CharacterVitals ?? throw new NotSupportedException("Menu input requires authentic saved characters.");
        NativeCharacterVitals next = NativeJunctionMenuInput.Apply(source, action, ResolveNativeAutoJunction);
        session.SetCharacterVitals(next);
        PlayerActionResult result = new(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
        return result;
    }
}
