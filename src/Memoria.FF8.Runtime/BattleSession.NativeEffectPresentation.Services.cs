using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<int> ReadRequiredPresentationPartyActors()
    {
        if (presentationForeignActors is null)
        {
            return [0, 1, 2];
        }
        ImmutableArray<int> actors = presentationForeignActors.Values.Where(actor => actor.IsParty).Select(actor => actor.Index).Order().Prepend(0).ToImmutableArray();
        if (presentationPartyActorCount is { } expected && actors.Length != expected)
        {
            throw new NotSupportedException("The native party presentation operation exceeds the host's available physical model bank.");
        }
        return actors;
    }

    private NativeBattleStageState ReadNativeStageVisibilityOwner()
    {
        if (presentationForeignActors is not null && presentationArena is null)
        {
            throw new NotSupportedException("The host has no arena visibility owner for this native stage operation.");
        }
        return NativeStage ?? throw new NotSupportedException("The native stage visibility operation requires its current stage owner.");
    }

    internal void PublishEffectPresentationSource(NativeBattleGraphicsModel source)
    {
        if (presentationForeignActors is null || source.Index != 0)
        {
            throw new InvalidDataException("The presentation source publication requires physical source slot zero and its presentation owner.");
        }
        NativeGraphics = NativeGraphics! with { Models = NativeGraphics!.Models.SetItem(0, source) };
    }

    internal int ReadEffectPresentationSourceVariable(NativeBattleGraphicsModel source, byte variable)
    {
        return ReadNativeGraphicsSequenceVariable(NativeGraphics! with { Models = NativeGraphics!.Models.SetItem(0, source) }, 0, variable);
    }

    internal void WriteEffectPresentationSourceVariable(byte variable, int value) => WriteNativeGraphicsSequenceVariable(variable, value);

    internal NativeBattleGraphicsModel WriteEffectPresentationSourceTargetVariable(NativeBattleGraphicsModel source, byte variable, int value)
    {
        NativeBattleGraphicsState graphics = NativeGraphics!;
        int target = graphics.ActionContext?.TargetIndex ?? throw new NotSupportedException("Native presentation target writes require their current50D300 target identity.");
        graphics = graphics with { Models = graphics.Models.SetItem(0, source) };
        NativeGraphics = graphics with { Models = graphics.Models.SetItem(target, NativeBattleActionGraphics.WriteSequenceTarget5048E0(graphics.Models[target], variable, value)) };
        return NativeGraphics.Models[0];
    }

    internal ushort NextEffectPresentationGlobalRandom() => NextNativeGlobalRandom();

    private void InitializeEffectPresentationServices(NativeBattleEffectParameters parameters)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(nativeActionPackets, parameters.HeaderIndex7C, NativeGraphics?.ActionGraphics) with { UseActionCamera = false };
        NativeBattleGraphicsState graphics = NativeBattleActionGeometryRules.PrepareGeometry50D300(NativeGraphics! with { ActionGraphics = action }, parameters.Actor78, parameters.TargetMask7A, parameters.TargetMask7A, globals, ReadNativeGraphicsBone);
        NativeEffectMemory = memory with { CameraAliases = (memory.CameraAliases ?? NativeBattleCameraAliases.Unknown()).Observe50D300(parameters.Actor78, parameters.TargetMask7A), ActionGeometryGlobals = globals.Observe50D300(graphics.ActionContext!) };
        NativeGraphics = graphics;
        if (NativeEffectMemory.PrimitiveProcess is { Software: { } software } process)
        {
            NativeSoftwareDrawingEnvironmentBuffers buffers = software.EnvironmentBuffers ?? throw new NotSupportedException("Native presentation requires its initialized drawing environments.");
            NativeSoftwareDrawingEnvironmentApplication application = buffers.Apply45B660(process.PacketBuffer, software.Environment);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = process with { Software = software with { EnvironmentBuffers = application.Buffers, Environment = application.Environment, SceneCapture = null } } };
        }
    }
}
