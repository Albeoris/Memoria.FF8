using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeCura889D60(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native889D60 requires its selected original effect.");
        NativeBattleCuraResources resources = effect.Binding.CuraResources ?? throw new NotSupportedException("Native889D60 requires its authenticated executable resources.");
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(NativeEffectMemory ?? throw new NotSupportedException("Native889D60 requires retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native889D60 requires its5099A0 registration.");
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native889D60 requires the original camera owner.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect with { Cura = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeCura889E40(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native889E40 requires its original effect owner.");
        NativeBattleCuraResources resources = effect.Binding.CuraResources ?? throw new NotSupportedException("Native889E40 requires its authenticated executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native889E40 requires its previous projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Cura requires the previously published1D97778 camera matrix.");
        NativeBattleCuraFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native889E40 requires retained effect memory."), effect, effect.Cura ?? throw new NotSupportedException("Native889E40 requires its executed889D60 state."));
        NativeBattleCuraControllerInputs inputs = new(nativeActionPackets, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Cura anchors require the original body pose source.")) { ApplyContact = ApplyContact };
        NativeBattleCuraControllerStep step = NativeBattleCuraController.Advance889E40(frame, inputs, resources, (current, address) =>
        {
            current = current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Cura lost its canonical process memory.") };
            NativeBattleCuraParticleInputs particleInputs = new(inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Cura requires its55CBD2 CRT random owner."), Execute) { Projection = projection };
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleCuraParticleStep leaf = NativeBattleCuraFamilyProfile.ForEffect(effect.Binding.Effect).IsSpriteCallback(task.Callback) ? NativeBattleCuraParticles.AdvanceSprite(current, address, resources, particleInputs) : NativeBattleCuraParticles.AdvanceLarge(current, address, resources, particleInputs);
            nativeCrtRandom = leaf.CrtRandomAfter;
            NativeEffectMemory = leaf.Frame.Memory;
            return new(leaf.Frame, leaf.Result);
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect with { Cura = step.Frame.State } };

        void ApplyContact(int index)
        {
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
        }

        NativeBattleCuraFrame Execute(NativeBattleCuraFrame current, NativeBattleCuraOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleCuraSpriteOperation sprite:
                    current = AppendEffect(current, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, NativeCamera!.Roll, 17, 2));
                    break;
                case NativeBattleCuraMeshOperation mesh:
                    current = AppendEffect(current, mesh.Batch);
                    break;
                case NativeBattleCuraStageFadeOperation fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native88B3D0 requires the retained four-group stage owner.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("Native88B3D0 requires its four44-byte stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Fade, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    break;
                case NativeBattleCuraActorInitializeOperation initialize:
                    current = current with { State = current.State with { ActorContext = new(initialize.Target) } };
                    break;
                case NativeBattleCuraActorVisibilityOperation visibility:
                    NativeBattleGraphicsModel model = graphics.Models[visibility.Target];
                    graphics = graphics with { Models = graphics.Models.SetItem(visibility.Target, model with { Flags0 = visibility.Hidden ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & ~4) }) };
                    break;
                case NativeBattleCuraActorDrawOperation actor:
                    NativeBattleCureActorContext context = current.State.ActorContext ?? throw new NotSupportedException("Native88BDA0 requires its executed88CA90 shared actor context.");
                    NativeBattleGraphicsModel target = graphics.Models[context.Target];
                    graphics = SubmitNativeShadowPackets5088A0(graphics, target, projection, 4112, false);
                    NativeBattleCureActorPacketBatch batch = NativeBattleCureActorAssembly.Emit88BDA0(target, NativeGraphicsPoseResource(graphics, context.Target), view, projection, actor.Center, actor.VerticalOffset, NativeBattleCuraFamilyProfile.ForEffect(effect.Binding.Effect).ReadContextF4(NativeEffectMemory!));
                    foreach (NativeBattleCureAllocatedPrimitive allocation in batch.Primitives)
                    {
                        if (allocation.Bank == NativeBattleCurePacketBank.Process)
                        {
                            graphics = AppendNativePrimitivePackets(graphics, [allocation.Primitive]);
                        }
                        else
                        {
                            current = AppendEffect(current with { Memory = NativeEffectMemory! }, new([new(allocation.Primitive, 0, 0)], allocation.Primitive.Words.Length * 4));
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException("Native Cura operation requires its original immediate owner.");
            }
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Cura operation lost process memory.") };
        }

        NativeBattleCuraFrame AppendEffect(NativeBattleCuraFrame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Cura requires its initialized original OT.");
            int cursor = current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Cura requires its executed889E40 draw cursor.");
            NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            NativeEffectMemory = write.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = write.Ordering } };
            return current with { Memory = write.Memory, State = current.State with { PrimaryDrawCursor = write.Cursor } };
        }
    }
}
