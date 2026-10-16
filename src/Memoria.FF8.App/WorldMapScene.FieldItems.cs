using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    public PlayerActionResult UseFieldItem(PlayerAction action, NativeFieldItem item, Func<NativeCharacterVitals, int, BattleNativePlayerStatRules> statRules)
    {
        if (action.Value is <= 0 or > 255 || action.Quantity is null or < 0 or >= 8 and < 16 or > 31 || action.Text is not null && (item.Type != 18 || !int.TryParse(action.Text, out int forgotten) || forgotten is < 0 or > 127) || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("World item use requires an item BYTE and a character0..7 or GF16..31 target.", nameof(action));
        }
        if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, new PlayerTrigger(HasPendingBattle?.Invoke() == true ? PlayerTriggerKind.Battle : PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, CurrentDialogue?.MessageId, CurrentDialogue?.Text), 0, "The current world interaction suspends item use.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("World item use requires authentic saved characters.");
        NativeEconomy economy = session.State.Economy ?? throw new NotSupportedException("World item use requires authentic saved inventory.");
        NativeLimitProgress? progress = null;
        (NativeCharacterVitals Vitals, NativeEconomy Economy, bool Changed) next;
        if (item.Type == 19)
        {
            var learned = item.LearnBlueMagic(vitals, economy, session.State.LimitProgress ?? throw new NotSupportedException("Blue Magic items require saved Limit progression."), action.Value, action.Quantity.Value);
            next = (vitals, learned.Economy, learned.Changed);
            progress = learned.Progress;
        }
        else
        {
            next = item.Use(vitals, economy, action.Value, action.Quantity.Value, item.Type == 20 ? statRules(vitals, action.Quantity.Value) : null, action.Text is null ? null : int.Parse(action.Text, System.Globalization.CultureInfo.InvariantCulture));
        }
        session.SetNativePartyMenuOwners(next.Vitals, next.Economy, session.State.ShotAvailability!, session.State.SalaryPresentation!, limitProgress: progress);
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
    }
}
