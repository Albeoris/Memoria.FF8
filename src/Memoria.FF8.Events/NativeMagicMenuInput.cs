using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativeMagicMenuInput
{
    public static bool IsMenuAction(PlayerActionKind kind) => kind is PlayerActionKind.UseFieldMagic or PlayerActionKind.TransferMagic;

    public static (int Source, int Spell, int Target) Validate(PlayerAction action)
    {
        string[] fields = action.Text?.Split(':') ?? [];
        if (!IsMenuAction(action.Kind) || fields.Length != 3 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int source) || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int spell) || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int target) || (uint)source >= 8 || (uint)target >= 8 || spell is < 1 or > 56 || action.Value is < 1 or > 100 || action.Kind == PlayerActionKind.UseFieldMagic && action.Value != 1 || action.Quantity is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("Magic input requires explicit physical source/spell/target identities, a valid quantity and one ordinary menu action.", nameof(action));
        }
        return (source, spell, target);
    }

    public static NativeCharacterVitals Apply(NativeCharacterVitals vitals, PlayerAction action, BattleNativePlayerStatRules? sourceRules = null, NativeWorldControlState? controls = null, Func<int, BattleNativePlayerStatRules>? readRules = null)
    {
        var selection = Validate(action);
        int source = selection.Source;
        if (action.Kind == PlayerActionKind.UseFieldMagic && vitals.MagicSystem.SharedMagicStock)
        {
            source = vitals.ResolveFieldMagicCaster(source, selection.Spell, readRules ?? (_ => sourceRules ?? throw new NotSupportedException("Shared field casting requires current actor statistics.")), controls) ?? throw new InvalidOperationException("No eligible field caster can use this shared spell.");
            if (readRules is not null)
            {
                sourceRules = readRules(source);
            }
        }
        if (!vitals.IsMenuCharacterAvailable(source, controls) || !vitals.IsMenuCharacterAvailable(selection.Target, controls))
        {
            throw new InvalidOperationException("Magic input selected a character outside the current native menu roster.");
        }
        return action.Kind == PlayerActionKind.UseFieldMagic ? vitals.CastFieldMagic(source, selection.Spell, selection.Target, sourceRules ?? throw new NotSupportedException("Field magic requires the caster's authenticated kernel statistic inputs."), out _) : vitals.TransferMagic(selection.Source, selection.Spell, selection.Target, action.Value, out _);
    }
}

public sealed partial class EventEngine
{
    public Func<NativeCharacterVitals, int, BattleNativePlayerStatRules>? ReadFieldMagicStatRules { get; set; }

    private bool ApplyMagicMenuAction(PlayerAction action)
    {
        NativeMagicMenuInput.Validate(action);
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        NativeCharacterVitals source = services.CharacterVitals ?? throw new NotSupportedException("Magic input requires authentic saved characters.");
        BattleNativePlayerStatRules? rules = action.Kind == PlayerActionKind.UseFieldMagic ? ReadFieldMagicStatRules?.Invoke(source, NativeMagicMenuInput.Validate(action).Source) : null;
        NativeCharacterVitals next = NativeMagicMenuInput.Apply(source, action, rules, services.WorldControls, ReadFieldMagicStatRules is null ? null : row => ReadFieldMagicStatRules(source, row));
        if (activeMenu)
        {
            PublishPartyMenuCallback(next, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
            partyMenu = partyMenu! with { Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetCharacterVitals(next);
        }
        return true;
    }
}
