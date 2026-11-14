using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeCastState(NativeBattleGraphicsModel Model, byte Phase, bool ReturnedToStanding)
{
    public bool GuardianInvocation { get; init; }
    public uint ServiceCallback { get; init; } = 0x50A9A0;
    public bool ImmediateInvocation => ServiceCallback is 0x50BDC0 or 0x50BEE0 or 0x50B190;
    public bool RetainsProcess { get; init; }
    public byte? PendingSequence { get; init; }
    public bool Complete => Phase == 8 && (ReturnedToStanding || ImmediateInvocation || RetainsProcess);
}

internal sealed record CrossoverNativeCastPoll(CrossoverNativeCastState State, bool InvokeEffect)
{
    public bool ReloadSourceResources { get; init; }
}

/// <summary>Original source sequence and resource handshake, with the host retaining camera and action ownership.</summary>
internal static class CrossoverNativeCastPresentation
{
    public static CrossoverNativeCastState Begin(NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, ImmutableArray<byte> commandSequences, byte command, ushort action = 0, uint serviceCallback = 0x50A9A0)
    {
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(command, 0, action, commandSequences);
        bool immediate = serviceCallback is 0x50BDC0 or 0x50BEE0 or 0x50B190;
        if (model is not { Index: 0, ModelId4: >= 0 and <= 10 } || !immediate && (model.Flags0 & 2) == 0 || commandSequences.Length != 39)
        {
            throw new InvalidDataException($"Mixed casting requires its loaded source character and command sequence (command {command}, action {action}, service {serviceCallback:X}, actor {model.Index}/{model.ModelId4}, flags {model.Flags0:X}, sequence {sequence}, available {resource.Contains(sequence)}).");
        }
        if (serviceCallback is 0x50BDC0 or 0x50BEE0 or 0x50B190)
        {
            return new(model, 0, false) { ServiceCallback = serviceCallback, RetainsProcess = serviceCallback is 0x50BDC0 or 0x50BEE0 };
        }
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Mixed casting requires the original character skeleton.");
        NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("Mixed casting requires the original character transform.");
        // The native casting callbacks size their effects from WORD38, independently of the body matrix.
        model = model with { Transform = transform with { Scale38 = pose.ReadModelScale5027D0() } };
        bool guardian = command is 38 or 244 or 254 && action is not (15 or 70);
        if (guardian)
        {
            model = NativeBattleGuardianInvocation.EnterSource(model);
        }
        bool pending = sequence != 0 && !resource.ContainsNativeIndex505850(sequence);
        NativeBattleGraphicsModel selected = pending ? model : NativeBattleGraphicsModels.SelectSequence(model, sequence);
        if (!pending && !ReferenceEquals(selected, model))
        {
            selected = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, sequence);
        }
        return new(selected, 3, false) { GuardianInvocation = guardian, ServiceCallback = serviceCallback, RetainsProcess = command is 11 or 14, PendingSequence = pending ? sequence : null };
    }

    public static CrossoverNativeCastState SelectPendingSequence(CrossoverNativeCastState source, NativeBattleGraphicsResource resource)
    {
        if (source.PendingSequence is not byte sequence)
        {
            return source;
        }
        NativeBattleGraphicsModel selected = NativeBattleGraphicsModels.SelectSequence(source.Model, sequence);
        if (!ReferenceEquals(selected, source.Model))
        {
            selected = NativeBattleSequenceSelections.SelectResource505BC0(source.Model, resource, sequence);
        }
        return source with { Model = selected, PendingSequence = null };
    }

    public static CrossoverNativeCastPoll Poll(CrossoverNativeCastState source, bool modulesBusy, bool effectComplete)
    {
        NativeBattleGraphicsModel model = source.Model;
        ushort flags = model.ResourceFlags44 ?? throw new InvalidDataException("Mixed casting lost its original source resource flags.");
        if (source.ImmediateInvocation)
        {
            if (source.Phase == 0)
            {
                return new(source with { Phase = 6 }, true);
            }
            return source.Phase == 6 && effectComplete ? new(source with { Phase = 8 }, false) : new(source, false);
        }
        if (source.Phase == 3)
        {
            return modulesBusy ? new(source, false) : new(source with { Model = model with { ResourceFlags44 = (ushort)(flags | 0x10) }, Phase = 4 }, false);
        }
        if (source.Phase == 4)
        {
            bool ready = source.GuardianInvocation ? (model.Flags0 & 4) != 0 : (flags & 0x20) != 0;
            return modulesBusy || !ready ? new(source, false) : new(source with { Model = source.GuardianInvocation ? NativeBattleGuardianInvocation.HideSource(model) : model with { ResourceFlags44 = (ushort)(flags & ~0x20) }, Phase = 6 }, true);
        }
        if (source.Phase == 6 && effectComplete)
        {
            return new(source with { Phase = 7 }, false) { ReloadSourceResources = source.GuardianInvocation };
        }
        if (source.Phase == 7)
        {
            model = source.GuardianInvocation ? NativeBattleGraphicsModels.SelectSequence(NativeBattleGuardianInvocation.RestoreSource(model), 20) : model with { ResourceFlags44 = (ushort)(flags | 0x200) };
            return new(source with { Model = model, Phase = 8 }, false);
        }
        if (source.Phase is not (6 or 8))
        {
            throw new InvalidDataException("Mixed casting has an unsupported native handshake phase.");
        }
        return new(source, false);
    }

    public static (CrossoverNativeCastState State, ImmutableArray<NativeBattleSequenceCue> Cues) AdvanceModel(CrossoverNativeCastState source, NativeBattleGraphicsResource resource, NativeBattleSequenceInputs services, NativeBattleGraphicsResource? originalResource = null)
    {
        if ((source.Model.Flags0 & 8) != 0)
        {
            return (source, []);
        }
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(source.Model, resource, services with { OriginalResource = originalResource ?? resource });
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Mixed casting requires the original character skeleton.");
        NativeBattleGraphicsModel model = step.Model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(step.Model, pose) };
        bool originalBinding = model.ResourceMagicId is null && model.ResourceRenzokukenTargetBank is null && model.ActiveStagedResourceRow100 is null && model.ActiveResourceRow100 == model.OriginalResourceRow104;
        bool standing = model.Sequence == NativeBattleGraphicsModels.PlayerStandingSequence(model.Flags8 ?? throw new InvalidDataException("Mixed casting lost its source status DWORD8.")) && (model.ResourceFlags44!.Value & 0x40) != 0;
        bool returned = source.ReturnedToStanding || source.Phase == 8 && originalBinding && (standing || step.Cues.Any(cue => cue.Kind == NativeBattleSequenceCueKind.RestoreStanding));
        return (source with { Model = model, ReturnedToStanding = returned }, step.Cues);
    }
}
