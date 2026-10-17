using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRecovery(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native recovery entry requires its selected original module.");
        NativeBattleRecoveryResources resources = effect.Binding.RecoveryResources ?? throw new NotSupportedException("Native recovery entry requires its authenticated original resource binding.");
        NativeBattleRecoveryFrame frame = NativeBattleRecoveryController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native recovery entry requires its retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        return graphics with { OriginalEffect = frame.Effect with { Recovery = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeRecovery(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native recovery callback requires its current original module.");
        NativeBattleRecoveryResources resources = effect.Binding.RecoveryResources ?? throw new NotSupportedException("Native recovery callback requires its authenticated resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native recovery callback requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native recovery callback requires the previously published camera matrix.");
        NativeBattleRecoveryFrame initial = new(NativeEffectMemory ?? throw new NotSupportedException("Native recovery callback requires its retained process memory."), effect, effect.Recovery ?? throw new NotSupportedException("Native recovery callback requires its executed original entry."));
        NativeBattleRecoveryControllerInputs inputs = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native recovery anchors require their original model pose source.")) { ApplyContact = ApplyContact };
        NativeBattleRecoveryControllerStep step = NativeBattleRecoveryController.Advance(initial, inputs, resources, (current, address) =>
        {
            //The controller's actual clear/previous callback writes must become canonical before the next immediate callback.
            NativeEffectMemory = current.Memory;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(resources.Effect);
            NativeBattleRecoveryLeafStep leaf = task.Callback == profile.GenericCallback
                ? NativeBattleRecoveryGeneric.Advance(current, address, new((frame, context) => NativeBattleRecoveryContext.Initialize(frame, context, resources, inputs), (frame, context) => NativeBattleRecoveryNodeEngine.Advance(frame, context, AdvanceNodes)))
                : NativeBattleRecoveryFade.Advance(current, address, ApplyFade);
            NativeEffectMemory = leaf.Frame.Memory;
            return leaf;
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        NativeEffectMemory = step.Frame.Memory;
        return graphics with { OriginalEffect = step.Frame.Effect with { Recovery = step.Frame.State } };

        NativeBattleRecoveryFrame ApplyContact(NativeBattleRecoveryFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native recovery contact lost its canonical process memory.") };
        }

        NativeBattleRecoveryFrame ApplyFade(NativeBattleRecoveryFrame current, NativeBattleStageFadeWrite fade)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native recovery fades require their retained four stage groups.");
            if (stage.Groups.IsDefault || stage.Groups.Length != 4)
            {
                throw new InvalidDataException("Native recovery fades require all four original stage groups.");
            }
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Fade, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
            return current;
        }

        NativeBattleRecoveryFrame AdvanceNodes(NativeBattleRecoveryFrame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native recovery drawing requires its initialized ordering table.");
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native recovery drawing requires its actual55CBD2 random owner.");
            NativeBattleRecoveryPresentationStep nodes = NativeBattleRecoveryLifecycle.AdvanceAndDraw(current, address, random, inputs.ReadModel, projection, NativeCamera!.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
            nativeCrtRandom = nodes.Random;
            NativeEffectMemory = nodes.Frame.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = nodes.Ordering } };
            return nodes.Frame;
        }
    }
}
