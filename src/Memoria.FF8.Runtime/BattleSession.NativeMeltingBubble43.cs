using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMeltingBubble43(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Melting Bubble requires its selected original effect.");
        NativeBattleMeltingBubble43Resources resources = effect.Binding.MeltingBubble43Resources ?? throw new NotSupportedException("Melting Bubble requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Melting Bubble requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Melting Bubble requires its actual CRT stream.");
        NativeBattleMeltingBubble43Frame frame = new(memory, effect, random, memory.RequireMeltingBubble43());
        if (initialize)
        {
            frame = NativeBattleMeltingBubble43Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Melting Bubble requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Melting Bubble requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Melting Bubble requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Melting Bubble requires its actual current view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = actor => graphics.Models[actor];
        NativeBattleMeltingBubble43Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), readModel, PoseSource, Execute, AdvanceGeneric);
        frame = NativeBattleMeltingBubble43Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Melting Bubble requires its actual current model rig.");

        void Publish(NativeBattleMeltingBubble43Frame current)
        {
            NativeEffectMemory = current.Memory.WithMeltingBubble43(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleMeltingBubble43Frame Execute(NativeBattleMeltingBubble43Frame current, NativeBattleMeltingBubble43Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMeltingBubble43Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleMeltingBubble43Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Melting Bubble contact lost its process memory.") };
                default:
                    throw new NotSupportedException("Melting Bubble requires its original immediate operation owner.");
            }
        }

        NativeBattleMeltingBubble43LeafStep AdvanceGeneric(NativeBattleMeltingBubble43Frame current, uint address)
        {
            NativeCrtRandomState genericRandom = current.Random;
            NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource)
            {
                ObserveBoneAnchor = (nodes, actor, selector) => nodes with { Memory = ObserveNativeBoneAnchor(nodes.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0)) }
            };
            NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), address, resources.GenericNodes(), genericInputs, new(AdvanceNodes) { BeforeAdvanceContext = UpdateCasterAnchor });
            return new(current.WithGeneric(advanced.Frame) with { Random = genericRandom }, advanced.Result);

            NativeBattleCuragaFrame UpdateCasterAnchor(NativeBattleCuragaFrame nodes, uint contextAddress)
            {
                NativeBattleEffectHierarchy hierarchy = new(nodes.Effect.Pools);
                NativeBattleModulePoolTask task = hierarchy.Read(contextAddress);
                if (unchecked((short)task.ReadWord(0x29C)) == 2)
                {
                    return nodes;
                }
                int actor = task.ReadByte(0x2C);
                NativeBattleBoneAnchorProducer anchor = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), 0xF0, 0);
                NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(nodes.Memory, anchor);
                NativeBattleScriptedColorRegisters registers = observed.ScriptedColorRegisters ?? throw new NotSupportedException("Melting Bubble requires its current anchor outputs.");
                NativeBattleVector vector = anchor.Enabled ? new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value)) : anchor.DisabledPosition;
                short z = unchecked((short)(vector.Z + 256));
                task = task.WriteDword(0x34 + 0x1F4, unchecked((uint)((int)vector.X << 16))).WriteDword(0x34 + 0x1F8, unchecked((uint)((int)vector.Y << 16))).WriteDword(0x34 + 0x1FC, unchecked((uint)((int)z << 16)));
                return nodes with { Memory = observed, Effect = nodes.Effect with { Pools = hierarchy.Write(contextAddress, task).Pools } };
            }

            NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint contextAddress)
            {
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Melting Bubble generic nodes require their ordering table.");
                NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, contextAddress, genericRandom, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                genericRandom = drawn.Random;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                return drawn.Frame;
            }
        }

    }
}
