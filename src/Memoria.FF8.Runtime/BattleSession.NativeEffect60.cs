using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect60(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect60 requires its selected original effect.");
        NativeBattleEffect60Resources resources = effect.Binding.Effect60Resources ?? throw new NotSupportedException("Effect60 requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect60 requires its actual process memory.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Effect60 requires its actual CRT stream.");
        NativeBattleEffect60Frame frame = new(memory, effect, random, memory.RequireEffect60());
        if (initialize)
        {
            frame = NativeBattleEffect60Controller.Initialize(memory, effect, random, nativeActionPackets, resources, frame.State);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Effect60 requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect60 requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect60 requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Effect60 requires its actual current view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = actor => graphics.Models[actor];
        NativeBattleEffect60Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), readModel, PoseSource, Execute, AdvanceGeneric)
        {
            ReadRotation8 = actor => PackedModel(actor, 0x0C),
            ReadPackedPosition8 = actor => PackedModel(actor, 0x1C),
            ReadWorld32 = actor => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect60 requires its physical model matrix."), null)
        };
        frame = NativeBattleEffect60Controller.Advance(frame, resources, inputs);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect60 requires its actual current model rig.");

        void Publish(NativeBattleEffect60Frame current)
        {
            NativeEffectMemory = current.Memory.WithEffect60(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect60Frame Execute(NativeBattleEffect60Frame current, NativeBattleEffect60Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect60Sound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleEffect60Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Effect60 contact lost its process memory.") };
                case NativeBattleEffect60Rotate rotate:
                    NativeBattleGeometryRegisterStep rotated = ObserveNativeGeometryRotation(new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect60 rotation requires its geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect60 rotation requires its data registers.")), rotate.Matrix, rotate.Axis, rotate.Angle, rotate.Padding18 is short padding ? unchecked((ushort)padding) : null);
                    return current with { Memory = current.Memory with { GeometryRegisters = rotated.Geometry, ScriptedColorRegisters = rotated.Colors } };
                case NativeBattleEffect60TargetAnchor anchor:
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[anchor.Actor], PoseSource(anchor.Actor), anchor.Selector, anchor.Distance);
                    return current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer) };
                case NativeBattleEffect60SetYaw yaw:
                    byte[] word = new byte[2];
                    BinaryPrimitives.WriteInt16LittleEndian(word, yaw.Value);
                    WriteActor(yaw.Actor, 0x0E, NativeBattleEffectArena.Clear().Write(0, word), 0, 2);
                    return current;
                case NativeBattleEffect60SetPackedPosition position:
                    WriteActor(position.Actor, 0x1C, position.Packed8, 0, 4);
                    WriteActor(position.Actor, 0x20, position.Packed8, 4, 4);
                    return current;
                case NativeBattleEffect60RestoreRotation rotation:
                    WriteActor(rotation.Actor, 0x0C, rotation.Packed8, 0, 4);
                    WriteActor(rotation.Actor, 0x10, rotation.Packed8, 4, 4);
                    return current;
                case NativeBattleEffect60StageInitialize:
                    NativeBattleStageState stage = RequireStage();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000 }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect60StageFade fade:
                    stage = RequireStage();
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Value }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect60TextureLoad load:
                    uint completion = checked(load.RootTaskAddress + 0x62);
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.SourceArenaOffset), completion, (address, value) => current = WriteCompletion(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect60ActDraw act:
                    (graphics, current) = DrawNativeEffect60Act(graphics, current, act.Address);
                    return current;
                case NativeBattleEffect60DebrisDraw debris:
                    (graphics, current) = DrawNativeEffect60Debris(graphics, current, debris.Address);
                    return current;
                default:
                    throw new NotSupportedException("Effect60 requires its original immediate operation owner.");
            }
        }

        NativeBattleEffect60LeafStep AdvanceGeneric(NativeBattleEffect60Frame current, uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.ReadByte(0x29) == 0 && unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
            {
                int actor = task.ReadByte(0x2C);
                NativeBattleBoneAnchorProducer anchor = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), 0x19, 0x400);
                current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, anchor) };
                NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect60 requires its actual anchor outputs.");
                NativeBattleVector vector = anchor.Enabled ? new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value)) : anchor.DisabledPosition;
                task = task.WriteWord(0x290, unchecked((ushort)vector.X)).WriteWord(0x292, unchecked((ushort)vector.Y)).WriteWord(0x294, unchecked((ushort)vector.Z));
                current = current with { Effect = current.Effect with { Pools = hierarchy.Write(address, task).Pools } };
            }
            NativeCrtRandomState genericRandom = current.Random;
            NativeBattleCuragaControllerInputs genericInputs = new(nativeActionPackets, view, readModel, PoseSource)
            {
                ObserveBoneAnchor = (nodes, actor, selector) => nodes with { Memory = ObserveNativeBoneAnchor(nodes.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0)) }
            };
            NativeBattleCuragaLeafStep advanced = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current.ToGeneric(), address, resources.GenericNodes(task.ReadDword(0x30)), genericInputs, new(AdvanceNodes));
            return new(current.WithGeneric(advanced.Frame) with { Random = genericRandom }, advanced.Result);

            NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame nodes, uint contextAddress)
            {
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect60 generic nodes require their ordering table.");
                NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(nodes, contextAddress, genericRandom, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
                genericRandom = drawn.Random;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
                return drawn.Frame;
            }
        }

        NativeBattleEffectArena PackedModel(int actor, int offset)
        {
            return NativeBattleRaldo19BallController.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        }

        void WriteActor(int actor, int offset, NativeBattleEffectArena bytes, int sourceOffset, int length)
        {
            NativeBattleGraphicsModel updated = NativeBattleRaldo19ModelMemory.Apply(graphics.Models[actor], offset, bytes, sourceOffset, length);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, updated) };
        }

        NativeBattleStageState RequireStage()
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect60 requires its four actual stage groups.");
            if (stage.Groups.Length != 4)
            {
                throw new InvalidDataException("Effect60 requires four physical stage descriptors.");
            }
            return stage;
        }

        static NativeBattleEffect60Frame WriteCompletion(NativeBattleEffect60Frame current, uint address, byte value)
        {
            return current with { Effect = WriteNativeEffect60CompletionByte(current.Effect, address, value) };
        }
    }
}
