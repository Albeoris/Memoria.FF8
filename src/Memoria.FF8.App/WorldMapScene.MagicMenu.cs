using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    public Func<NativeCharacterVitals, int, BattleNativePlayerStatRules>? ReadFieldMagicStatRules { get; set; }
    private PlayerActionResult ApplyMagicMenuAction(PlayerAction action)
    {
        NativeMagicMenuInput.Validate(action);
        if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, new PlayerTrigger(HasPendingBattle?.Invoke() == true ? PlayerTriggerKind.Battle : PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, CurrentDialogue?.MessageId, CurrentDialogue?.Text), 0, "The current world interaction suspends the magic menu.");
        }
        NativeCharacterVitals source = session.State.CharacterVitals ?? throw new NotSupportedException("Magic input requires authentic saved characters.");
        BattleNativePlayerStatRules? rules = action.Kind == PlayerActionKind.UseFieldMagic ? ReadFieldMagicStatRules?.Invoke(source, NativeMagicMenuInput.Validate(action).Source) : null;
        session.SetCharacterVitals(NativeMagicMenuInput.Apply(source, action, rules, session.State.WorldControls));
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
    }
}
