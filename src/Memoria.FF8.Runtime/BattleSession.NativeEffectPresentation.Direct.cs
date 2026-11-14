using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal bool ReadEffectPresentationNativeBusy()
    {
        return NativeOriginalEffectBusy(NativeGraphics ?? throw new InvalidOperationException("The presentation has no current graphics owner."));
    }

    internal bool ReadEffectPresentationServiceComplete()
    {
        if (!EffectPresentationInvoked)
        {
            return false;
        }
        if (presentationSelectedModulePhase is not null)
        {
            return presentationSelectedModuleComplete;
        }
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new InvalidOperationException("The presentation has no current graphics owner.");
        NativeBattleEffectParameters parameters = graphics.OriginalEffect?.Parameters ?? throw new InvalidDataException("The current presentation service lost its parameter owner.");
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        uint callback = NativeBattleActionGraphics.SelectService(header).Callback;
        ushort action = NativeBattleActionGraphics.Word(header, 4);
        if (callback == 0x50A9A0 && action == 65534)
        {
            return NativeOriginalEffectDescriptorReleased(graphics);
        }
        if (callback == 0x50A9A0 && header.ReadByte(1) is 11 or 14)
        {
            return parameters.Parameter81 == 1;
        }
        if (callback == 0x50BDC0 && action is not (0xFFFC or 0xFFFA) || callback == 0x50BEE0 && header.ReadByte(1) == 0xED)
        {
            return parameters.Parameter81 == 1 && NativeCameraTaskMask == 0;
        }
        return !NativeOriginalEffectBusy(graphics);
    }

    internal void ReinvokeEffectPresentationDirect(NativeBattleActionPacketsState packets, NativeBattleGraphicsModel source, NativeCrtRandomState randomState, NativeBattleEffectTextureState textures, NativeBattleEffectParameters parameters, ImmutableArray<NativeBattleForeignActor> foreignActors)
    {
        if (presentationForeignActors is null || !EffectPresentationInvoked || presentationSourceIndex != 0 || source.Index != 0 || NativeGraphics?.PresentationRoster is not null)
        {
            throw new InvalidOperationException("A direct presentation phase requires its invoked retained presentation owner.");
        }
        if (HasPendingEffectPresentationEvents || !ReadEffectPresentationServiceComplete())
        {
            throw new InvalidOperationException("A direct presentation phase waits for the previous native service and consumed output.");
        }
        packets.Validate();
        if (parameters.Actor78 != 0 || parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new InvalidDataException("A direct presentation phase requires its bound source and physical header.");
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        uint callback = NativeBattleActionGraphics.SelectService(header).Callback;
        if (callback is not (0x50BDC0 or 0x50BEE0))
        {
            throw new NotSupportedException("The selected source header does not use a native retained direct service.");
        }
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new InvalidDataException("The direct presentation phase lost its graphics owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("The direct presentation phase lost its process owner.");
        NativeBattleOriginalEffectState selected = memory.RequireDirectEntry21DFEC4(graphics.OriginalEffect);
        NativeBattleGraphicsModel loaded = graphics.Models[0];
        source = source with { ActiveResourceRow100 = loaded.ActiveResourceRow100, OriginalResourceRow104 = loaded.OriginalResourceRow104, TextureResourceIndex = loaded.TextureResourceIndex, LinkedTextureResourceIndex = loaded.LinkedTextureResourceIndex, BodyTextureSlots = loaded.BodyTextureSlots, LinkedTextureSlots = loaded.LinkedTextureSlots, TextureAllocationMask = loaded.TextureAllocationMask };
        Dictionary<int, NativeBattleForeignActor> previousForeignActors = presentationForeignActors;
        if (!foreignActors.IsDefault)
        {
            presentationForeignActors = foreignActors.ToDictionary(actor => actor.Index);
        }
        nativeActionPackets = packets.ActivateHeader(parameters.HeaderIndex7C);
        nativeCrtRandom = randomState;
        NativeEffectMemory = memory with { Textures = textures };
        NativeGraphics = graphics with { Models = BindPresentationForeignModels(graphics.Models.SetItem(0, source), roster: null, previous: previousForeignActors) };
        InitializeEffectPresentationServices(parameters);
        graphics = NativeGraphics!;
        NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new InvalidDataException("The direct presentation phase lost its normalized source service.");
        if (context.Callback != callback || context.ActorIndex != parameters.Actor78 || context.TargetMask != parameters.TargetMask7A)
        {
            throw new InvalidDataException("The direct presentation parameters contradict their physical source header.");
        }
        if (callback == 0x50BDC0)
        {
            graphics = PublishNativeOriginalEffectTitle(graphics, context);
        }
        memory = (NativeEffectMemory ?? throw new InvalidDataException("The direct presentation phase lost its initialized process owner.")).SetDirectParameters(callback, header.ReadByte(1), NativeBattleActionGraphics.Word(header, 4), header.ReadByte(6));
        NativeEffectMemory = memory;
        graphics = graphics with { OriginalEffect = selected with { Parameters = parameters with { Parameter80 = memory.Parameter80, Parameter81 = 0, Parameter82 = memory.Parameter82, Parameter83 = 0 } } };
        uint? returnedDescriptor = selected.ReturnedDescriptor;
        graphics = InvokeNativeOriginalEffect(graphics, assignReturnedTask: false);
        NativeGraphics = graphics with { Flags = graphics.Flags | 0x10, OriginalEffect = graphics.OriginalEffect! with { ReturnedDescriptor = returnedDescriptor } };
        presentationInvocationCompleted = false;
    }
}
