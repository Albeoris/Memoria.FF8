using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDeath(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Death requires its selected effect.");
        NativeBattleDeathResources resources = effect.Binding.DeathResources ?? throw new NotSupportedException("Native Death requires its original resources.");
        NativeBattleDeathFrame frame = NativeBattleDeathController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Death requires retained memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Death requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Death requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeDeath(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Death requires its selected effect.");
        NativeBattleDeathResources resources = effect.Binding.DeathResources ?? throw new NotSupportedException("Native Death requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Death requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Death requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Death requires its actual camera matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Death requires its original graphics rules.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Death requires its original interpolation constants.");
        NativeBattleStageColors stageColors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native Death requires its current model colors.");
        NativeBattleDeathFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Death requires retained memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Death requires its CRT random owner."));
        NativeBattleDeathInputs inputs = new(nativeActionPackets, projection, camera.Roll, colors, stageColors.Color44, stageColors.Color40, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Death requires its current target pose."), DrawModel, Execute);
        NativeBattleDeathFrame advanced = NativeBattleDeathController.Advance(frame, resources, inputs);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleDeathModelStep DrawModel(NativeBattleDeathFrame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleShadowSource shadow = rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Death requires its original shadow source.");
            NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Death requires its current renderer capabilities.");
            if ((task.ReadByte(0x26) & 8) != 0)
            {
                NativeBattleDeathDraw dark = NativeBattleDeathDrawing.Dark(current, task, model, resources, projection, shadow, capabilities);
                return new(Append(dark.Frame, dark.Packets), dark.Task);
            }
            NativeBattleDeathDraw normal = NativeBattleDeathDrawing.Normal(current, task, model, resources, projection, shadow, capabilities, colors);
            return new(Append(normal.Frame, normal.Packets), normal.Task);
        }

        NativeBattleDeathFrame Append(NativeBattleDeathFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Death requires its actual pending ordering table.");
            NativeBattleDeathState state = current.Memory.Death14 ?? throw new NotSupportedException("Native Death lost its packet cursor owner.");
            int cursor = state.Cursor ?? throw new NotSupportedException("Native Death lost its executed packet arena selection.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory with { Death14 = state with { Cursor = written.Cursor } } };
        }

        NativeBattleDeathFrame Execute(NativeBattleDeathFrame current, NativeBattleDeathOperation operation)
        {
            switch (operation)
            {
                case NativeBattleDeathPackets packets:
                    return Append(current, packets.Batch);
                case NativeBattleDeathSound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(new(sound.Descriptor, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleDeathContact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Death contact lost process memory.") };
                case NativeBattleDeathStage stageWrite:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Death requires its stage color owners.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = stageWrite.Fade, ColorFlags = stageWrite.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleDeathTextureLoad texture:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)texture.Source), texture.Completion, (address, value) => current = current with { Effect = NativeBattleDeathController.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Death operation has no proved implementation.");
            }
        }
    }

    private bool TryWriteNativeDeathCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleDeathController.RootBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x8B68C0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleDeathController.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x8B68C0 } current ? current : throw new NotSupportedException("Native Death completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleDeathController.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}

