using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBadBreath300(NativeBattleGraphicsState graphics) => RunNativeBadBreath300(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeBadBreath300(NativeBattleGraphicsState graphics) => RunNativeBadBreath300(graphics, false);

    private NativeBattleGraphicsState RunNativeBadBreath300(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Bad Breath requires its selected original effect.");
        NativeBattleBadBreath300Resources resources = effect.Binding.BadBreath300Resources ?? throw new NotSupportedException("Bad Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Bad Breath requires its actual process memory.");
        NativeBattleBadBreath300Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Bad Breath requires its CRT stream."), memory.BadBreath300 ?? throw new NotSupportedException("Bad Breath requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Bad Breath requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Bad Breath requires its original interpolation constants.");
        NativeBattleBadBreath300Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Bad Breath requires its current actor skeleton."), _ => Camera().CapturePackedView1D97778(), Anchor, Mouth, Bone, Execute, (current, source, context, node) => NativeBattleBadBreath300Drawing.EmitNode(current, source, context, node, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Bad Breath requires its published projection."), colors, Deliver), NativeHostCallbacks ?? graphics.Callback);
        inputs = inputs with { EmitNodePresentation = (current, source, context, node, identity) => NativeBattleBadBreath300Drawing.EmitNode(current, source, context, node, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Bad Breath requires its published projection."), colors, Deliver, pilotIdentity: identity) };
        frame = initialize ? NativeBattleBadBreath300Controller.Initialize(frame, resources, inputs) : NativeBattleBadBreath300Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Bad Breath requires its current camera.");

        void Publish(NativeBattleBadBreath300Frame current)
        {
            NativeEffectMemory = current.Memory with { BadBreath300 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Bad Breath lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBadBreath300Frame Import(NativeBattleBadBreath300Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Bad Breath external service lost its process memory.");
            return current with { Memory = latest, State = latest.BadBreath300 ?? throw new InvalidDataException("Bad Breath external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Bad Breath external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Bad Breath external service lost its CRT stream.") };
        }

        NativeBattleBadBreath300Frame Deliver(NativeBattleBadBreath300Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Bad Breath requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleBadBreath300Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBadBreath300Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleBadBreath300AnchorStep Anchor(NativeBattleBadBreath300Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Bad Breath requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Bad Breath requires the actual actor heading.")).Angle36 });
        }

        NativeBattleBadBreath300PointStep Mouth(NativeBattleBadBreath300Frame current)
        {
            byte actor = current.Effect.Parameters!.Actor78;
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Bad Breath mouth requires its current actor skeleton.");
            NativeBattleBadBreath300MouthStep prepared = NativeBattleBadBreath300Mouth.Prepare(current.Memory, graphics.Models[actor], pose, resources.MouthAnchor);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            return new(current with { Memory = prepared.Memory }, prepared.Position);
        }

        NativeBattleBadBreath300PointStep Bone(NativeBattleBadBreath300Frame current, int actor, byte selector, short distance)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Bad Breath requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, selector, distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Bad Breath bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleBadBreath300Frame DrawStrip(NativeBattleBadBreath300Frame current, uint address, uint sourceAddress, uint selectedVerticesAddress, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            ImmutableArray<byte> source = current.State.ReadMesh(sourceAddress);
            if (selectedVerticesAddress != NativeBattleBadBreath300Profile.ArenaAddress + NativeBattleBadBreath300Profile.ScratchOffset)
            {
                throw new InvalidDataException("Bad Breath strip vertices leave their shared working scratch owner.");
            }
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Bad Breath strip drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 92), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), 0, 92);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Bad Breath strip drawing requires its current working arena.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Bad Breath strip drawing requires its retained projection controls.");
            NativeBattleGeometryRegisterStep registers = new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Bad Breath strip drawing requires its current geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Bad Breath strip drawing requires its current data registers."));
            NativeBattleBadBreath300StripMeshStep drawn = NativeBattleBadBreath300StripMesh.Draw66F710(source, sourceAddress, temporary, selectedVerticesAddress, ReadVertex, registers, Camera().Projection ?? throw new NotSupportedException("Bad Breath strip drawing requires its current projection."), controls.Dqa, controls.Dqb, controls.Zsf4, colors, pilotIdentity: pilotIdentity);
            current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context92, 92) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                if (offset < 0 || offset > NativeBattleBadBreath300Profile.ScratchLength - 8)
                {
                    throw new InvalidDataException("Bad Breath strip face leaves its current selected vertex bank.");
                }
                int at = NativeBattleBadBreath300Profile.ScratchOffset + offset;
                byte[] xyz = arena.Read(at, 6);
                (byte[] bytes, bool[] known) = arena.ReadSnapshot(at + 6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
            }
        }

        NativeBattleBadBreath300Frame Execute(NativeBattleBadBreath300Frame current, NativeBattleBadBreath300Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBadBreath300BindCaster bind:
                    if (bind.ResourceAddress != 0x103DCB0 || bind.SaveAddress != NativeBattleBadBreath300Profile.ArenaAddress + NativeBattleBadBreath300Profile.BindingOffset)
                    {
                        throw new InvalidDataException("Bad Breath caster binding leaves its original directory and arena row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Bad Breath requires its CPU resource rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Bad Breath requires its shared working arena.")).ReadResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress))) : staging.ReadResourcePointer(address);
                    }

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        if (!IsNativeEffectArenaAddress(address))
                        {
                            throw new NotSupportedException("Bad Breath binding must publish into its original shared arena row.");
                        }
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Bad Breath requires its shared working arena.");
                        current = current with { Memory = current.Memory with { Arena = arena.WriteResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), pointer) } };
                    }
                case NativeBattleBadBreath300RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleBadBreath300Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleBadBreath300Clip clip:
                    return Deliver(current, NativeBattleModuleRenderCommands.Emit700150(clip.Phase, 64, 64, 0, 14, 22, 14, clip.PilotIdentity));
                case NativeBattleBadBreath300DrawStrip strip:
                    return DrawStrip(current, strip.TemporaryAddress, strip.SourceAddress, strip.SelectedVerticesAddress, strip.PilotIdentity);
                case NativeBattleBadBreath300DrawParticle particle:
                    NativeBattleModulePoolTask task = NativeBattleBadBreath300Controller.Read(current, particle.TaskAddress);
                    if (task.Callback != NativeBattleBadBreath300Profile.ParticleCallback)
                    {
                        throw new InvalidDataException("Bad Breath particle drawing requires its actual callback.");
                    }
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Bad Breath particle drawing requires its physical temporary memory."), 180);
                    short scale = unchecked((short)task.ReadWord(0x16));
                    NativeBattleModuleAnchor point = new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14)), scale);
                    current = current with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, Camera().CapturePackedView1D97778(), point, scale, checked((short)-(scale >> 2))) };
                    NativeBattleFileStaging particleStaging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, resources.ParticleSprite.Address, 0).Write(allocation.Address + 4, BitConverter.GetBytes(task.ReadWord(0xC)));
                    current = DrawSprite(current with { Staging = particleStaging }, allocation.Address, task.ReadWord(0xC), particle.PilotIdentity);
                    return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, 180) };
                case NativeBattleBadBreath300Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Bad Breath operation has no proved implementation.");
            }
        }

        NativeBattleBadBreath300Frame DrawSprite(NativeBattleBadBreath300Frame current, uint address, ushort phase, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resources.ParticleSprite;
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Bad Breath sprite drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Bad Breath sprite arguments differ from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Bad Breath sprite dispatcher selected a different source."), Camera().Projection ?? throw new NotSupportedException("Bad Breath sprite drawing requires its current projection."), Camera().Roll, 17, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
