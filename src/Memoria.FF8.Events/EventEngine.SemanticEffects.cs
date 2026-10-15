using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private uint? semanticSalaryMovementUnits;
    public bool CanApplySemanticEffect => CanRunSemanticSalaryCallback();

    /// <summary>Runs a retained historical salary input through the current ordinary callback and salary producer, without restoring an old result.</summary>
    public PlayerActionResult ApplySemanticEffect(ReplayJournalEntry effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        effect.Validate();
        int[]? arguments = effect.EffectArguments ?? effect.Receipts.SingleOrDefault(receipt => receipt.Kind == ReplayReceiptKind.PersistentEffect)?.Arguments;
        if (effect.Kind != ReplayEntryKind.PersistentEffect || effect.Scene != SceneKind.Field || effect.FieldId != FieldId || (ReplayPersistentEffectKind)effect.Code is not (ReplayPersistentEffectKind.SalaryProgress or ReplayPersistentEffectKind.SalaryPayment) || arguments is not [0 or 3 or 5, >= 1 and <= 30000] || semanticSalaryMovementUnits is not null)
        {
            throw new NotSupportedException("Historical field effects require an explicit supported salary input and callback count for this field.");
        }
        int callbacks = checked(effect.Frames * arguments[1]);
        if (callbacks > 30000)
        {
            throw new NotSupportedException("Historical salary effect count exceeds the bounded callback input.");
        }
        int[] cause = [arguments[0], 1];
        ReplayJournalEntry input = ReplayBoundary(ReplayEntryKind.PersistentEffect) with { Code = effect.Code, Frames = callbacks, EffectArguments = cause, NativeSourceHash = programHash };
        return RunReplay(input, () =>
        {
            if (!CanRunSemanticSalaryCallback())
            {
                return new PlayerActionResult(false, NextTrigger, 0, "The current field cannot admit a historical salary callback.");
            }
            long start = Frame;
            semanticSalaryMovementUnits = (uint)cause[0];
            try
            {
                for (int callback = 0; callback < callbacks; callback++)
                {
                    if (!CanRunSemanticSalaryCallback())
                    {
                        throw new NotSupportedException("Historical salary callbacks reached an external decision, departure or unsupported current field owner.");
                    }
                    PlayerTrigger before = NextTrigger;
                    StepCore();
                    RecordReplayProgress(before);
                }
            }
            finally
            {
                semanticSalaryMovementUnits = null;
            }
            return new PlayerActionResult(true, NextTrigger, checked((int)(Frame - start)));
        });
    }

    private bool CanRunSemanticSalaryCallback()
    {
        return program is not null && defaultsStarted && NativeRuntimeProfile is { IsVerified: true } && services.ReplayScene == SceneKind.Field && !transitionRequested && LastFault is null && !IsDialogueOpen && PartyMenu is null && PendingInteractiveBattle is null && !services.IsMoviePlaying && services.SeeD is not null && Scene.NativeSalaryMovementUnits is 0 or 3 or 5;
    }
}
