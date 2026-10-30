using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeWhiteWind297(NativeBattleGraphicsState graphics) => RunNativeWhiteWind297(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeWhiteWind297(NativeBattleGraphicsState graphics) => RunNativeWhiteWind297(graphics, false);

    private NativeBattleGraphicsState RunNativeWhiteWind297(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Blue Magic White Wind requires its selected original effect.");
        NativeBattleWhiteWind297Resources resources = effect.Binding.WhiteWind297Resources ?? throw new NotSupportedException("Blue Magic White Wind requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Blue Magic White Wind requires its actual process memory.");
        NativeBattleWhiteWind297Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Blue Magic White Wind requires its CRT stream."), memory.WhiteWind297 ?? throw new NotSupportedException("Blue Magic White Wind requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Blue Magic White Wind requires its addressed file and temporary memory.") };
        NativeBattleWhiteWind297Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], _ => Camera().CapturePackedView1D97778(), Anchor, Execute, (current, source, context, node) => NativeBattleWhiteWind297Drawing.EmitNode(current, source, context, node, Camera().Projection ?? throw new NotSupportedException("Blue Magic White Wind requires its published projection."), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Blue Magic White Wind requires its original interpolation constants."), Deliver));
        inputs = inputs with { EmitNodePresentation = (current, source, context, node, identity) => NativeBattleWhiteWind297Drawing.EmitNode(current, source, context, node, Camera().Projection ?? throw new NotSupportedException("Blue Magic White Wind requires its published projection."), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Blue Magic White Wind requires its original interpolation constants."), Deliver, pilotIdentity: identity) };
        frame = initialize ? NativeBattleWhiteWind297Controller.Initialize(frame, resources, inputs) : NativeBattleWhiteWind297Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Blue Magic White Wind requires its current camera.");

        void Publish(NativeBattleWhiteWind297Frame current)
        {
            NativeEffectMemory = current.Memory with { WhiteWind297 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Blue Magic White Wind lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleWhiteWind297Frame Import(NativeBattleWhiteWind297Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Blue Magic White Wind external service lost its process memory.");
            return current with { Memory = latest, State = latest.WhiteWind297 ?? throw new InvalidDataException("Blue Magic White Wind external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Blue Magic White Wind external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Blue Magic White Wind external service lost its CRT stream.") };
        }

        NativeBattleWhiteWind297Frame Deliver(NativeBattleWhiteWind297Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Blue Magic White Wind requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleWhiteWind297Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleWhiteWind297Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleWhiteWind297AnchorStep Anchor(NativeBattleWhiteWind297Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Blue Magic White Wind requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Blue Magic White Wind requires the actual actor heading.")).Angle36 });
        }

        NativeBattleWhiteWind297Frame Execute(NativeBattleWhiteWind297Frame current, NativeBattleWhiteWind297Operation operation)
        {
            switch (operation)
            {
                case NativeBattleWhiteWind297BindCaster bind:
                    if (bind.ResourceAddress != 0x1056888 || bind.SaveAddress != NativeBattleWhiteWind297Profile.ArenaAddress + NativeBattleWhiteWind297Profile.BindingOffset)
                    {
                        throw new InvalidDataException("Blue Magic White Wind caster binding leaves its original directory and arena row.");
                    }
                    Publish(current);
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Blue Magic White Wind requires its CPU resource rows."), actor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        return IsNativeEffectArenaAddress(address) ? (current.Memory.Arena ?? throw new NotSupportedException("Blue Magic White Wind requires its shared working arena.")).ReadResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress))) : staging.ReadResourcePointer(address);
                    }

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        if (!IsNativeEffectArenaAddress(address))
                        {
                            throw new NotSupportedException("Blue Magic White Wind binding must publish into its original shared arena row.");
                        }
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Blue Magic White Wind requires its shared working arena.");
                        current = current with { Memory = current.Memory with { Arena = arena.WriteResourcePointer(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), pointer) } };
                    }
                case NativeBattleWhiteWind297RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleWhiteWind297Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleWhiteWind297Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Blue Magic White Wind operation has no proved implementation.");
            }
        }
    }
}
