namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeCure8D6A00(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native8D6A00 requires its selected original effect.");
        NativeBattleCureResources resources = effect.Binding.CureResources ?? throw new NotSupportedException("Native8D6A00 requires its authenticated executable resources.");
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(NativeEffectMemory ?? throw new NotSupportedException("Native8D6A00 requires its retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native8D6A00 requires its5099A0 registration.");
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native8D6A00 requires the original camera owner.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect with { Cure = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeCure8D6B30(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native8D6B30 requires its original effect owner.");
        NativeBattleCureResources resources = effect.Binding.CureResources ?? throw new NotSupportedException("Native8D6B30 requires its authenticated executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native8D6B30 requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native8D6B30 requires its previously published camera matrix.");
        NativeBattleCureFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native8D6B30 requires retained effect memory."), effect, effect.Cure ?? throw new NotSupportedException("Native8D6B30 requires its executed8D6A00 state."));
        NativeBattleCureControllerInputs inputs = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Cure anchors require the original body pose source.")) { ApplyContact = ApplyContact };
        NativeBattleCureControllerStep step = NativeBattleCureController.Advance8D6B30(frame, inputs, resources, (current, address) =>
        {
            //A contact can enqueue texture writes before this leaf. Keep that canonical process memory and all earlier direct OT writes.
            current = current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Cure lost its process memory.") };
            NativeBattleCureParticleInputs particleInputs = new(inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Cure requires its55CBD2 CRT random owner."), Execute) { Projection = projection };
            NativeBattleCureParticleStep particle = NativeBattleCureParticles.AdvancePrivate(current, address, resources, particleInputs);
            nativeCrtRandom = particle.CrtRandomAfter;
            NativeEffectMemory = particle.Frame.Memory;
            return new(particle.Frame, particle.Result);
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect with { Cure = step.Frame.State } };

        void ApplyContact(int index)
        {
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
        }

        NativeBattleCureFrame Execute(NativeBattleCureFrame current, NativeBattleCureOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleCureActorInitializeOperation initialize:
                    current = current with { State = current.State with { ActorContext = new(initialize.Target) } };
                    break;
                case NativeBattleCureActorVisibilityOperation visibility:
                    NativeBattleGraphicsModel model = graphics.Models[visibility.Target];
                    graphics = graphics with { Models = graphics.Models.SetItem(visibility.Target, model with { Flags0 = visibility.Hidden ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & ~4) }) };
                    break;
                case NativeBattleCureSpriteOperation sprite:
                    current = AppendEffect(current, NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, NativeCamera!.Roll, 17, 2));
                    break;
                case NativeBattleCurePacketOperation packets:
                    current = AppendEffect(current, packets.Batch);
                    break;
                case NativeBattleCureActorDrawOperation actor:
                    NativeBattleCureActorContext context = current.State.ActorContext ?? throw new NotSupportedException("Native8D7110 requires its executed8D8280 shared actor context.");
                    NativeBattleGraphicsModel target = graphics.Models[context.Target];
                    graphics = SubmitNativeShadowPackets5088A0(graphics, target, projection, 4112, false);
                    NativeBattleCureActorPacketBatch batch = NativeBattleCureActorAssembly.Emit8D7110(target, NativeGraphicsPoseResource(graphics, context.Target), current.State.CopiedCamera ?? throw new NotSupportedException("Native8D7110 requires8D6B30's copied camera."), projection, actor.Center, actor.VerticalOffset, effect.Binding.Effect == 50 ? NativeEffectMemory!.Potion50ContextF4 : NativeEffectMemory!.CureContextF4);
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
                    throw new NotSupportedException("Native Cure operation requires its original immediate owner.");
            }
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Cure operation lost process memory.") };
        }

        NativeBattleCureFrame AppendEffect(NativeBattleCureFrame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Cure requires its initialized original OT.");
            NativeBattleCurePacketWrite write = NativeBattleCurePacketArena.AppendPrimary(current.Memory, current.State, primitive.Pending, batch);
            NativeEffectMemory = write.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = write.Ordering } };
            return current with { Memory = write.Memory, State = write.State };
        }
    }
}
