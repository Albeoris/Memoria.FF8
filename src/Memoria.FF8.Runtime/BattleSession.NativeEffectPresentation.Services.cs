using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<BattleModelOwner> ReadRequiredPresentationPartyActors()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        ImmutableArray<BattleModelOwner> party = [.. graphics.ActorModels.Party.Entries.Select(entry => entry.Owner)];
        if (presentationPartyActorCount is { } expected && party.Length != expected)
        {
            throw new InvalidDataException("Presentation party actors disagree with the host's party count.");
        }
        return party;
    }

    private NativeBattleStageState ReadNativeStageVisibilityOwner()
    {
        if (presentationForeignActors is not null && presentationArena is null && !hostedBattlePresentation)
        {
            throw new NotSupportedException("The host has no arena visibility owner for this native stage operation.");
        }
        return NativeStage ?? throw new NotSupportedException("The native stage visibility operation requires its current stage owner.");
    }

    internal void PublishEffectPresentationSource(BattleModelOwner owner, NativeBattleGraphicsModel source)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (presentationForeignActors is null || owner != presentationSourceOwner || !HasOriginalPresentationSourceSlot(graphics, owner))
        {
            throw new InvalidDataException("The presentation source publication requires its retained original source slot zero.");
        }
        NativeGraphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, source) };
    }

    private static bool HasOriginalPresentationSourceSlot(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        return graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out byte operand) && operand == 0 &&
            graphics.ActorModels.Owners.All(candidate => graphics.NativeBindings.TryEncodeExactActor(candidate, graphics.ActorModels, out _));
    }

    internal NativeBattleScriptValue ReadEffectPresentationSourceVariable(NativeBattleGraphicsModel source, byte variable)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        BattleModelOwner owner = RequirePresentationSourceOwner();
        return ReadNativeGraphicsSequenceVariable(graphics with { ActorModels = graphics.ActorModels.Set(owner, source) }, owner, variable);
    }

    internal void WriteEffectPresentationSourceVariable(byte variable, int value) => WriteNativeGraphicsSequenceVariable(variable, value);

    internal NativeBattleGraphicsModel WriteEffectPresentationSourceTargetVariable(NativeBattleGraphicsModel source, byte variable, int value)
    {
        NativeBattleGraphicsState graphics = NativeGraphics!;
        BattleModelOwner target = graphics.ActionContext?.Target ?? throw new NotSupportedException("Native presentation target writes require their current50D300 target identity.");
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(RequirePresentationSourceOwner(), source) };
        NativeGraphics = graphics with { ActorModels = graphics.ActorModels.Set(target, NativeBattleActionGraphics.WriteSequenceTarget5048E0(graphics.ActorModels.Get(target), variable, value)) };
        return ReadEffectPresentationSource();
    }

    internal ushort NextEffectPresentationGlobalRandom() => NextNativeGlobalRandom();

    private void InitializeEffectPresentationServices(NativeBattleEffectInvocation invocation)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        switch (invocation)
        {
            case NativeBattleEffectInvocation.Combat combat:
                NativeBattleEffectParameters parameters = combat.Parameters;
                var action = NativeBattleActionGraphics.Normalize(nativeActionPackets, parameters.Header, graphics.ActionGraphics) with { UseActionCamera = false };
                graphics = graphics with { ActionGraphics = action };
                NativeGraphics = PrepareNativeActionGeometry(graphics, parameters.Source, parameters.TargetActors, parameters.TargetActors);
                break;
            case NativeBattleEffectInvocation.Positioning positioning:
                BattleModelTargets selected = BattleModelTargets.Empty.Add(positioning.Source);
                NativeGraphics = PrepareNativeActionGeometry(graphics, positioning.Source, selected, selected);
                break;
            default:
                throw new InvalidDataException("Native presentation services require an admitted invocation binding.");
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native presentation lost its initialized process owner.");
        if (memory.PrimitiveProcess is { Software: { } software } process)
        {
            NativeSoftwareDrawingEnvironmentBuffers buffers = software.EnvironmentBuffers ?? throw new NotSupportedException("Native presentation requires its initialized drawing environments.");
            NativeSoftwareDrawingEnvironmentApplication application = buffers.Apply45B660(process.PacketBuffer, software.Environment);
            NativeEffectMemory = memory with { PrimitiveProcess = process with { Software = software with { EnvironmentBuffers = application.Buffers, Environment = application.Environment, SceneCapture = null } } };
        }
    }

}
