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
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(parameters.Header);
        if (header.Command is not (0x26 or 0xF4 or 0xFE))
        {
            throw new InvalidOperationException("Guardian presentation finalization requires the source header's guardian outer service.");
        }
        switch (NativeBattleActionGraphics.SelectService(header).Callback)
        {
            case 0x50B2A0:
                graphics = FinishNativeGuardianInvocationContacts(graphics);
                graphics = graphics with { Flags = graphics.Flags & ~0x10u };
                break;
            case 0x50A9A0:
                //The ordinary casting service has no late contact consumer; BYTE81 does not select one.
                uint flags = graphics.Flags & ~0x20u;
                graphics = graphics with { Flags = (parameters.Flags79 & 2) == 0 ? flags & ~0x10u : flags };
                break;
            default:
                throw new NotSupportedException("Guardian presentation finalization requires the source header's supported outer completion contract.");
        }
        NativeGraphics = graphics;
        NativeEffectMemory = NativeEffectMemory! with { Active = 0 };
        presentationInvocationCompleted = true;
        NativeBattleEffectPresentationStep result = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray()) { Sequences = presentationSequences.ToImmutableArray() };
        presentationContacts.Clear();
        presentationSounds.Clear();
        presentationSequences.Clear();
        return result;
    }
}
