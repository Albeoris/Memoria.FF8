using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativePsychoBlast23(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Psycho Blast requires its selected original effect.");
        NativeBattlePsychoBlast23Resources resources = effect.Binding.PsychoBlast23Resources ?? throw new NotSupportedException("Psycho Blast requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Psycho Blast requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Psycho Blast requires its actual CRT stream.");
        NativeBattlePsychoBlast23Frame frame = new(memory, effect, random, memory.RequirePsychoBlast23());
        if (initialize)
        {
            frame = NativeBattlePsychoBlast23Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Psycho Blast requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Psycho Blast requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Psycho Blast requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Psycho Blast requires its actual current view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = actor => graphics.Models[actor];
        NativeBattlePsychoBlast23Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), readModel, PoseSource, Execute, AdvanceGeneric, AdvanceOverlay);
        frame = NativeBattlePsychoBlast23Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Psycho Blast requires its actual current model rig.");

        void Publish(NativeBattlePsychoBlast23Frame current)
        {
            NativeEffectMemory = current.Memory.WithPsychoBlast23(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattlePsychoBlast23Frame Execute(NativeBattlePsychoBlast23Frame current, NativeBattlePsychoBlast23Operation operation)
        {
            switch (operation)
            {
                case NativeBattlePsychoBlast23Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattlePsychoBlast23Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Psycho Blast contact lost its process memory.") };
                case NativeBattlePsychoBlast23Rotate rotate:
                    NativeBattleGeometryRegisterStep rotated = ObserveNativeGeometryRotation(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Psycho Blast rotation requires its geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Psycho Blast rotation requires its data registers.")), rotate.Matrix, rotate.Axis, rotate.Angle, rotate.Padding18 is short padding ? unchecked((ushort)padding) : null);
                    return current with { Memory = current.Memory with { GeometryRegisters = rotated.Geometry, ScriptedColorRegisters = rotated.Colors } };
                case NativeBattlePsychoBlast23TargetAnchor anchor:
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[anchor.Actor], PoseSource(anchor.Actor), anchor.Selector, anchor.Distance);
                    return current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
                case NativeBattlePsychoBlast23LookBasis basis:
                    return ObserveBasis(current, basis.Direction, basis.Up);
                case NativeBattlePsychoBlast23TextureLoad load:
                    uint completion = checked(load.RootTaskAddress + 0x62);
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.SourceArenaOffset), completion, (address, value) => current = WriteCompletion(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattlePsychoBlast23ActDraw act:
                    (graphics, current) = DrawNativePsychoBlast23Act(graphics, current, act.Address);
                    return current;
                case NativeBattlePsychoBlast23DebrisDraw debris:
                    (graphics, current) = DrawNativePsychoBlast23Debris(graphics, current, debris.Address);
                    return current;
                default:
                    throw new NotSupportedException("Psycho Blast requires its original immediate operation owner.");
            }
        }

        NativeBattlePsychoBlast23LeafStep AdvanceGeneric(NativeBattlePsychoBlast23Frame current, uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.ReadByte(0x29) == 0 && unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
            {
                task = task.WriteDword(0x290, BinaryPrimitives.ReadUInt32LittleEndian(current.State.Basis56.Read(0x20, 4))).WriteDword(0x294, BinaryPrimitives.ReadUInt32LittleEndian(current.State.Basis56.Read(0x24, 4)));
                current = current with { Effect = current.Effect with { Pools = hierarchy.Write(address, task).Pools } };
            }
            NativeCrtRandomState genericRandom = current.Random;
            NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource)
            {
                ObserveBoneAnchor = (nodes, actor, selector) => nodes with { Memory = ObserveNativeBoneAnchor(nodes.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0)) }
            };
            NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), address, resources.GenericNodes(), genericInputs, new(AdvanceNodes));
            return new(current.WithGeneric(advanced.Frame) with { Random = genericRandom }, advanced.Result);

            NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint contextAddress)
            {
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Psycho Blast generic nodes require their ordering table.");
                NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, contextAddress, genericRandom, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                genericRandom = drawn.Random;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                return drawn.Frame;
            }
        }

        NativeBattlePsychoBlast23LeafStep AdvanceOverlay(NativeBattlePsychoBlast23Frame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Psycho Blast overlay requires its ordering table.");
            NativeBattleFullscreenOverlayStep advanced = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, primitive.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Psycho Blast requires its actual fullscreen pause owner."), resources.Overlay);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Frame.Ordering } };
            return new(current with { Memory = advanced.Frame.Memory, Effect = current.Effect with { Pools = advanced.Frame.Pools } }, advanced.Result);
        }

        NativeBattlePsychoBlast23Frame ObserveBasis(NativeBattlePsychoBlast23Frame current, NativeBattleVector direction, NativeBattleVector up)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Psycho Blast basis requires its retained temporary memory."), 32);
            NativeBattleGeometryRegisterStep cross = NativeBattleGeometry.LoadDiagonal45DC70(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Psycho Blast basis requires its geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Psycho Blast basis requires its data registers.")), up);
            cross = NativeBattleGeometry.LoadSignedIr45DC40(cross, direction);
            cross = NativeBattleGeometry.OuterProduct45E820(cross);
            byte[] vectors = new byte[18];
            for (int index = 0; index < 3; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 2), unchecked((short)cross.Colors.Ir[index]!.Value));
            }
            short[] source = [up.X, up.Y, up.Z, direction.X, direction.Y, direction.Z];
            for (int index = 0; index < source.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(6 + index * 2), source[index]);
            }
            NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(allocation.Staging.Write(allocation.Address, vectors), 32);
            return current with { Memory = current.Memory with { GeometryRegisters = cross.Geometry, ScriptedColorRegisters = cross.Colors } };
        }

        static NativeBattlePsychoBlast23Frame WriteCompletion(NativeBattlePsychoBlast23Frame current, uint address, byte value)
        {
            return current with { Effect = WriteNativePsychoBlast23CompletionByte(current.Effect, address, value) };
        }
    }
}
