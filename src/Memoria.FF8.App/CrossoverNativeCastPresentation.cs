using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeCastState(NativeBattleGraphicsModel Model, byte Phase, bool ReturnedToStanding)
{
    public bool Complete => Phase == 8 && ReturnedToStanding;
}

internal sealed record CrossoverNativeCastPoll(CrossoverNativeCastState State, bool InvokeEffect);

/// <summary>Original source sequence and resource handshake, with the host retaining camera and action ownership.</summary>
internal static class CrossoverNativeCastPresentation
{
    public static CrossoverNativeCastState Begin(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, ImmutableArray<byte> commandSequences, byte command)
    {
        if (command is not (2 or 4 or 5) || model is not { Index: 0, ModelId4: >= 0 and <= 10 } || (model.Flags0 & 2) == 0 || commandSequences.Length != 39 || !resource.Contains(commandSequences[command]))
        {
            throw new InvalidDataException("Mixed casting requires a loaded original FFVIII character and a native Magic, Item or Limit sequence.");
        }
        return new(NativeBattleGraphicsModels.SelectSequence(model, commandSequences[command]), 3, false);
    }

    public static CrossoverNativeCastPoll Poll(CrossoverNativeCastState source, bool modulesBusy, bool effectComplete)
    {
        NativeBattleGraphicsModel model = source.Model;
        ushort flags = model.ResourceFlags44 ?? throw new InvalidDataException("Mixed casting lost its original source resource flags.");
        if (source.Phase == 3)
        {
            return modulesBusy ? new(source, false) : new(source with { Model = model with { ResourceFlags44 = (ushort)(flags | 0x10) }, Phase = 4 }, false);
        }
        if (source.Phase == 4)
        {
            return modulesBusy || (flags & 0x20) == 0 ? new(source, false) : new(source with { Model = model with { ResourceFlags44 = (ushort)(flags & ~0x20) }, Phase = 6 }, true);
        }
        if (source.Phase == 6 && effectComplete)
        {
            return new(source with { Phase = 7 }, false);
        }
        if (source.Phase == 7)
        {
            return new(source with { Model = model with { ResourceFlags44 = (ushort)(flags | 0x200) }, Phase = 8 }, false);
        }
        if (source.Phase is not (6 or 8))
        {
            throw new InvalidDataException("Mixed casting has an unsupported native handshake phase.");
        }
        return new(source, false);
    }

    public static (CrossoverNativeCastState State, ImmutableArray<NativeBattleSequenceCue> Cues) AdvanceModel(CrossoverNativeCastState source, NativeBattleGraphicsResource resource, NativeBattleSequenceInputs services)
    {
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(source.Model, resource, services with { OriginalResource = resource });
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Mixed casting requires the original character skeleton.");
        NativeBattleGraphicsModel model = step.Model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(step.Model, pose) };
        bool returned = source.ReturnedToStanding || step.Cues.Any(cue => cue.Kind == NativeBattleSequenceCueKind.RestoreStanding);
        if (returned && source.Phase != 8)
        {
            throw new InvalidDataException("Mixed casting returned before its native effect completion handshake.");
        }
        return (source with { Model = model, ReturnedToStanding = returned }, step.Cues);
    }
}
