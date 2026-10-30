using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDegenerator306(NativeBattleGraphicsState graphics) => RunNativeDegenerator306(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeDegenerator306(NativeBattleGraphicsState graphics) => RunNativeDegenerator306(graphics, false);

    private NativeBattleGraphicsState RunNativeDegenerator306(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Degenerator requires its selected original effect.");
        NativeBattleDegenerator306Resources resources = effect.Binding.Degenerator306Resources ?? throw new NotSupportedException("Degenerator requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Degenerator requires its actual process memory.");
        NativeBattleDegenerator306Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Degenerator requires its CRT stream."), memory.Degenerator306 ?? throw new NotSupportedException("Degenerator requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Degenerator requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Degenerator requires its original interpolation constants.");
        NativeBattleDegenerator306Inputs inputs = new(graphics.Flags, nativeActionPackets, actor => graphics.Models[actor], _ => Camera().CapturePackedView1D97778(), () => Camera().FocalLength ?? throw new NotSupportedException("Degenerator requires its retained focal shadow."), actor => NativeBattleRaldo19BallController.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), 0x0C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), Bone, Anchor, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleDegenerator306Controller.Initialize(frame, resources, inputs) : NativeBattleDegenerator306Controller.Advance(frame, resources, inputs);
        if (!initialize && frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Degenerator requires its current camera.");

        void Publish(NativeBattleDegenerator306Frame current)
        {
            NativeEffectMemory = current.Memory with { Degenerator306 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Degenerator lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleDegenerator306Frame Import(NativeBattleDegenerator306Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Degenerator external service lost its process memory.");
            return current with { Memory = latest, State = latest.Degenerator306 ?? throw new InvalidDataException("Degenerator external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Degenerator external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Degenerator external service lost its CRT stream.") };
        }

        NativeBattleDegenerator306Frame Deliver(NativeBattleDegenerator306Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleDegenerator306Presentation.PreserveNativeDepths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Degenerator requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleDegenerator306Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleDegenerator306Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleDegenerator306AnchorStep Anchor(NativeBattleDegenerator306Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Degenerator requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Degenerator requires the actual actor heading.")).Angle36 });
        }

        NativeBattleDegenerator306PointStep Bone(NativeBattleDegenerator306Frame current, int actor, short selector, short distance)
        {
            Publish(current);
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Degenerator requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, unchecked((byte)selector), distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Degenerator bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleDegenerator306Frame Execute(NativeBattleDegenerator306Frame current, NativeBattleDegenerator306Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDegenerator306BindCaster bind:
                    if (bind.ResourceAddress != 0xCE454C || bind.SaveAddress != NativeBattleDegenerator306Profile.BindingSave)
                    {
                        throw new InvalidDataException("Degenerator caster binding leaves its original resource and saved row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Degenerator requires its current CPU rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                        {
                            return pointer;
                        }
                        if (address >= NativeBattleDegenerator306Profile.BindingSave && address < NativeBattleDegenerator306Profile.BindingSave + 32)
                        {
                            return current.State.ReadResourcePointer(address);
                        }
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Degenerator binding requires its current arena pointers.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : staging.ReadResourcePointer(address);
                    }
                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        current = current with { State = current.State.WriteResourcePointer(address, pointer) };
                    }
                case NativeBattleDegenerator306RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleDegenerator306Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleDegenerator306Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleDegenerator306Stage fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Degenerator requires its current stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Depth, ColorFlags = fade.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleDegenerator306StageFlags flags:
                    NativeBattleStageState flaggedStage = NativeStage ?? throw new NotSupportedException("Degenerator requires its current stage flags.");
                    NativeStage = flaggedStage with { Groups = flaggedStage.Groups.Select(group => group with { Flags = (byte)((group.Flags & ~flags.ClearMask) | flags.SetMask) }).ToImmutableArray() };
                    return current;
                case NativeBattleDegenerator306ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = (ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask) }) };
                    return current;
                case NativeBattleDegenerator306ActorAlpha alpha:
                    graphics = graphics with { Models = graphics.Models.SetItem(alpha.Actor, graphics.Models[alpha.Actor] with { Alpha7 = alpha.Value }) };
                    return current;
                case NativeBattleDegenerator306ActorRotation8 rotation:
                    graphics = graphics with { Models = graphics.Models.SetItem(rotation.Actor, NativeBattleRaldo19ModelMemory.Apply(NativeBattleRaldo19ModelMemory.Apply(graphics.Models[rotation.Actor], 0x0C, rotation.Packed8, 0, 4), 0x10, rotation.Packed8, 4, 4)) };
                    return current;
                case NativeBattleDegenerator306ActorPackedPosition8 position:
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, NativeBattleRaldo19ModelMemory.Apply(NativeBattleRaldo19ModelMemory.Apply(graphics.Models[position.Actor], 0x1C, position.Packed8, 0, 4), 0x20, position.Packed8, 4, 4)) };
                    return current;
                case NativeBattleDegenerator306ActorPosition position:
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, position.Position.X);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), position.Position.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), position.Position.Z);
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, NativeBattleRaldo19ModelMemory.Apply(NativeBattleRaldo19ModelMemory.Apply(graphics.Models[position.Actor], 0x1C, NativeBattleEffectArena.Clear().Write(0, xyz), 0, 4), 0x20, NativeBattleEffectArena.Clear().Write(0, xyz), 4, 2)) };
                    return current;
                case NativeBattleDegenerator306RestoreShadow restore:
                    graphics = graphics with { Models = graphics.Models.SetItem(restore.Actor, graphics.Models[restore.Actor] with { Color40 = colors.Transition }) };
                    return current;
                case NativeBattleDegenerator306ActorRecolor recolor:
                    NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Degenerator requires its current color control owner.");
                    NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Degenerator requires its current far controls.");
                    NativeBattleScriptedColorRegisters colorRegisters = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Degenerator requires its current GTE data registers.");
                    far = LoadFar(colors.Normal, far);
                    NativeBattleScriptedDpcsResult normal = NativeBattleScriptedDpcs.Transform45F270(colorRegisters, 0x32000000, unchecked((uint)recolor.Cue), far, colors);
                    far = LoadFar(colors.Transition, far);
                    NativeBattleScriptedDpcsResult shadow = NativeBattleScriptedDpcs.Transform45F270(normal.Registers, 0, normal.Registers.Ir0!.Value, far, colors);
                    graphics = graphics with { Models = graphics.Models.SetItem(recolor.Actor, graphics.Models[recolor.Actor] with { Color44 = normal.PackedRgb, Color40 = (shadow.PackedRgb & 0xFFFFFF) | 0x02000000 }) };
                    return current with { Memory = current.Memory with { ScriptedColorRegisters = shadow.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
                case NativeBattleDegenerator306CommonTexture texture:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(texture.SourceAddress, texture.CompletionAddress, (address, value) => current = NativeBattleDegenerator306Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleDegenerator306DrawStatic88 draw:
                    return DrawMesh(current, draw.TemporaryAddress, draw.SourceAddress, false, draw.TableBaseBucket, unchecked((byte)(draw.DepthShift & 31)), pilotIdentity: draw.PilotIdentity);
                case NativeBattleDegenerator306DrawCustom108 draw:
                    return DrawMesh(current, draw.TemporaryAddress, resources.CustomMesh.Address, true, 17, 2, pilotIdentity: draw.PilotIdentity);
                case NativeBattleDegenerator306DrawSprite180 draw:
                    return DrawSprite(current, draw.TemporaryAddress, draw.SourceAddress, draw.Phase, draw.DepthShift, pilotIdentity: draw.PilotIdentity);
                case NativeBattleDegenerator306DrawBody136 draw:
                    Publish(current);
                    var body = DrawNativeDegenerator306Body(current, graphics, draw.Actor, draw.TemporaryAddress, pilotIdentity: draw.PilotIdentity);
                    graphics = body.Graphics;
                    return body.Frame;
                default:
                    throw new NotSupportedException("Degenerator operation requires its original native owner.");
            }
        }

        NativeBattleModelRendererRegisters LoadFar(uint rgb, NativeBattleModelRendererRegisters far) => far with { FarRed = (rgb & 255) << 4, FarGreen = ((rgb >> 8) & 255) << 4, FarBlue = ((rgb >> 16) & 255) << 4 };

        NativeBattleDegenerator306Frame DrawMesh(NativeBattleDegenerator306Frame current, uint address, uint sourceAddress, bool custom, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            int length = custom ? 108 : 88;
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Degenerator requires its physical drawing context.");
            NativeBattleEffectArena temporary = NativeDegenerator306View(staging, address, length);
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleCameraProjection projection = Camera().Projection ?? throw new NotSupportedException("Degenerator requires its published projection.");
            NativeBattleGfGeometryBatch batch;
            if (custom)
            {
                NativeBattleDegenerator306CustomMeshStep drawn = NativeBattleDegenerator306CustomMesh.Draw5A3AC0(current.Memory, mesh, temporary, projection, colors, bucket, shift, pilotIdentity: pilotIdentity);
                current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context108, length) };
                batch = drawn.Batch;
            }
            else
            {
                NativeBattleGeometryRegisters registers = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Degenerator requires its current matrix owner.");
                NativeBattleActionMatrix matrix = NativeBattleGeometry.CaptureMatrix(registers);
                NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(current.Memory, mesh, matrix, projection, temporary, colors, matrixPadding18: registers.Rotation20[18] is byte low && registers.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null, tableBaseBucket: bucket, depthShift: shift, pilotIdentity: pilotIdentity);
                current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, length) };
                batch = drawn.Batch;
            }
            return Deliver(current, batch);
        }

        NativeBattleDegenerator306Frame DrawSprite(NativeBattleDegenerator306Frame current, uint address, uint sourceAddress, ushort phase, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Degenerator sprite requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeDegenerator306View(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Degenerator sprite differs from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => resources.ReadSprite(selected), Camera().Projection ?? throw new NotSupportedException("Degenerator sprite requires its current projection."), Camera().Roll, 17, depthShift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
