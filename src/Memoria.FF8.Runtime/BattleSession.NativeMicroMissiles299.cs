using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeMicroMissiles299(NativeBattleGraphicsState graphics) => RunNativeMicroMissiles299(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeMicroMissiles299(NativeBattleGraphicsState graphics) => RunNativeMicroMissiles299(graphics, false);

    private NativeBattleGraphicsState RunNativeMicroMissiles299(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Micro Missiles requires its selected original effect.");
        NativeBattleMicroMissiles299Resources resources = effect.Binding.MicroMissiles299Resources ?? throw new NotSupportedException("Micro Missiles requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Micro Missiles requires its actual process memory.");
        NativeBattleMicroMissiles299Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Micro Missiles requires its CRT stream."), memory.MicroMissiles299 ?? throw new NotSupportedException("Micro Missiles requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Micro Missiles requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Micro Missiles requires its original interpolation constants.");
        NativeBattleMicroMissiles299Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Micro Missiles requires its current actor skeleton."), _ => Camera().CapturePackedView1D97778(), Anchor, Execute, (current, source, context, node) => NativeBattleMicroMissiles299Drawing.EmitNode(current, source, context, node, Camera().Projection ?? throw new NotSupportedException("Micro Missiles requires its published projection."), colors, Deliver), colors);
        inputs = inputs with { EmitNodePresentation = (current, source, context, node, identity) => NativeBattleMicroMissiles299Drawing.EmitNode(current, source, context, node, Camera().Projection ?? throw new NotSupportedException("Micro Missiles requires its published projection."), colors, Deliver, pilotIdentity: identity) };
        frame = initialize ? NativeBattleMicroMissiles299Controller.Initialize(frame, resources, inputs) : NativeBattleMicroMissiles299Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Micro Missiles requires its current camera.");

        void Publish(NativeBattleMicroMissiles299Frame current)
        {
            NativeEffectMemory = current.Memory with { MicroMissiles299 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Micro Missiles lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleMicroMissiles299Frame Import(NativeBattleMicroMissiles299Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Micro Missiles external service lost its process memory.");
            return current with { Memory = latest, State = latest.MicroMissiles299 ?? throw new InvalidDataException("Micro Missiles external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Micro Missiles external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Micro Missiles external service lost its CRT stream.") };
        }

        NativeBattleMicroMissiles299Frame Deliver(NativeBattleMicroMissiles299Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Micro Missiles requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleMicroMissiles299Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleMicroMissiles299Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleMicroMissiles299AnchorStep Anchor(NativeBattleMicroMissiles299Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Micro Missiles requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Micro Missiles requires the actual actor heading.")).Angle36 });
        }

        NativeBattleMicroMissiles299Frame Execute(NativeBattleMicroMissiles299Frame current, NativeBattleMicroMissiles299Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMicroMissiles299BindCaster bind:
                    if (bind.ResourceAddress != 0x1047120 || bind.SaveAddress != NativeBattleMicroMissiles299Profile.ArenaAddress + NativeBattleMicroMissiles299Profile.BindingOffset)
                    {
                        throw new InvalidDataException("Micro Missiles caster binding leaves its original directory and arena row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Micro Missiles requires its CPU resource rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Micro Missiles requires its shared working arena.")).ReadResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress))) : staging.ReadResourcePointer(address);
                    }

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        if (!IsNativeEffectArenaAddress(address))
                        {
                            throw new NotSupportedException("Micro Missiles binding must publish into its original shared arena row.");
                        }
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Micro Missiles requires its shared working arena.");
                        current = current with { Memory = current.Memory with { Arena = arena.WriteResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), pointer) } };
                    }
                case NativeBattleMicroMissiles299RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleMicroMissiles299Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleMicroMissiles299DrawSprite sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.ResourceAddress, sprite.Phase, sprite.PilotIdentity);
                case NativeBattleMicroMissiles299DrawParticle particle:
                    NativeBattleModulePoolTask task = NativeBattleMicroMissiles299Controller.Read(current, particle.TaskAddress);
                    if (task.Callback != NativeBattleMicroMissiles299Profile.ParticleCallback)
                    {
                        throw new InvalidDataException("Micro Missiles particle drawing requires its actual callback.");
                    }
                    NativeBattleModuleAnchor point = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)));
                    current = current with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, Camera().CapturePackedView1D97778(), point, 1024, -256) };
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Micro Missiles particle drawing requires its physical temporary memory."), 180);
                    NativeBattleFileStaging particleStaging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, resources.ParticleSprite.Address, 0).Write(allocation.Address + 4, BitConverter.GetBytes(task.ReadWord(0x12)));
                    current = DrawSprite(current with { Staging = particleStaging }, allocation.Address, resources.ParticleSprite.Address, task.ReadWord(0x12), particle.PilotIdentity);
                    return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, 180) };
                case NativeBattleMicroMissiles299Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Micro Missiles operation has no proved implementation.");
            }
        }

        NativeBattleMicroMissiles299Frame DrawSprite(NativeBattleMicroMissiles299Frame current, uint address, uint resourceAddress, ushort phase, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resourceAddress == resources.ParticleSprite.Address ? resources.ParticleSprite : resourceAddress == resources.TrailSprite.Address ? resources.TrailSprite : resourceAddress == resources.ImpactSprite.Address ? resources.ImpactSprite : throw new NotSupportedException("Micro Missiles sprite resource leaves its original banks.");
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Micro Missiles sprite drawing requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resourceAddress || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != phase)
            {
                throw new InvalidDataException("Micro Missiles sprite arguments differ from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Micro Missiles sprite dispatcher selected a different source."), Camera().Projection ?? throw new NotSupportedException("Micro Missiles sprite drawing requires its current projection."), Camera().Roll, 17, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }
}
