using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeWindBlast29(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Wind Blast requires its selected original effect.");
        NativeBattleWindBlast29Resources resources = effect.Binding.WindBlast29Resources ?? throw new NotSupportedException("Wind Blast requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Wind Blast requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Wind Blast requires its actual CRT stream.");
        NativeBattleWindBlast29Frame frame = new(memory, effect, random, memory.RequireWindBlast29());
        if (initialize)
        {
            frame = NativeBattleWindBlast29Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Wind Blast requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Wind Blast requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Wind Blast requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Wind Blast requires its actual current view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = actor => graphics.Models[actor];
        NativeBattleWindBlast29Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), readModel, PoseSource, Execute, AdvanceGeneric);
        frame = NativeBattleWindBlast29Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Wind Blast requires its actual current model rig.");

        void Publish(NativeBattleWindBlast29Frame current)
        {
            NativeEffectMemory = current.Memory.WithWindBlast29(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleWindBlast29Frame Execute(NativeBattleWindBlast29Frame current, NativeBattleWindBlast29Operation operation)
        {
            switch (operation)
            {
                case NativeBattleWindBlast29Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleWindBlast29Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Wind Blast contact lost its process memory.") };
                default:
                    throw new NotSupportedException("Wind Blast requires its original immediate operation owner.");
            }
        }

        NativeBattleWindBlast29LeafStep AdvanceGeneric(NativeBattleWindBlast29Frame current, uint address)
        {
            NativeCrtRandomState genericRandom = current.Random;
            NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource)
            {
                ObserveBoneAnchor = (nodes, actor, selector) => nodes with { Memory = ObserveNativeBoneAnchor(nodes.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0)) }
            };
            NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), address, resources.GenericNodes(), genericInputs, new(AdvanceNodes));
            return new(current.WithGeneric(advanced.Frame) with { Random = genericRandom }, advanced.Result);

            NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint contextAddress)
            {
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Wind Blast generic nodes require their ordering table.");
                NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, contextAddress, genericRandom, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                genericRandom = drawn.Random;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                return drawn.Frame;
            }
        }

    }
}
