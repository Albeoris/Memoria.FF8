namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeModuleEffects(NativeBattleGraphicsState graphics)
    {
        if (graphics.OriginalEffect is { Binding.Effect: 18 } effect)
        {
            graphics = graphics with { OriginalEffect = effect with { PresentationPackets = [], PresentationPacketScope = null } };
        }
        if (graphics.OriginalEffect is { Binding.Effect: 18, ReturnedDescriptor: not null } && graphics.ModuleEffects.CastingPresentation?.Cinematic18Birth is not > 0)
        {
            throw new NotSupportedException("The selected cinematic18 context requires explicit casting presentation provenance; migrate that selected older snapshot before use.");
        }
        if (graphics.OriginalEffect is { } selected && NativeBattleRenzokukenVisualLifetimes.Owns(selected.Binding.Effect))
        {
            graphics = graphics with { OriginalEffect = selected with { PresentationPackets = [] } };
        }
        NativeBattleModuleState restoredModules = graphics.ModuleEffects.RestoreCastingPresentation(actor => graphics.Models[actor].Sequence);
        graphics = graphics with { ModuleEffects = restoredModules };
        NativeBattleModuleStep step = graphics.ModuleEffects.Advance(new(graphics.Flags) { Resources = definition.NativeModuleResources, CrtRandom = nativeCrtRandom, ReadModelAnchor = index => ReadNativeModelAnchor(graphics, index), ReadModelBone = (index, bone, fraction) => ReadNativeGraphicsBone(graphics, index, bone, fraction), ReadModelBoneCount = index => checked((byte)(NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native573560 requires its current model skeleton owner.")).BoneCount), ReadModelRotation14 = index => graphics.Models[index].Rotation14 ?? throw new NotSupportedException("Native module effect requires the loaded model rotationWORD14."), TransformModelPosition = (index, local) => NativeBattlePoseMatrices.TransformModulePosition570CC0(graphics.Models[index], local), ReadModelTransform = index => graphics.Models[index].Transform ?? throw new NotSupportedException("Native module effect requires its model WORD36/38 transform owner.") }, Dispatch, ObserveNativeModuleStack);
        nativeCrtRandom = step.CrtRandomAfter;
        foreach (NativeBattleModuleDraw draw in step.Draws)
        {
            if (draw.Kind == NativeBattleModuleDrawKind.Sound)
            {
                NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native module sound lacks its original spatial anchor.");
                PublishNativeModuleSound(draw.Sound, draw.SoundFlags, anchor);
            }
        }
        return graphics with { ModuleEffects = step.State, ModuleDraws = step.Draws };

        NativeBattleModuleCallback Dispatch(NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs)
        {
            uint callback = modules.Slots[slot].Callback ?? throw new InvalidDataException("Native shared callback lost its physical address.");
            (NativeBattleGraphicsState advanced, NativeBattleModuleCallback result) = callback switch
            {
                0x56F9C0 or 0x56FCD0 or 0x56FE10 => AdvanceNativeRinoaPhysicalHit(graphics, modules, slot, inputs),
                0x587CA0 or 0x587F30 or 0x585C70 or 0x585F00 or 0x5835D0 or 0x583860 or 0x580F50 or 0x5811E0 or 0x57EC70 or 0x57EF00 or 0x57C390 or 0x57C620 or 0x57A010 or 0x57A2A0 or 0x577D90 or 0x578020 or 0x575BA0 or 0x575E30 => AdvanceNativeRenzokukenSpecialShared(graphics, modules, slot, inputs),
                0x5FD810 or 0x5FDAA0 => AdvanceNativeRenzokukenSpecial161Shared(graphics, modules, slot, inputs),
                0x6013C0 or 0x601650 => AdvanceNativeRenzokuken159Shared(graphics, modules, slot, inputs),
                0x5FF4E0 or 0x5FF770 => AdvanceNativeRenzokuken160Shared(graphics, modules, slot, inputs),
                0x61E2F0 or 0x61E580 => AdvanceNativeRenzokuken141Shared(graphics, modules, slot, inputs),
                0x5A70A0 or 0x5A7330 => AdvanceNativeRenzokukenVariantShared(graphics, modules, slot, inputs, NativeBattleRenzokukenVariantProfile.Renzokuken259),
                0x58BE50 or 0x58C0E0 => AdvanceNativeRenzokukenVariantShared(graphics, modules, slot, inputs, NativeBattleRenzokukenVariantProfile.Renzokuken332),
                0x58A0C0 or 0x58A350 => AdvanceNativeRenzokukenVariantShared(graphics, modules, slot, inputs, NativeBattleRenzokukenVariantProfile.Renzokuken333),
                _ => throw new NotSupportedException($"Native shared callback {callback:X8} requires its original executable implementation.")
            };
            graphics = advanced;
            return result;
        }
    }

    private static NativeBattleGraphicsState ScheduleNativeModelEffect(NativeBattleGraphicsState graphics, int index, byte operand)
    {
        (byte effect, uint flags) = NativeBattleModuleState.SelectModelEffect(operand, graphics.Flags);
        NativeBattleCastingSequenceProvenance? source = null;
        if (graphics.ModuleEffects.CastingPresentation is { } presentation && presentation.Sequences.TryGetValue(index, out NativeBattleCastingSequenceProvenance? selected) && graphics.Models[index].Sequence == selected.Sequence)
        {
            source = selected;
        }
        NativeBattleModuleState modules = graphics.ModuleEffects.AppendModelEffect(index, effect, effect == 0 ? source : null);
        return graphics with { ModuleEffects = modules, Flags = flags };
    }

    private void PublishNativeModuleSound(uint sound, uint flags, NativeBattleModuleAnchor anchor, string? actorId = null)
    {
        //501160 clears the3D-mode switch;5017A0 then uses5011B0's current camera projection before46B2A0.
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5017A0 sound requires its actual5011B0 camera projection.");
        byte pan = projection.ReadPan(new(anchor.X, anchor.Y, anchor.Z));
        PublishNativeModuleSound(new(sound, flags, anchor) { Pan = pan }, actorId);
    }
}
