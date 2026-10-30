using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeGatlingGun305(NativeBattleGraphicsState graphics) => RunNativeGatlingGun305(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeGatlingGun305(NativeBattleGraphicsState graphics) => RunNativeGatlingGun305(graphics, false);

    private NativeBattleGraphicsState RunNativeGatlingGun305(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Gatling Gun requires its selected original effect.");
        NativeBattleGatlingGun305Resources resources = effect.Binding.GatlingGun305Resources ?? throw new NotSupportedException("Gatling Gun requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Gatling Gun requires its actual process memory.");
        NativeBattleGatlingGun305Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Gatling Gun requires its CRT stream."), memory.GatlingGun305 ?? throw new NotSupportedException("Gatling Gun requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Gatling Gun requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Gatling Gun requires its original interpolation constants.");
        NativeBattleGatlingGun305Inputs inputs = new(graphics.Flags, nativeActionPackets, actor => graphics.Models[actor], _ => Camera().CapturePackedView1D97778(), Bone, Anchor, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleGatlingGun305Controller.Initialize(frame, resources, inputs) : NativeBattleGatlingGun305Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Gatling Gun requires its current camera.");

        void Publish(NativeBattleGatlingGun305Frame current)
        {
            NativeEffectMemory = current.Memory with { GatlingGun305 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gatling Gun lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleGatlingGun305Frame Import(NativeBattleGatlingGun305Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Gatling Gun external service lost its process memory.");
            return current with { Memory = latest, State = latest.GatlingGun305 ?? throw new InvalidDataException("Gatling Gun external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Gatling Gun external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Gatling Gun external service lost its CRT stream.") };
        }

        NativeBattleGatlingGun305Frame Deliver(NativeBattleGatlingGun305Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleGatlingGun305Presentation.PreserveNativeDepths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Gatling Gun requires its pending ordering table.");
            NativeBattleGatlingGun305PacketWrite written = NativeBattleGatlingGun305PacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleGatlingGun305AnchorStep Anchor(NativeBattleGatlingGun305Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gatling Gun requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Gatling Gun requires the actual actor heading.")).Angle36 });
        }

        NativeBattleGatlingGun305PointStep Bone(NativeBattleGatlingGun305Frame current, int actor, short selector, short distance)
        {
            Publish(current);
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gatling Gun requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, unchecked((byte)selector), distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gatling Gun bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleGatlingGun305Frame Execute(NativeBattleGatlingGun305Frame current, NativeBattleGatlingGun305Operation operation)
        {
            switch (operation)
            {
                case NativeBattleGatlingGun305BindCaster bind:
                    if (bind.ResourceAddress != 0xCEB5E8 || bind.SaveAddress != NativeBattleGatlingGun305Profile.BindingSave)
                    {
                        throw new InvalidDataException("Gatling Gun caster binding leaves its original resource and saved row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Gatling Gun requires its current CPU rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                        {
                            return pointer;
                        }
                        if (address >= NativeBattleGatlingGun305Profile.BindingSave && address < NativeBattleGatlingGun305Profile.BindingSave + 32)
                        {
                            return current.State.ReadResourcePointer(address);
                        }
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Gatling Gun binding requires its current arena pointers.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : staging.ReadResourcePointer(address);
                    }
                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        current = current with { State = current.State.WriteResourcePointer(address, pointer) };
                    }
                case NativeBattleGatlingGun305RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleGatlingGun305Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleGatlingGun305Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleGatlingGun305Stage fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Gatling Gun requires its current stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Depth, ColorFlags = fade.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleGatlingGun305DrawStatic88 draw:
                    return DrawMesh(current, draw.TemporaryAddress, draw.SourceAddress, draw.TableBaseBucket, unchecked((byte)(draw.DepthShift & 31)), pilotIdentity: draw.PilotIdentity);
                case NativeBattleGatlingGun305DrawSprite180 draw:
                    return DrawSprite(current, draw.TemporaryAddress, draw.SourceAddress, draw.Phase, draw.DepthShift, pilotIdentity: draw.PilotIdentity);
                default:
                    throw new NotSupportedException("Gatling Gun operation requires its original native owner.");
            }
        }

        NativeBattleGatlingGun305Frame DrawMesh(NativeBattleGatlingGun305Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            const int length = 88;
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gatling Gun requires its physical drawing context.");
            NativeBattleEffectArena temporary = NativeGatlingGun305View(staging, address, length);
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleCameraProjection projection = Camera().Projection ?? throw new NotSupportedException("Gatling Gun requires its published projection.");
            NativeBattleGeometryRegisters registers = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Gatling Gun requires its current matrix owner.");
            NativeBattleActionMatrix matrix = NativeBattleGeometry.CaptureMatrix(registers);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(current.Memory, mesh, matrix, projection, temporary, colors, matrixPadding18: registers.Rotation20[18] is byte low && registers.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null, tableBaseBucket: bucket, depthShift: shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, length) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleGatlingGun305Frame DrawSprite(NativeBattleGatlingGun305Frame current, uint address, uint sourceAddress, ushort phase, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gatling Gun sprite requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeGatlingGun305View(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Gatling Gun sprite differs from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => resources.ReadSprite(selected), Camera().Projection ?? throw new NotSupportedException("Gatling Gun sprite requires its current projection."), Camera().Roll, 17, depthShift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }

    private static NativeBattleEffectArena NativeGatlingGun305View(NativeBattleFileStaging staging, uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

}
