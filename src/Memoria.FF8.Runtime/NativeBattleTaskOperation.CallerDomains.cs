using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskOperation
{
    internal static void PreflightCallerDomains(NativeBattleLogicalOrdering? ordering, NativeBattleFileStaging? staging)
    {
        ordering?.RequirePublishedProducerPrefix();
        PreflightPublishedOrdering(ordering);
        if (staging is null)
        {
            return;
        }
        if (staging.IsSnapshot || staging.Transaction is not null || staging.WorkingOwner.HasUnpublishedOverlays)
        {
            throw new InvalidOperationException("Caller staging publication requires its clean working owner.");
        }
        NativeBattleFileStagingWorkingOwner owner = staging.WorkingOwner;
        owner.RetainedForOperations = true;
        EnlistRetainedStaging(owner);
        ValidateStagingPublication(owner, true);
    }

    internal static void AcceptCallerDomains(NativeBattleLogicalOrdering? ordering, NativeBattleFileStaging? staging, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            RememberPublishedOrdering(ordering);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
        if (staging is not null)
        {
            try
            {
                AcceptStagingPublication(staging.WorkingOwner, ref failure);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }
}
