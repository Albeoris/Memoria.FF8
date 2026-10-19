using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleIfritEntryStep([property: JsonRequired] NativeBattleEffectMemory Memory, [property: JsonRequired] NativeBattleOriginalEffectState Effect, [property: JsonRequired] NativeBattleWaterSlotBank SlotBank, [property: JsonRequired] NativeBattleWaterTemporaryFrame TemporaryFrame, [property: JsonRequired] int CurrentHeaderIndex);

/// <summary>B25780's entry through the shared scripted-effect initialization, before its B25DF0 callback.</summary>
public static class NativeBattleIfritEntry
{
    public static NativeBattleIfritEntryStep InitializeB25780(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleScriptedInitializationDirectories directories, NativeBattleWaterInitializationInputs inputs)
    {
        NativeBattleCinematicEntryStep initialized = NativeBattleCinematicEntry.Initialize(NativeBattleCinematicProfile.ForEffect(201), memory, effect, directories, inputs);
        return new(initialized.Memory, initialized.Effect, initialized.SlotBank, initialized.TemporaryFrame, initialized.CurrentHeaderIndex);
    }
}
