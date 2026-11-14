using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<int> ReadRequiredPresentationPartyActors()
    {
        if (NativeGraphics?.PresentationRoster is { } roster)
        {
            return NativeBattlePresentationPartyRows.Resolve(roster, presentationPartyActorCount);
        }
        return NativeBattlePresentationPartyRows.Resolve(presentationSourceIndex, presentationSourceModelId, presentationSourceIsParty, hostedGuardianFormMode,
            presentationNativeTargets?.Values, presentationForeignActors?.Values, presentationPartyActorCount);
    }

    private NativeBattleStageState ReadNativeStageVisibilityOwner()
    {
        if (presentationForeignActors is not null && presentationArena is null && !hostedBattlePresentation)
        {
            throw new NotSupportedException("The host has no arena visibility owner for this native stage operation.");
        }
        return NativeStage ?? throw new NotSupportedException("The native stage visibility operation requires its current stage owner.");
    }

    internal void PublishEffectPresentationSource(NativeBattleGraphicsModel source)
    {
        if (presentationForeignActors is null || presentationSourceIndex != 0 || source.Index != 0 || NativeGraphics?.PresentationRoster is not null)
        {
            throw new InvalidDataException("The presentation source publication requires physical source slot zero and its presentation owner.");
        }
        NativeGraphics = NativeGraphics! with { ActorModels = NativeGraphics!.ActorModels.Set(0, source) };
    }

    internal int ReadEffectPresentationSourceVariable(NativeBattleGraphicsModel source, byte variable)
    {
        return ReadNativeGraphicsSequenceVariable(NativeGraphics! with { ActorModels = NativeGraphics!.ActorModels.Set(presentationSourceIndex, source) }, presentationSourceIndex, variable);
    }

    internal void WriteEffectPresentationSourceVariable(byte variable, int value) => WriteNativeGraphicsSequenceVariable(variable, value);

    internal NativeBattleGraphicsModel WriteEffectPresentationSourceTargetVariable(NativeBattleGraphicsModel source, byte variable, int value)
    {
        NativeBattleGraphicsState graphics = NativeGraphics!;
        int target = graphics.ActionContext?.TargetIndex ?? throw new NotSupportedException("Native presentation target writes require their current50D300 target identity.");
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(presentationSourceIndex, source) };
        NativeGraphics = graphics with { ActorModels = graphics.ActorModels.Set(target, NativeBattleActionGraphics.WriteSequenceTarget5048E0(graphics.ActorModels.Get(target), variable, value)) };
        return ReadEffectPresentationSource();
    }

    internal ushort NextEffectPresentationGlobalRandom() => NextNativeGlobalRandom();

    private void InitializeEffectPresentationServices(NativeBattleEffectParameters parameters)
    {
        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(nativeActionPackets, parameters.Header, NativeGraphics?.ActionGraphics) with { UseActionCamera = false };
        NativeBattleGraphicsState graphics = NativeGraphics! with { ActionGraphics = action };
        if (graphics.PresentationRoster is { } roster)
        {
            NativeBattleEffectMemory geometryMemory = NativeEffectMemory!;
            NativeBattleActionGeometryGlobals globals = geometryMemory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
            BattleModelTargets targets = parameters.TargetActors;
            graphics = NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, parameters.Source, targets, targets, globals, ReadNativeGraphicsBone);
            NativeEffectMemory = geometryMemory with { CameraAliases = (geometryMemory.CameraAliases ?? NativeBattleCameraAliases.Unknown()).Observe50D300(parameters.Source, targets, roster), ActionGeometryGlobals = globals.Observe50D300(graphics.ActionContext!) };
            NativeGraphics = graphics;
        }
        else
        {
            NativeGraphics = PrepareEffectPresentationGeometry(graphics, parameters.Source, parameters.TargetActors);
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native presentation lost its initialized process owner.");
        if (memory.PrimitiveProcess is { Software: { } software } process)
        {
            NativeSoftwareDrawingEnvironmentBuffers buffers = software.EnvironmentBuffers ?? throw new NotSupportedException("Native presentation requires its initialized drawing environments.");
            NativeSoftwareDrawingEnvironmentApplication application = buffers.Apply45B660(process.PacketBuffer, software.Environment);
            NativeEffectMemory = memory with { PrimitiveProcess = process with { Software = software with { EnvironmentBuffers = application.Buffers, Environment = application.Environment, SceneCapture = null } } };
        }
    }

    private NativeBattleGraphicsState PrepareEffectPresentationGeometry(NativeBattleGraphicsState graphics, byte actor, ushort targets)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
        graphics = NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, actor, targets, targets, globals, ReadNativeGraphicsBone);
        NativeEffectMemory = memory with { CameraAliases = (memory.CameraAliases ?? NativeBattleCameraAliases.Unknown()).Observe50D300(actor, targets), ActionGeometryGlobals = globals.Observe50D300(graphics.ActionContext!) };
        return graphics;
    }
}
