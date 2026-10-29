using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDisolvingAcid132(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Disolving Acid requires its selected original effect.");
        NativeBattleDisolvingAcid132Resources resources = effect.Binding.DisolvingAcid132Resources ?? throw new NotSupportedException("Disolving Acid requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Disolving Acid requires its actual process memory.");
        NativeBattleDisolvingAcid132Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Disolving Acid requires its CRT stream."), memory.DisolvingAcid132 ?? throw new NotSupportedException("Disolving Acid requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Disolving Acid requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Disolving Acid requires its original interpolation constants.");
        NativeBattleDisolvingAcid132Inputs inputs = new(nativeActionPackets, colors, Emitter, ModelMatrix, pointer => checked((byte)PoseSource(Actor(pointer)).BoneCount), Bone, _ => Camera().CapturePackedView1D97778(), _ => Projection(), AdvanceAct, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleDisolvingAcid132Controller.Initialize(frame, resources, inputs) : NativeBattleDisolvingAcid132Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Disolving Acid requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Disolving Acid requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Disolving Acid requires its current actor skeleton.");

        void Publish(NativeBattleDisolvingAcid132Frame current)
        {
            NativeEffectMemory = current.Memory with { DisolvingAcid132 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Disolving Acid lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleDisolvingAcid132Frame ObserveBone(NativeBattleDisolvingAcid132Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        int Actor(uint pointer)
        {
            long offset = (long)pointer - 0x1D972C0;
            if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Disolving Acid requires its actual current model pointer.");
            }
            return checked((int)(offset / 156));
        }

        NativeBattlePackedMatrix32 ModelMatrix(NativeBattleDisolvingAcid132Frame current, uint pointer) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Disolving Acid requires its current caster world matrix."), null);

        NativeBattleDisolvingAcid132PointStep Bone(NativeBattleDisolvingAcid132Frame current, uint pointer, byte selector, short distance)
        {
            int actor = Actor(pointer);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleDisolvingAcid132PointStep Emitter(NativeBattleDisolvingAcid132Frame current, NativeBattleEffectArena row)
        {
            int caster = Actor(current.State.ReadDword(NativeBattleDisolvingAcid132Profile.CasterGlobal));
            NativeBattlePoseSource pose = PoseSource(caster);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[caster], pose);
            byte[] anchor = row.Read(0, 8);
            int bone = BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan(6));
            if (bone < 0 || bone >= bones.Length)
            {
                throw new InvalidDataException("Disolving Acid emitter leaves its actual current bone publication.");
            }
            NativeBattleActionMatrix world = graphics.Models[caster].GeometryWorldMatrix ?? throw new NotSupportedException("Disolving Acid requires its current caster world matrix.");
            var observed = NativeBattleComposedAnchorTransform.Transform(current.Memory, NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]), anchor.ToImmutableArray());
            return new(current with { Memory = observed.Memory }, observed.Position);
        }

        NativeBattleDisolvingAcid132ActStep AdvanceAct(NativeBattleDisolvingAcid132Frame current, uint address, NativeBattleGfAnimationSource source, int offset, int size, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(NativeBattleDisolvingAcid132Controller.Read(current, address), offset, size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleDisolvingAcid132ActContext12.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("DisolvingAcid132 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleDisolvingAcid132Profile.WorkingGlobal) + NativeBattleDisolvingAcid132Profile.MorphOffset);
                NativeBattleDisolvingAcid132ActContextStep drawn = NativeBattleDisolvingAcid132ActContext12.Draw(current.Memory, source, node, context, Camera().CapturePackedView1D97778(), temporary, Projection(), colors, morph, NativeBattleDisolvingAcid132Profile.MorphBytes, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(132, NativeBattleDisolvingAcid132Controller.Read(current, address), node.ResourceOffset) with { ResourceGeneration = ((long)source.Address << 32) | (uint)offset });
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("DisolvingAcid132 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleDisolvingAcid132Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleDisolvingAcid132Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleDisolvingAcid132Controller.Write(current, address, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleDisolvingAcid132Controller.Read(current, address), offset, advanced.Workspace));
            return new(current, advanced.Remaining);
        }

        NativeBattleDisolvingAcid132Frame Deliver(NativeBattleDisolvingAcid132Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Disolving Acid requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleDisolvingAcid132Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleDisolvingAcid132Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffectArena ReadTemporary(NativeBattleDisolvingAcid132Frame current, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Disolving Acid requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        NativeBattleDisolvingAcid132Frame Execute(NativeBattleDisolvingAcid132Frame current, NativeBattleDisolvingAcid132Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDisolvingAcid132DeliverRibbonBatch ribbon:
                    return Deliver(current, ribbon.Batch);
                case NativeBattleDisolvingAcid132DrawTemplate180 template:
                    NativeBattleModuleSpriteDraw templateDraw = NativeBattleTemplateSprite683490.Draw(current.Memory, ReadTemporary(current, template.TemporaryAddress, 180), resources.Sprite(template.SourceAddress), Projection(), pilotIdentity: template.PilotIdentity);
                    return Deliver(current with { Memory = templateDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, template.TemporaryAddress, templateDraw.Temporary180, 180) }, templateDraw.Batch);
                case NativeBattleDisolvingAcid132Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Disolving Acid sound lost its process memory.") };
                case NativeBattleDisolvingAcid132Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(checked((nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Disolving Acid requires its current first contact.")) + contact.Index))) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Disolving Acid contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleDisolvingAcid132DrawSprite180 sprite:
                    NativeBattleModuleResource spriteSource = resources.Sprite(sprite.SourceAddress);
                    NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, ReadTemporary(current, sprite.TemporaryAddress, 180), address => address == spriteSource.Address ? spriteSource : throw new NotSupportedException("Disolving Acid selected an unsupported sprite source."), Projection(), Camera().Roll, 17, pilotIdentity: sprite.PilotIdentity);
                    return Deliver(current with { Memory = spriteDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, sprite.TemporaryAddress, spriteDraw.Temporary180, 180) }, spriteDraw.Batch);
                default:
                    throw new NotSupportedException("Disolving Acid operation has no proved implementation.");
            }
        }
    }
}
