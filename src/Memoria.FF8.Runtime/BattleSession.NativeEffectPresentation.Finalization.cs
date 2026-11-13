using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool presentationInvocationCompleted;

    /// <summary>Finishes the guardian outer invocation after the host has completed casting and the native module has naturally released its callbacks.</summary>
    internal NativeBattleEffectPresentationStep CompleteEffectPresentationInvocation()
    {
        if (presentationForeignActors is null)
        {
            throw new InvalidOperationException("Guardian presentation finalization requires its presentation owner.");
        }
        if (presentationInvocationCompleted)
        {
            return new([], []);
        }
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new InvalidDataException("Guardian presentation finalization lost its graphics owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Guardian presentation finalization lost its process owner.");
        if (!EffectPresentationInvoked || memory.ReadReturnedTask5044B0() != 0 || memory.DetachedReturnedTask is not null || graphics.ModuleEffects.Head is not null || graphics.Auxiliary.Head is not null || HasPendingEffectPresentationEvents)
        {
            throw new InvalidOperationException("Guardian presentation finalization waits for actual invocation, natural native callback completion and consumed output.");
        }
        NativeBattleEffectParameters parameters = graphics.OriginalEffect?.Parameters ?? throw new InvalidDataException("Guardian presentation finalization lost its module parameters.");
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        if (NativeBattleActionGraphics.SelectService(header).Callback != 0x50B2A0)
        {
            throw new InvalidOperationException("Guardian presentation finalization requires the source header's guardian outer service.");
        }
        graphics = FinishNativeGuardianInvocationContacts(graphics);
        NativeGraphics = graphics with { Flags = graphics.Flags & ~0x10u };
        NativeEffectMemory = NativeEffectMemory! with { Active = 0 };
        presentationInvocationCompleted = true;
        NativeBattleEffectPresentationStep result = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray()) { Sequences = presentationSequences.ToImmutableArray() };
        presentationContacts.Clear();
        presentationSounds.Clear();
        presentationSequences.Clear();
        return result;
    }
}
