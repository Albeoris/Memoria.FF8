using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeElectrocute275(NativeBattleGraphicsState graphics) => RunNativeElectrocute275(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeElectrocute275(NativeBattleGraphicsState graphics) => RunNativeElectrocute275(graphics, false);

    private NativeBattleGraphicsState RunNativeElectrocute275(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Electrocute requires its selected original effect.");
        NativeBattleElectrocute275Resources resources = effect.Binding.Electrocute275Resources ?? throw new NotSupportedException("Electrocute requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Electrocute requires its actual process memory.");
        NativeBattleElectrocute275Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Electrocute requires its CRT stream."), memory.Electrocute275 ?? throw new NotSupportedException("Electrocute requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Electrocute requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Electrocute requires its original interpolation constants.");
        NativeBattleElectrocute275Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Electrocute requires its current skeleton.")).BoneCount, actor => graphics.Models[actor].ActiveStagedResourceRow100 ?? throw new NotSupportedException("Electrocute requires its current addressed caster binding."), _ => Camera().CapturePackedView1D97778(), Anchor, Bone, AdvanceAct, Execute) { Colors = colors };
        frame = initialize ? NativeBattleElectrocute275Controller.Initialize(frame, resources, inputs) : NativeBattleElectrocute275Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Electrocute requires its current camera.");

        void Publish(NativeBattleElectrocute275Frame current)
        {
            NativeEffectMemory = current.Memory with { Electrocute275 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Electrocute lost its addressed memory.");
            nativeCrtRandom = current.Random;
            graphics = graphics with { OriginalEffect = current.Effect };
        }

        NativeBattleElectrocute275Frame Import(NativeBattleElectrocute275Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Electrocute external service lost its process memory.");
            return current with { Effect = graphics.OriginalEffect ?? throw new InvalidDataException("Electrocute lost its current effect tasks."), Memory = latest, State = latest.Electrocute275 ?? throw new InvalidDataException("Electrocute external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Electrocute external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Electrocute external service lost its CRT stream.") };
        }

        NativeBattleElectrocute275Frame Deliver(NativeBattleElectrocute275Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Electrocute requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleElectrocute275Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleElectrocute275Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleElectrocute275AnchorStep Anchor(NativeBattleElectrocute275Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Electrocute requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Electrocute requires its actual actor heading.")).Angle36 });
        }

        NativeBattleElectrocute275PointStep Bone(NativeBattleElectrocute275Frame current, int actor, short selector, short distance)
        {
            Publish(current);
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Electrocute requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, checked((byte)selector), distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Electrocute bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleElectrocute275Frame Execute(NativeBattleElectrocute275Frame current, NativeBattleElectrocute275Operation operation)
        {
            Publish(current);
            switch (operation)
            {
                case NativeBattleElectrocute275BindCaster bind:
                    if (bind.ResourceAddress != 0x10DA574 || bind.SaveAddress != current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleElectrocute275Profile.MainDescriptor).BankAddress + 0x14)
                    {
                        throw new InvalidDataException("Electrocute caster binding leaves its original directory and arena row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Electrocute requires its CPU resource rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                        {
                            return pointer;
                        }
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Electrocute requires its current arena.")).ReadResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress))) : staging.ReadResourcePointer(address);
                    }

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } };
                    }
                case NativeBattleElectrocute275RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleElectrocute275SpatialSound sound:
                    PublishNativeModuleSound(sound.Value, checked((uint)sound.Volume), sound.Position);
                    return Import(current);
                case NativeBattleElectrocute275Flash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.Argument0), unchecked((short)flash.Argument1), unchecked((short)flash.Argument2), unchecked((short)flash.Argument3)) };
                    return current;
                case NativeBattleElectrocute275Stage stage:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Electrocute requires its retained stage color rows.");
                    NativeStage = stageState with { Groups = stageState.Groups.Select(group => group with { ColorDepth = stage.Depth, ColorFlags = stage.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleElectrocute275BlendActorColor blend:
                    NativeBattleGraphicsModel tinted = graphics.Models[blend.Actor];
                    NativeBattleWeightedColorStep color = NativeBattleWeightedColor56CC00.Blend(current.Memory, blend.FirstColor, blend.SecondColor, blend.FirstFactor, blend.SecondFactor, tinted.Color40 ?? throw new NotSupportedException("Electrocute requires its current actor color DWORD."), colors);
                    graphics = graphics with { Models = graphics.Models.SetItem(blend.Actor, tinted with { Color40 = color.Color }) };
                    return current with { Memory = color.Memory };
                case NativeBattleElectrocute275LoadView:
                    NativeBattlePackedMatrix32 view = Camera().CapturePackedView1D97778();
                    short? padding = view.Known[18] && view.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(view.Bytes.AsSpan().Slice(18)) : null;
                    return current with { Memory = current.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Electrocute requires its current geometry registers."), view.ReadMatrix(), true, true, padding) } };
                case NativeBattleElectrocute275ComposeCaster compose:
                    (current, graphics) = PrepareNativeElectrocute275Bones(current, graphics, compose.Actor, true);
                    return current;
                case NativeBattleElectrocute275RestoreCaster restore:
                    (current, graphics) = PrepareNativeElectrocute275Bones(current, graphics, restore.Actor, false);
                    return current;
                case NativeBattleElectrocute275ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleElectrocute275ProjectWorld project:
                    uint working = current.State.ReadDword(NativeBattleElectrocute275Profile.WorkingGlobal);
                    NativeBattleRibbonBank bank = new(checked(working + NativeBattleElectrocute275Profile.WorldPointsOffset), checked((int)(working - NativeBattleElectrocute275Profile.ArenaAddress) + NativeBattleElectrocute275Profile.WorldPointsOffset), NativeBattleElectrocute275Profile.PointCount);
                    return current with { Memory = NativeBattleRibbonProjection.ProjectCurrent(current.Memory, project.HeadAddress, bank, Camera().Projection ?? throw new NotSupportedException("Electrocute requires its current projection."), current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Electrocute requires its current projection controls.")) };
                case NativeBattleElectrocute275DrawWorld world:
                    var worldDraw = NativeBattleElectrocute275Ribbons.DrawProjected(current, world.HeadAddress, world.Count, world.TextureVOffset, resources, NativeBattleRibbonProjection.CaptureWorldDirectionWord(current.Memory, Camera().Projection ?? throw new NotSupportedException("World ribbon requires its current projection.")));
                    return Deliver(worldDraw.Frame, worldDraw.Batch);
                case NativeBattleElectrocute275DrawBoneTrail boneTrail:
                    byte caster = current.Effect.Parameters!.Actor78;
                    if (graphics.Models[caster].ActiveStagedResourceRow100 != boneTrail.BindingAddress)
                    {
                        throw new InvalidDataException("Electrocute bone trail leaves its current addressed caster binding.");
                    }
                    uint boneBank = checked(current.State.ReadDword(NativeBattleElectrocute275Profile.WorkingGlobal) + NativeBattleElectrocute275Profile.BonePointsOffset);
                    current = ProjectNativeElectrocute275BoneTrail(current, graphics, caster, boneTrail.HeadAddress, boneBank, NativeBattleElectrocute275Profile.PointCount * NativeBattleElectrocute275Profile.PointStride);
                    var boneDraw = NativeBattleElectrocute275Ribbons.DrawBoneProjected(current, boneTrail.HeadAddress, boneTrail.Count, resources);
                    return Deliver(boneDraw.Frame, boneDraw.Batch);
                case NativeBattleElectrocute275DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.Phase, sprite.DepthShift);
                case NativeBattleElectrocute275ContactAll:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(current.Effect.Parameters!.HeaderIndex7C) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Electrocute operation has no proved implementation.");
            }
        }

        NativeBattleElectrocute275TaskStep AdvanceAct(NativeBattleElectrocute275Frame current, uint taskAddress)
        {
            NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(current, taskAddress);
            byte caster = current.Effect.Parameters!.Actor78;
            NativeBattleElectrocute275AnchorStep anchored = Anchor(current, caster);
            current = anchored.Frame;
            NativeBattleModuleAnchor anchor = anchored.Anchor;
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, BitConverter.GetBytes(anchor.X)).Write(2, BitConverter.GetBytes(unchecked((short)(anchor.Y + 200)))).Write(4, BitConverter.GetBytes(anchor.Z)).Write(6, BitConverter.GetBytes(anchor.W)).Write(8, BitConverter.GetBytes(current.State.ReadDword(NativeBattleElectrocute275Profile.WorkingGlobal) + NativeBattleElectrocute275Profile.MorphOffset));
            NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(0xC, 0xC8).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Act, 0, (next, node) =>
            {
                current = NativeBattleElectrocute275Drawing.EmitNode(current, resources.Act, context, node, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Electrocute requires its projection."), colors, Deliver);
                return next;
            });
            current = NativeBattleElectrocute275Controller.Write(current, taskAddress, NativeBattleElectrocute275Controller.Read(current, taskAddress).WriteBytes(0xC, advanced.Workspace.Data.AsSpan()));
            return new(current, advanced.Remaining == 0 ? 2 : 0);
        }

        NativeBattleElectrocute275Frame DrawSprite(NativeBattleElectrocute275Frame current, uint address, uint sourceAddress, ushort phase, byte depthShift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Electrocute sprite drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Electrocute sprite arguments differ from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Electrocute sprite dispatcher selected a different source."), Camera().Projection ?? throw new NotSupportedException("Electrocute sprite drawing requires its current projection."), Camera().Roll, 17, depthShift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
