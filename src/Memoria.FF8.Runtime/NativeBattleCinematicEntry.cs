namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicEntryStep(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleWaterSlotBank SlotBank, NativeBattleWaterTemporaryFrame TemporaryFrame, int CurrentHeaderIndex);

/// <summary>Selected cinematic entry over the shared initialization and single physical callback pool.</summary>
public static class NativeBattleCinematicEntry
{
    public static NativeBattleCinematicEntryStep Initialize(NativeBattleCinematicProfile profile, NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleScriptedInitializationDirectories directories, NativeBattleWaterInitializationInputs inputs)
    {
        RequireBinding(profile, effect);
        effect.Validate();
        directories.Validate();
        if (effect.Parameters is not { } parameters || inputs.ActionParametersAddress != 0x1D99A78 || inputs.InitializationDirectoryAddress != profile.InitializationAddress || inputs.Caster != parameters.Actor78 || inputs.CurrentHeaderIndex != parameters.HeaderIndex7C)
        {
            throw new InvalidDataException("Native cinematic entry requires its actual action, header and selected initialization-directory bindings.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native cinematic entry requires its current effect arena.");
        NativeBattleScriptedEffectMemory core = memory.ScriptedEffects ?? throw new NotSupportedException("Native cinematic entry requires its retained shared scripted-effect core.");
        RequireResource(profile.File0Publication, inputs.Resource0);
        RequireResource(profile.File1Publication, inputs.Resource1);
        if (profile.Effect == 203)
        {
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic debug publication requires its retained primitive process.");
            memory = memory with { PrimitiveProcess = process.SetDebug45B5D0() };
        }
        NativeBattleWaterInitializationResult initialized = NativeBattleWaterInitialization.InitializeScriptedEntry(core, arena, directories, inputs, new(profile.QueueAddress, profile.BankAddress, 0x10, 1, profile.Callback), checked((short)profile.SourceBase));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.QueueAddress, profile.BankAddress, 0x10, 1);
        (pool, _) = pool.Append(profile.Callback);
        return new(memory with { Arena = initialized.Arena, ScriptedEffects = initialized.Core, Cinematic = new(profile.Effect, initialized.TemporaryFrame.Bytes[0x145]) }, effect with { Pools = [pool], ReturnedDescriptor = pool.DescriptorAddress }, initialized.SlotBank, initialized.TemporaryFrame, initialized.CurrentHeaderIndex);

        void RequireResource(uint publication, NativeBattleWaterInitializationResource resource)
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == publication) ?? throw new NotSupportedException("Native cinematic entry requires both ordered executed initializer publications.");
            if (pointer.ArenaOffset != resource.ArenaOffset)
            {
                throw new InvalidDataException("Native cinematic entry resource input disagrees with its executed initializer publication.");
            }
        }
    }

    internal static void RequireBinding(NativeBattleCinematicProfile profile, NativeBattleOriginalEffectState effect)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(effect);
        if (profile != NativeBattleCinematicProfile.ForEffect(profile.Effect) || effect.Binding.Effect != profile.Effect || effect.Binding.Entry != profile.Entry || effect.Binding.Initializer != profile.Initializer)
        {
            throw new InvalidDataException("Native cinematic execution requires its actual selected profile, entry and initializer.");
        }
    }
}
