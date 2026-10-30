using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private DiagnosticSlotsSource PrepareDiagnosticSlotsSource(BattleSession battle, DiagnosticBattleRequest request)
    {
        DiagnosticSlotsDeclaration declaration = request.Driver!.SlotsAdmission!;
        string actorId = request.Driver.Input.ActorId!;
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == actorId);
        BattleNativeLimitDefinition original = BattleResources.ReadNativeLimitDefinition(5);
        if (!actor.IsPlayer || actor.ModelId != 5 || actor.NativeLimit is not { CharacterId: 5, Command: 16 } || JsonSerializer.Serialize(actor.NativeLimit) != JsonSerializer.Serialize(original) || battle.SelphieCardSlots)
        {
            throw new NotSupportedException("Slots diagnostics require original Selphie and ordinary Slot rules.");
        }
        byte index = checked((byte)(battle.ObserveDiagnosticCombatants().Single(candidate => candidate.Id == actorId).NativeIndex ?? throw new NotSupportedException("Slots source requires its actual physical party index.")));
        ImmutableArray<byte> Row(int section, int row) => BattleResources.NativeKernelRecords.Single(record => record.Section == section && record.Index == row).Bytes;
        ImmutableArray<byte> Table(int section) => BattleResources.NativeKernelRecords.Where(record => record.Section == section).OrderBy(record => record.Index).SelectMany(record => record.Bytes).ToImmutableArray();
        BattleNativeAction action = BattleResources.NativeActions.Single(candidate => candidate.Command == 16 && candidate.ActionId == declaration.MagicAction);
        if (action.KernelSection != 1 || action.KernelIndex != declaration.MagicAction || action.MagicId != request.Driver.ExpectedEffect || !action.RawRecord.AsSpan().SequenceEqual(Row(1, declaration.MagicAction).AsSpan()))
        {
            throw new InvalidDataException("Slots source action differs from the original installed kernel row.");
        }
        DiagnosticSlotsSource source = new(diagnosticBattle!.InstanceId, battle.DiagnosticLifecycleId, actorId, index, declaration.MagicAction, battle.Capture().Definition.LimitProgress?.SpecialMagicFlags ?? throw new NotSupportedException("Slots source requires its initialized saved learning owner."), original, Row(6, 5), Table(26), Table(27), Row(29, 0), action.RawRecord, "");
        source = source with { Fingerprint = source.ComputeFingerprint() };
        source.Validate();
        return source;
    }
}
