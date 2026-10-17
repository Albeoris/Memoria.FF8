using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRestoration(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native restoration entry requires its selected original module.");
        NativeBattleRestorationResources resources = effect.Binding.RestorationResources ?? throw new NotSupportedException("Native restoration entry requires its authenticated original resource binding.");
        NativeBattleRestorationFrame frame = NativeBattleRestorationController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native restoration entry requires its retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native restoration entry requires its actual5099A0 registration.");
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native restoration entry requires its original camera owner.")) with { ControlResource = textures.Animation, ControlCursor = 0 };
        }
        return graphics with { OriginalEffect = frame.Effect with { Restoration = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeRestoration(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native restoration callback requires its current original module.");
        NativeBattleRestorationResources resources = effect.Binding.RestorationResources ?? throw new NotSupportedException("Native restoration callback requires its authenticated resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native restoration callback requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native restoration callback requires the previously published camera matrix.");
        NativeBattleRestorationFrame initial = new(NativeEffectMemory ?? throw new NotSupportedException("Native restoration callback requires its retained process memory."), effect, effect.Restoration ?? throw new NotSupportedException("Native restoration callback requires its executed original entry."));
        NativeBattleRestorationControllerInputs inputs = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native restoration anchors require their original model pose source.")) { ApplyContact = ApplyContact };
        NativeBattleRestorationControllerStep step = NativeBattleRestorationController.Advance(initial, inputs, resources, (current, address) =>
        {
            //The controller's actual clear/previous callback writes must become canonical before the next immediate callback.
            NativeEffectMemory = current.Memory;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(resources.Effect);
            NativeBattleRestorationLeafStep leaf = task.Callback == profile.GenericCallback
                ? NativeBattleRestorationGeneric.Advance(current, address, new((frame, context) => NativeBattleRestorationContext.Initialize(frame, context, resources, inputs), (frame, context) => NativeBattleRestorationNodeEngine.Advance(frame, context, AdvanceNodes)))
                : NativeBattleRestorationFade.Advance(current, address, ApplyFade);
            NativeEffectMemory = leaf.Frame.Memory;
            return leaf;
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        NativeEffectMemory = step.Frame.Memory;
        return graphics with { OriginalEffect = step.Frame.Effect with { Restoration = step.Frame.State } };

        NativeBattleRestorationFrame ApplyContact(NativeBattleRestorationFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native restoration contact lost its canonical process memory.") };
        }

        NativeBattleRestorationFrame ApplyFade(NativeBattleRestorationFrame current, NativeBattleStageFadeWrite fade)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native restoration fades require their retained four stage groups.");
            if (stage.Groups.IsDefault || stage.Groups.Length != 4)
            {
                throw new InvalidDataException("Native restoration fades require all four original stage groups.");
            }
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Fade, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
            return current;
        }

        NativeBattleRestorationFrame AdvanceNodes(NativeBattleRestorationFrame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native restoration drawing requires its initialized ordering table.");
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native restoration drawing requires its actual55CBD2 random owner.");
            NativeBattleRestorationPresentationStep nodes = NativeBattleRestorationLifecycle.AdvanceAndDraw(current, address, random, inputs.ReadModel, projection, NativeCamera!.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
            nativeCrtRandom = nodes.Random;
            NativeEffectMemory = nodes.Frame.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = nodes.Ordering } };
            return nodes.Frame;
        }
    }
}
