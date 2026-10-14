using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeSalaryEffects(NativeSalaryPresentation? Presentation, NativeGlobalSoundProcess? SoundProcess, IReadOnlyList<GlobalSoundRequest> Requests);

public sealed partial class EventEngine
{
    private bool NeedsNativeSalaryTransitionReset()
    {
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("Native movie/battle entry requires its actual DWORD104 salary-display condition.");
        if ((controls.EventFlags104 & 0x10) != 0)
        {
            return false;
        }
        NativeSeeDState seeD = services.SeeD ?? throw new NotSupportedException("Native movie/battle entry requires the current signed salary countdown WORD184.");
        if (unchecked((short)seeD.SalaryDisplayCountdown184) <= 0)
        {
            return false;
        }
        if (services.SalaryPresentation is null || !services.CanStoreSalaryPresentation || !services.CanStoreNativeEventOwners)
        {
            throw new NotSupportedException("Native movie/battle salary UI reset requires its retained process words and atomic service.");
        }
        services.SalaryPresentation.Validate();
        return true;
    }

    private void ResetNativeSalaryForTransition(Fiber fiber)
    {
        if (NeedsNativeSalaryTransitionReset())
        {
            PublishNativeEventOwners(fiber, services.WorldControls!, seeD: services.SeeD! with { SalaryDisplayCountdown184 = 0 }, salaryPresentation: services.SalaryPresentation!.Reset());
        }
    }

    public NativeSalaryEffects PrepareSalaryEffects(NativeSalaryResult result, NativeSalaryPresentation? presentation, NativeGlobalSoundProcess? soundProcess)
    {
        List<GlobalSoundRequest> requests = [];
        if (result.Payment is NativeSalaryPayment payment)
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || presentation is null || soundProcess is null || !services.CanStoreSalaryPresentation || !services.CanStoreGlobalSoundProcess)
            {
                throw new NotSupportedException("Native salary payment requires the actual UI process and current sound allocator before publication.");
            }
            presentation = presentation.StartPayment(payment.OldRank, payment.NewRank, payment.OldPay, payment.NewPay);
            foreach (uint effect in new uint[] { 91, 92, 93 })
            {
                PreparedGlobalSoundEmission prepared = PrepareGlobalSound(effect, 0, 128, 127, soundProcess);
                soundProcess = prepared.Process;
                requests.Add(prepared.Request);
            }
        }
        if (result.CloseDisplay)
        {
            if (presentation is null || !services.CanStoreSalaryPresentation)
            {
                throw new NotSupportedException("Native salary countdown closure requires its retained presentation words.");
            }
            presentation = presentation.Close();
        }
        return new NativeSalaryEffects(presentation, soundProcess, requests);
    }

    public void PublishSalaryEffects(NativeSalaryEffects effects)
    {
        // The caller has already atomically published salary/economy/UI/audio owners.
        // These records notify transient output and retain historical attempts, never writable voice authority.
        foreach (GlobalSoundRequest request in effects.Requests)
        {
            Scene.GlobalSoundRequests.Add(request);
            AudioCommandExecuted?.Invoke(request);
        }
    }
}
