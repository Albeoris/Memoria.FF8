using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record EffectComponentPreviewSource(BattleSession Battle, string Sha256);

internal sealed partial class GameWorld
{
    internal bool IsEffectComponentPreview { get; private set; }
    internal BattleDiagnosticComponentAdmission? ComponentSourceAdmission { get; private set; }

    private BattleSession StartEffectComponentPreview(EffectPreviewRequest request, EffectComponentPreviewSource component)
    {
        if (battle is not null || session.State.PendingBattleEncounter is not null || diagnosticBattle is not null)
        {
            throw new InvalidOperationException("A component preview requires an unallocated standalone host.");
        }
        if (request.EffectId is not ushort effect || component.Sha256 is not { Length: 64 } || !component.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("A private component preview requires its native effect and scenario input SHA256.");
        }
        _ = ValidateEffectComponentSource(component.Battle, effect);
        string executable = component.Battle.ReadDefinition().NativeRetainedMemory?.ColdExecutableSha256 ?? throw new InvalidDataException("The component source lacks its authenticated executable owner.");
        if (!string.Equals(executable, assets.NativeExecutableHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The component source executable differs from the installed resources.");
        }
        NativeBattleEffectBinding installed = assets.ReadNativeEffectCatalogForEffects([effect]).Single(binding => binding.Effect == effect);
        NativeBattleEffectBinding supplied = component.Battle.Presentation!.ReadDiagnosticComponentResources(effect);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(installed), JsonSerializer.SerializeToElement(supplied)))
        {
            throw new InvalidDataException("The component source differs from the installed native effect binding and resources.");
        }
        BattleSession active = component.Battle.ForkForContinuation();
        BattleDiagnosticComponentAdmission admission = ValidateEffectComponentSource(active, effect);
        long callbacks = active.NativeHostCallbacks ?? throw new NotSupportedException("Component preview requires its restored native callback ledger.");
        IsEffectComponentPreview = true;
        ComponentSourceAdmission = admission.ModuleLoaded ? admission : null;
        battle = active;
        synchronizedNativeBattleCallbacks = callbacks;
        session.SetLiveBattle(active);
        Events.SynchronizeNativeBattleMusicProcess(active.NativeMusicProcess);
        return active;
    }

    internal static BattleDiagnosticComponentAdmission ValidateEffectComponentSource(BattleSession active, ushort expectedEffect)
    {
        if (active.Outcome != BattleOutcome.Active || active.Presentation is not { } process)
        {
            throw new InvalidDataException("A component source requires an active canonical battle presentation.");
        }
        return process.ReadDiagnosticComponentAdmission(expectedEffect);
    }

    private void AdvanceEffectComponentClock(BattleSession active, int callbacks)
    {
        if (active.NativeExamTimer is { } retained)
        {
            session.SetExamTimer(retained);
        }
        else
        {
            session.SetExamTimer((session.ExamTimer ?? throw new InvalidDataException("Component presentation lost its standalone transport clock.")).AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks));
        }
    }
}
