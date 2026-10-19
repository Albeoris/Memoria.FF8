using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeConfuse36(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Confuse36 entry requires its selected original module.");
        NativeBattleConfuse36Resources resources = effect.Binding.Confuse36Resources ?? throw new NotSupportedException("Native Confuse36 entry requires its authenticated original resource binding.");
        NativeBattleConfuse36Frame frame = NativeBattleConfuse36Controller.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Confuse36 entry requires its retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Confuse36 entry requires its actual5099A0 registration.");
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Confuse36 entry requires its original camera owner.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect with { Confuse36 = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeConfuse36(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Confuse36 callback requires its current original module.");
        NativeBattleConfuse36Resources resources = effect.Binding.Confuse36Resources ?? throw new NotSupportedException("Native Confuse36 callback requires its authenticated resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Confuse36 callback requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Confuse36 callback requires the previously published camera matrix.");
        NativeBattleConfuse36Frame initial = new(NativeEffectMemory ?? throw new NotSupportedException("Native Confuse36 callback requires its retained process memory."), effect, effect.Confuse36 ?? throw new NotSupportedException("Native Confuse36 callback requires its executed original entry."));
        NativeBattleConfuse36ControllerInputs inputs = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Confuse36 anchors require their original model pose source.")) { ApplyContact = ApplyContact, TransformTarget = TransformTarget };
        NativeBattleConfuse36ControllerStep step = NativeBattleConfuse36Controller.Advance(initial, inputs, resources, (current, address) =>
        {
            //The controller's actual clear/previous callback writes must become canonical before the next immediate callback.
            NativeEffectMemory = current.Memory;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleConfuse36Profile profile = NativeBattleConfuse36Profile.ForEffect(resources.Effect);
            NativeBattleConfuse36LeafStep leaf = task.Callback == profile.GenericCallback
                ? NativeBattleConfuse36Generic.Advance(current, address, new((frame, context) => NativeBattleConfuse36Context.Initialize(frame, context, resources, inputs), (frame, context) => NativeBattleConfuse36NodeEngine.Advance(frame, context, AdvanceNodes)))
                : NativeBattleConfuse36Auxiliary.Advance(current, address, resources, Append, value =>
                {
                    NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Confuse shake requires its current camera owner.");
                    NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native Confuse shake requires its current view translation WORDs.");
                    NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = value } };
                });
            NativeEffectMemory = leaf.Frame.Memory;
            return leaf;
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        NativeEffectMemory = step.Frame.Memory;
        return graphics with { OriginalEffect = step.Frame.Effect with { Confuse36 = step.Frame.State } };

        NativeBattleConfuse36Frame ApplyContact(NativeBattleConfuse36Frame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Confuse36 contact lost its canonical process memory.") };
        }

        NativeBattleConfuse36Frame TransformTarget(NativeBattleConfuse36Frame current, byte actor)
        {
            if (((graphics.Models[actor].ResourceFlags44 ?? throw new NotSupportedException("Native505CE0 requires its selected resource flags.")) & 0x140) != 0)
            {
                graphics = SelectNativeHeaderSequence(graphics, actor, 4);
            }
            return current;
        }

        NativeBattleConfuse36Frame Append(NativeBattleConfuse36Frame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Confuse requires its initialized physical ordering table.");
            int cursor = current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Confuse requires its current module packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State with { PrimaryDrawCursor = written.Cursor } };
        }

        NativeBattleConfuse36Frame AdvanceNodes(NativeBattleConfuse36Frame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Confuse36 drawing requires its initialized ordering table.");
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Confuse36 drawing requires its actual55CBD2 random owner.");
            NativeBattleConfuse36PresentationStep nodes = NativeBattleConfuse36Lifecycle.AdvanceAndDraw(current, resources, address, random, inputs.ReadModel, projection, NativeCamera!.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
            nativeCrtRandom = nodes.Random;
            NativeEffectMemory = nodes.Frame.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = nodes.Ordering } };
            return nodes.Frame;
        }
    }
}
