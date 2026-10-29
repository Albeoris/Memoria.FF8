using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticShotCycleDeclaration([property: JsonRequired] byte AmmoAction, [property: JsonRequired] ushort OpeningEffect, [property: JsonRequired] ushort EndingEffect)
{
    public ushort ExpectedHeaderAction(byte command)
    {
        if (AmmoAction > 7)
        {
            throw new InvalidDataException("Shot header identity requires its original ammo0..7 index.");
        }
        return command switch
        {
            14 => checked((ushort)(101 + AmmoAction)),
            237 or 238 => AmmoAction,
            _ => throw new InvalidDataException("Shot header identity requires command14,237 or238.")
        };
    }

    public void Validate(BattleInput input, ushort effect, int? nativeAction, byte? nativeCommand)
    {
        if (AmmoAction > 7 || OpeningEffect == 0 || EndingEffect == 0 || effect == 0 || input.Kind != BattleInputKind.Action || input.Ability != BattleAbility.LimitBreak || input.ActionId != (14 << 16 | AmmoAction) || nativeAction != AmmoAction || nativeCommand != 237 || string.IsNullOrWhiteSpace(input.ActorId) || string.IsNullOrWhiteSpace(input.TargetId))
        {
            throw new InvalidDataException("Shot diagnostics require the real command14 selection, matching ammo0..7 and one command237 focal invocation.");
        }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticShotObservation(long Callback, int Inventory, byte LearnedFlags, BattleNativeShotState? Shot);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticShotCycleReceipt(int Execution, string ActorId, string TargetId, byte ActorIndex, byte TargetIndex, DiagnosticShotCycleDeclaration Declaration, ushort FiringEffect, BattleDiagnosticLimitSelectionReceipt Selection, DiagnosticShotObservation BeforeOpening)
{
    public DiagnosticShotObservation? Opening { get; init; }
    public DiagnosticShotObservation? ReadyToFire { get; init; }
    public DiagnosticShotObservation? Triggered { get; init; }
    public DiagnosticShotObservation? ReadyToEnd { get; init; }
    public DiagnosticShotObservation? Ending { get; init; }
    public DiagnosticShotObservation? Completed { get; init; }
    public BattleDiagnosticEffectDispatch[] Dispatches { get; init; } = [];
}

/// <summary>Checks one ordinary opening, one accepted shot and explicit widget cancellation without accepting arbitrary supporting effects.</summary>
public static class DiagnosticShotCycleValidation
{
    public static void ValidateReceipt(DiagnosticShotCycleReceipt receipt, BattleInput input, ushort effect, BattleDiagnosticEffectDispatch[] dispatches, long startCallback, long endCallback)
    {
        receipt.Declaration.Validate(input, effect, receipt.Declaration.AmmoAction, 237);
        DiagnosticShotObservation[] points = [receipt.BeforeOpening, receipt.Opening ?? Missing(), receipt.ReadyToFire ?? Missing(), receipt.Triggered ?? Missing(), receipt.ReadyToEnd ?? Missing(), receipt.Ending ?? Missing(), receipt.Completed ?? Missing()];
        byte ammo = receipt.Declaration.AmmoAction;
        BattleDiagnosticLimitSelectionReceipt selection = receipt.Selection;
        if (receipt.Execution is < 1 or > 2 || receipt.ActorId != input.ActorId || receipt.TargetId != input.TargetId || receipt.ActorIndex > 2 || receipt.TargetIndex is < 3 or > 10 || receipt.FiringEffect != effect || !selection.Accepted || selection.ActorId != receipt.ActorId || selection.CommandOwner != 2 || selection.Command != 14 || selection.CrisisSelections.IsDefaultOrEmpty || selection.CrisisSelections.Length > 256 || selection.CrisisSelections.Any(crisis => crisis > 4) || selection.RandomBefore is null || selection.RandomAfter is null || selection.CrisisSelections[^1] is < 1 or > 4 || receipt.BeforeOpening.Inventory < (receipt.Execution == 1 ? 3 : 2))
        {
            throw new InvalidDataException("Shot cycle admission or finite ammunition ownership is invalid.");
        }
        selection.RandomBefore!.Validate();
        selection.RandomAfter!.Validate();
        for (int index = 0; index < points.Length; index++)
        {
            if (points[index].Callback < startCallback || points[index].Callback > endCallback || index > 0 && points[index].Callback < points[index - 1].Callback || points[index].Inventory != points[0].Inventory - (index >= 3 ? 1 : 0) || index < 3 && points[index].LearnedFlags != points[0].LearnedFlags || index >= 3 && points[index].LearnedFlags != points[0].LearnedFlags && points[index].LearnedFlags != (byte)(points[0].LearnedFlags | 1 << ammo))
            {
                throw new InvalidDataException("Shot cycle callback or ammunition lineage is invalid.");
            }
        }
        if (points[0].Shot is not null || points[6].Shot is not null || points[1].Shot?.Phase != BattleNativeShotPhase.Opening || points[2].Shot?.Phase != BattleNativeShotPhase.Ready || points[3].Shot?.Phase != BattleNativeShotPhase.AwaitingEffect || points[4].Shot?.Phase != BattleNativeShotPhase.Ready || points[5].Shot?.Phase != BattleNativeShotPhase.Ending || points[6].LearnedFlags != (byte)(points[0].LearnedFlags | 1 << ammo))
        {
            throw new InvalidDataException("Shot cycle does not prove the real opening/ready/trigger/ready/ending/completion route.");
        }
        BattleNativeShotState opening = points[1].Shot!;
        for (int index = 1; index <= 5; index++)
        {
            BattleNativeShotState state = points[index].Shot!;
            if (state.ActionId != ammo || state.TargetMask != opening.TargetMask || (state.TargetMask & (1 << receipt.TargetIndex)) == 0 || state.InitialTimer <= 0 || state.InitialTimer != opening.InitialTimer || state.RemainingAmmo != points[index].Inventory || state.Phase != BattleNativeShotPhase.Ending && state.RemainingTimer <= 0 || index > 1 && state.RemainingTimer > points[index - 1].Shot!.RemainingTimer || state.Phase == BattleNativeShotPhase.Ending && state.RemainingTimer != -1)
            {
                throw new InvalidDataException("Shot cycle changed its real process, target, timer or ammunition owner.");
            }
        }
        if (points[1].Shot!.RemainingTimer != opening.InitialTimer || points[2].Callback >= points[4].Callback || points[6].Callback <= points[5].Callback || receipt.Dispatches.Length != 3 || dispatches.Length != 3)
        {
            throw new InvalidDataException("Shot cycle requires ordered control/firing dispatches and native completion.");
        }
        byte[] commands = [14, 237, 238];
        ushort[] effects = [receipt.Declaration.OpeningEffect, effect, receipt.Declaration.EndingEffect];
        HashSet<(int Header, long Generation)> headers = [];
        for (int index = 0; index < 3; index++)
        {
            BattleDiagnosticEffectDispatch dispatch = dispatches[index];
            DiagnosticShotObservation lower = points[index == 0 ? 1 : index == 1 ? 3 : 5];
            DiagnosticShotObservation upper = points[index == 0 ? 2 : index == 1 ? 4 : 6];
            if (dispatch != receipt.Dispatches[index] || dispatch.Reflection is not null || dispatch.ActorId != receipt.ActorId || dispatch.Ability != BattleAbility.LimitBreak || dispatch.ActionId != receipt.Declaration.ExpectedHeaderAction(commands[index]) || dispatch.NativeCommand != commands[index] || dispatch.RequestedEffect != effects[index] || dispatch.ExecutedEffect != effects[index] || dispatch.NativeActorIndex != receipt.ActorIndex || dispatch.NativeTargetMask != (index == 2 ? 0x80F8 : opening.TargetMask) || dispatch.NativeHeaderIndex is not int header || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || !headers.Add((header, generation)) || dispatch.Callback < lower.Callback || dispatch.Callback > upper.Callback || index > 0 && (dispatch.Serial <= dispatches[index - 1].Serial || dispatch.Callback < dispatches[index - 1].Callback))
            {
                throw new InvalidDataException("Shot cycle dispatch identity, stage or header lineage is invalid.");
            }
        }
    }

    public static void ValidateChain(DiagnosticShotCycleReceipt first, DiagnosticShotCycleReceipt second)
    {
        if (first.Execution != 1 || second.Execution != 2 || first.Completed is not { Shot: null } completed || first.ActorId != second.ActorId || first.TargetId != second.TargetId || first.ActorIndex != second.ActorIndex || first.TargetIndex != second.TargetIndex || first.Declaration != second.Declaration || first.FiringEffect != second.FiringEffect || first.Opening?.Shot?.TargetMask != second.Opening?.Shot?.TargetMask || second.BeforeOpening.Shot is not null || second.BeforeOpening.Inventory != completed.Inventory || second.BeforeOpening.LearnedFlags != completed.LearnedFlags || second.BeforeOpening.Callback < completed.Callback || first.Dispatches.Length != 3 || second.Dispatches.Length != 3 || second.Dispatches[0].Serial <= first.Dispatches[^1].Serial || first.Dispatches.Any(previous => second.Dispatches.Any(current => current.NativeHeaderIndex == previous.NativeHeaderIndex && current.NativeHeaderGeneration == previous.NativeHeaderGeneration)))
        {
            throw new InvalidDataException("The second Shot cycle did not preserve the first live cycle's inventory, learning and dispatch lineage.");
        }
    }

    private static DiagnosticShotObservation Missing() => throw new InvalidDataException("Shot cycle stage observation is missing.");
}
