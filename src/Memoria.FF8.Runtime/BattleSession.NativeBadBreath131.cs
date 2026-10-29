using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBadBreath131(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Bad Breath requires its selected original effect.");
        NativeBattleBadBreath131Resources resources = effect.Binding.BadBreath131Resources ?? throw new NotSupportedException("Bad Breath requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Bad Breath requires its actual process memory.");
        NativeBattleBadBreath131Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Bad Breath requires its CRT stream."), memory.BadBreath131 ?? throw new NotSupportedException("Bad Breath requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Bad Breath requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Bad Breath requires its original interpolation constants.");
        NativeBattleBadBreath131Inputs inputs = new(nativeActionPackets, colors, actor => graphics.Models[actor], actor => PoseSource(actor).BoneCount, _ => Camera().CapturePackedView1D97778(), Anchor, Bone, AdvanceAct, Execute);
        frame = initialize ? NativeBattleBadBreath131Controller.Initialize(frame, resources, inputs) : NativeBattleBadBreath131Controller.Advance(frame, resources, inputs);
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
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Bad Breath requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Bad Breath requires its current actor skeleton.");

        void Publish(NativeBattleBadBreath131Frame current)
        {
            NativeEffectMemory = current.Memory with { BadBreath131 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Bad Breath lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBadBreath131Frame ObserveBone(NativeBattleBadBreath131Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleBadBreath131AnchorStep Anchor(NativeBattleBadBreath131Frame current, int actor)
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

        NativeBattleBadBreath131PointStep Bone(NativeBattleBadBreath131Frame current, int actor, short selector, int distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleBadBreath131ActStep AdvanceAct(NativeBattleBadBreath131Frame current, uint address, NativeBattleGfAnimationSource source, int offset, int size, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(NativeBattleBadBreath131Controller.Read(current, address), offset, size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleBadBreath131ActContext12.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("BadBreath131 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleBadBreath131Profile.WorkingGlobal) + NativeBattleBadBreath131Profile.MorphOffset);
                NativeBattleBadBreath131ActContextStep drawn = NativeBattleBadBreath131ActContext12.Draw(current.Memory, source, node, context, Camera().CapturePackedView1D97778(), temporary, Projection(), colors, morph, NativeBattleBadBreath131Profile.MorphBytes, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(131, NativeBattleBadBreath131Controller.Read(current, address), node.ResourceOffset) with { ResourceGeneration = ((long)source.Address << 32) | (uint)offset });
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("BadBreath131 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleBadBreath131Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBadBreath131Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleBadBreath131Controller.Write(current, address, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleBadBreath131Controller.Read(current, address), offset, advanced.Workspace));
            return new(current, advanced.Remaining);
        }

        NativeBattleBadBreath131Frame Deliver(NativeBattleBadBreath131Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Bad Breath requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleBadBreath131Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBadBreath131Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffectArena ReadTemporary(NativeBattleBadBreath131Frame current, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Bad Breath requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        NativeBattleBadBreath131Frame Execute(NativeBattleBadBreath131Frame current, NativeBattleBadBreath131Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBadBreath131ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleBadBreath131ActorColor color:
                    graphics = graphics with { Models = graphics.Models.SetItem(color.Actor, graphics.Models[color.Actor] with { Color40 = color.Color }) };
                    return current;
                case NativeBattleBadBreath131FlashEnvelope flash:
                    return Deliver(current, NativeBattleModuleRenderCommands.Emit700150(flash.Age, 64, 64, 0, 14, 22, 14, flash.PilotIdentity));
                case NativeBattleBadBreath131DrawStrip92 strip:
                    var drawnStrip = NativeBattleBadBreath131StripDrawing.Draw(current, strip.TemporaryAddress, strip.SourceAddress, strip.SelectedVerticesAddress, Projection(), colors, strip.PilotIdentity);
                    return Deliver(drawnStrip.Frame, drawnStrip.Batch);
                case NativeBattleBadBreath131Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Bad Breath sound lost its process memory.") };
                case NativeBattleBadBreath131Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(checked((nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Bad Breath requires its current first contact.")) + contact.Index))) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Bad Breath contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleBadBreath131DrawSprite180 sprite:
                    if (sprite.SourceAddress != resources.ParticleSprite.Address)
                    {
                        throw new NotSupportedException("Bad Breath selected an unsupported sprite source.");
                    }
                    NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, ReadTemporary(current, sprite.TemporaryAddress, 180), address => address == resources.ParticleSprite.Address ? resources.ParticleSprite : throw new NotSupportedException("Bad Breath selected an unsupported sprite source."), Projection(), Camera().Roll, 17, pilotIdentity: sprite.PilotIdentity);
                    return Deliver(current with { Memory = spriteDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, sprite.TemporaryAddress, spriteDraw.Temporary180, 180) }, spriteDraw.Batch);
                default:
                    throw new NotSupportedException("Bad Breath operation has no proved implementation.");
            }
        }
    }
}
