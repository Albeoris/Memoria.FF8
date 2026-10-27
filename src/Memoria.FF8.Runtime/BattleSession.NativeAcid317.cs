using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeAcid317(NativeBattleGraphicsState graphics) => RunNativeAcid317(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeAcid317(NativeBattleGraphicsState graphics) => RunNativeAcid317(graphics, false);

    private NativeBattleGraphicsState RunNativeAcid317(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Acid requires its selected original effect.");
        NativeBattleAcid317Resources resources = effect.Binding.Acid317Resources ?? throw new NotSupportedException("Acid requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Acid requires its actual process memory.");
        NativeBattleAcid317Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Acid requires its CRT stream."), memory.Acid317 ?? throw new NotSupportedException("Acid requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Acid requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Acid requires its original interpolation constants.");
        NativeBattleAcid317Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], actor => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Acid requires its current skeleton.")).BoneCount, _ => Camera().CapturePackedView1D97778(), Anchor, Bone, Mouth, Execute) { Colors = colors };
        frame = initialize ? NativeBattleAcid317Controller.Initialize(frame, resources, inputs) : NativeBattleAcid317Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Acid requires its current camera.");

        void Publish(NativeBattleAcid317Frame current)
        {
            NativeEffectMemory = current.Memory with { Acid317 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Acid lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleAcid317Frame Import(NativeBattleAcid317Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Acid external service lost its process memory.");
            return current with { Memory = latest, State = latest.Acid317 ?? throw new InvalidDataException("Acid external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Acid external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Acid external service lost its CRT stream.") };
        }

        NativeBattleAcid317Frame Deliver(NativeBattleAcid317Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Acid requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleAcid317Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleAcid317Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleAcid317AnchorStep Anchor(NativeBattleAcid317Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Acid requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Acid requires the actual actor heading.")).Angle36 });
        }

        NativeBattleAcid317PointStep Mouth(NativeBattleAcid317Frame current)
        {
            byte actor = current.Effect.Parameters!.Actor78;
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Acid mouth requires its current actor skeleton.");
            NativeBattleBadBreath300MouthStep prepared = NativeBattleBadBreath300Mouth.Prepare(current.Memory, graphics.Models[actor], pose, resources.MouthAnchor8);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            return new(current with { Memory = prepared.Memory }, prepared.Position);
        }

        NativeBattleAcid317PointStep Bone(NativeBattleAcid317Frame current, int actor, short selector, short distance)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Acid requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, checked((byte)selector), distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Acid bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleAcid317Frame Execute(NativeBattleAcid317Frame current, NativeBattleAcid317Operation operation)
        {
            switch (operation)
            {
                case NativeBattleAcid317BindCaster bind:
                    if (bind.ResourceAddress != 0xFFE6B0 || bind.SaveAddress != NativeBattleAcid317Profile.ArenaAddress + NativeBattleAcid317Profile.BindingOffset)
                    {
                        throw new InvalidDataException("Acid caster binding leaves its original directory and arena row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Acid requires its CPU resource rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Acid requires its shared working arena.")).ReadResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress))) : staging.ReadResourcePointer(address);
                    }

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        if (!IsNativeEffectArenaAddress(address))
                        {
                            throw new NotSupportedException("Acid binding must publish into its original shared arena row.");
                        }
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Acid requires its shared working arena.");
                        current = current with { Memory = current.Memory with { Arena = arena.WriteResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), pointer) } };
                    }
                case NativeBattleAcid317RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleAcid317Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleAcid317ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleAcid317SnapshotBody snapshot:
                    (current, graphics) = SnapshotNativeAcid317Body(current, graphics, snapshot.Actor, snapshot.CacheAddress);
                    return current;
                case NativeBattleAcid317DrawBody132 body:
                    (current, graphics) = DrawNativeAcid317Body(current, graphics, body.Actor, body.Fraction);
                    return current;
                case NativeBattleAcid317AdvanceAct act:
                    return AdvanceAct(current, act.TaskAddress);
                case NativeBattleAcid317ProjectPoints project:
                    return ProjectPoints(current, project.HeadAddress, project.Count);
                case NativeBattleAcid317DrawTrail trail:
                    var strip = NativeBattleAcid317Trail.Draw(current, trail.HeadAddress, trail.Count, resources);
                    return Deliver(strip.Frame, strip.Batch);
                case NativeBattleAcid317DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.Phase, sprite.DepthShift);
                case NativeBattleAcid317Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Acid operation has no proved implementation.");
            }
        }

        NativeBattleAcid317Frame AdvanceAct(NativeBattleAcid317Frame current, uint taskAddress)
        {
            NativeBattleModulePoolTask task = NativeBattleAcid317Controller.Read(current, taskAddress);
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, task.ReadBytes(0x16, 8)).Write(8, BitConverter.GetBytes(current.State.ReadDword(NativeBattleAcid317Profile.WorkingGlobal) + NativeBattleAcid317Profile.MorphOffset));
            NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(0x20, 0x248).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.TargetAct, 0, (next, node) =>
            {
                NativeBattleEffectArena normalized = current.State.ReadView(NativeBattleAcid317Profile.PackedViewGlobal, 32);
                (byte[] bytes, bool[] known) = normalized.ReadSnapshot(0, 32);
                current = NativeBattleAcid317Drawing.EmitNode(current, resources.TargetAct, context, node, Camera().CapturePackedView1D97778(), new(bytes.ToImmutableArray(), known.ToImmutableArray()), Camera().Projection ?? throw new NotSupportedException("Acid requires its projection."), colors, Deliver);
                return next;
            });
            return NativeBattleAcid317Controller.Write(current, taskAddress, advanced.Workspace.CopyTo(NativeBattleAcid317Controller.Read(current, taskAddress), 0x20, retainUnknown: false));
        }

        NativeBattleAcid317Frame ProjectPoints(NativeBattleAcid317Frame current, uint head, short count) => NativeBattleAcid317PointProjection.Project(current, head, count, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Acid requires its projection."));

        NativeBattleAcid317Frame DrawSprite(NativeBattleAcid317Frame current, uint address, uint sourceAddress, ushort phase, byte depthShift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Acid sprite drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Acid sprite arguments differ from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Acid sprite dispatcher selected a different source."), Camera().Projection ?? throw new NotSupportedException("Acid sprite drawing requires its current projection."), Camera().Roll, 17, depthShift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
