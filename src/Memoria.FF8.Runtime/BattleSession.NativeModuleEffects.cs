namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeModuleEffects(NativeBattleGraphicsState graphics)
    {
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
        NativeBattleModuleState modules = graphics.ModuleEffects.AppendModelEffect(index, effect);
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
