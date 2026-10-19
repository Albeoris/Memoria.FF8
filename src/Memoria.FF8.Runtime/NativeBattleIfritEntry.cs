using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleIfritEntryStep([property: JsonRequired] NativeBattleEffectMemory Memory, [property: JsonRequired] NativeBattleOriginalEffectState Effect, [property: JsonRequired] NativeBattleWaterSlotBank SlotBank, [property: JsonRequired] NativeBattleWaterTemporaryFrame TemporaryFrame, [property: JsonRequired] int CurrentHeaderIndex);

/// <summary>B25780's entry through the shared scripted-effect initialization, before its B25DF0 callback.</summary>
public static class NativeBattleIfritEntry
{
    public static NativeBattleIfritEntryStep InitializeB25780(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleScriptedInitializationDirectories directories, NativeBattleWaterInitializationInputs inputs)
    {
        effect.Validate();
        directories.Validate();
        if (effect.Binding is not { Effect: 201, Entry: 0xB25780, Initializer: 0xB25750 } || effect.Parameters is not { } parameters || inputs.ActionParametersAddress != 0x1D99A78 || inputs.InitializationDirectoryAddress != 0x1874894 || inputs.Caster != parameters.Actor78 || inputs.CurrentHeaderIndex != parameters.HeaderIndex7C)
        {
            throw new InvalidDataException("Native Ifrit entry requires its selected201 initializer and actual action, header and initialization-directory bindings.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Ifrit entry requires its current effect arena.");
        NativeBattleScriptedEffectMemory core = memory.ScriptedEffects ?? throw new NotSupportedException("Native Ifrit entry requires its retained shared scripted-effect core.");
        RequireResource(0x2796E4C, inputs.Resource0);
        RequireResource(0x2796E48, inputs.Resource1);
        NativeBattleWaterInitializationResult initialized = NativeBattleWaterInitialization.InitializeScriptedEntry(core, arena, directories, inputs, new(0x2796E18, 0x2796E28, 0x10, 1, 0xB25DF0));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2796E18, 0x2796E28, 0x10, 1);
        (pool, _) = pool.Append(0xB25DF0);
        return new(memory with { Arena = initialized.Arena, ScriptedEffects = initialized.Core, Ifrit201 = new(initialized.TemporaryFrame.Bytes[0x145]) }, effect with { Pools = [pool], ReturnedDescriptor = pool.DescriptorAddress }, initialized.SlotBank, initialized.TemporaryFrame, initialized.CurrentHeaderIndex);

        void RequireResource(uint publication, NativeBattleWaterInitializationResource resource)
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == publication) ?? throw new NotSupportedException("Native Ifrit entry requires both ordered executed initializer publications.");
            if (pointer.ArenaOffset != resource.ArenaOffset)
            {
                throw new InvalidDataException("Native Ifrit entry resource input disagrees with its executed initializer publication.");
            }
        }
    }
}
