using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeScannedModels? nativeScannedModels;
    private BattleScanKnowledge scanKnowledge = new();

    public BattleScanKnowledge ScanKnowledge => scanKnowledge;

    private void ResolveNativeScan(BattleCombatant actor, BattleCombatant recipient)
    {
        NativeScannedModels? history = actor.IsPlayer ? nativeScannedModels ?? throw new NotSupportedException("Native Scan requires the current saved160-bit model history; an old unknown bank must be explicitly migrated.") : null;
        BattleObserverSide observer = actor.IsPlayer ? BattleObserverSide.Party : BattleObserverSide.Enemy;
        scanKnowledge = scanKnowledge.Observe(new(observer, recipient.Id, actor.Id, Frame, recipient.ElementDefenses.Select(BattleKnownAffinity.FromNativeDefense).ToImmutableArray()));
        if (!actor.IsPlayer)
        {
            Log("Scan", actor.Id, recipient.Id, 0, $"{recipient.Name} scanned.");
            return;
        }
        NativeScannedModels before = history!;
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

    private void RestoreScanKnowledge(BattleScanKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        knowledge.Validate();
        if (knowledge.Observations.Any(observation => observation.Frame > Frame || !byId.TryGetValue(observation.TargetId, out _) || !byId.TryGetValue(observation.ScannerId, out BattleCombatant? scanner) || observation.Observer != (scanner.IsPlayer ? BattleObserverSide.Party : BattleObserverSide.Enemy)))
        {
            throw new InvalidDataException("Scan knowledge references an unknown allocation, observer or future capture frame.");
        }
        scanKnowledge = knowledge;
    }

    private void RestoreNativeScannedModels(NativeScannedModels? saved)
    {
        NativeScannedModels.ValidateReceipt(definition.NativeScannedModels, saved);
        nativeScannedModels = saved;
    }
}
