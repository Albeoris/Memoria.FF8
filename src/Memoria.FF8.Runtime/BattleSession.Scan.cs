namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeScannedModels? nativeScannedModels;

    private void ResolveNativeScan(BattleCombatant actor, BattleCombatant recipient)
    {
        NativeScannedModels before = nativeScannedModels ?? throw new NotSupportedException("Native Scan requires the current saved160-bit model history; an old unknown bank must be explicitly migrated.");
        int model = recipient.ModelId;
        bool known = before.Contains(model);
        bool show = !known || (definition.NativeScanAlwaysShow ?? throw new NotSupportedException("Repeated native Scan requires the actual saved options DWORD1CFE73C bit100."));
        nativeScannedModels = before.Add(model);
        if (!show)
        {
            MarkNativeContactFlags(2, 0);
        }
        if (show)
        {
            Log("Scan", actor.Id, recipient.Id, 0, $"{recipient.Name} Lv {recipient.Level} HP {recipient.CurrentHp}/{recipient.MaximumHp}.");
        }
    }

    private void RestoreNativeScannedModels(NativeScannedModels? saved)
    {
        NativeScannedModels.ValidateReceipt(definition.NativeScannedModels, saved);
        nativeScannedModels = saved;
    }
}
