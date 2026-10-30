using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeOilBlast137(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = (graphics.OriginalEffect ?? throw new NotSupportedException("Oil Blast requires its selected original effect.")) with { PresentationPackets = [] };
        NativeBattleOilBlast137Resources resources = effect.Binding.OilBlast137Resources ?? throw new NotSupportedException("Oil Blast requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Oil Blast requires its actual process memory.");
        NativeBattleOilBlast137Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Oil Blast requires its CRT stream."), memory.OilBlast137 ?? throw new NotSupportedException("Oil Blast requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Oil Blast requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Oil Blast requires its original interpolation constants.");
        NativeBattleOilBlast137Inputs inputs = new(nativeActionPackets, colors, Bone, _ => Camera().CapturePackedView1D97778(), ModelYaw, AdvanceAct, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleOilBlast137Controller.Initialize(frame, resources, inputs) : NativeBattleOilBlast137Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Oil Blast requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Oil Blast requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Oil Blast requires its current actor skeleton.");

        void Publish(NativeBattleOilBlast137Frame current)
        {
            NativeEffectMemory = current.Memory with { OilBlast137 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Oil Blast lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleOilBlast137Frame ObserveBone(NativeBattleOilBlast137Frame current, NativeBattleBoneAnchorProducer producer)
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
                throw new NotSupportedException("Oil Blast requires its actual current model pointer.");
            }
            return checked((int)(offset / 156));
        }

        short ModelYaw(uint pointer) => graphics.Models[Actor(pointer)].Rotation14 ?? throw new NotSupportedException("Oil Blast requires its current model rotation WORD14.");

        NativeBattleOilBlast137PointStep Bone(NativeBattleOilBlast137Frame current, uint pointer, byte selector, short distance)
        {
            int actor = Actor(pointer);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleOilBlast137ActStep AdvanceAct(NativeBattleOilBlast137Frame current, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(NativeBattleOilBlast137Controller.Read(current, address), offset, size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleOilBlast137ActContext44.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("OilBlast137 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleOilBlast137Profile.WorkingGlobal) + NativeBattleOilBlast137Profile.MorphOffset);
                NativeBattleOilBlast137ActContextStep drawn = NativeBattleOilBlast137ActContext44.DrawPresentation(current.Memory, source, node, context, temporary, Projection(), colors, morph, NativeBattleOilBlast137Profile.MorphBytes, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(137, NativeBattleOilBlast137Controller.Read(current, address), unchecked(offset * 65537 + node.ResourceOffset)) with { ResourceGeneration = source.Address });
                drawn = drawn with { Batch = NativeBattleContextBodyPresentation.CaptureDepths(drawn.Batch) };
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("OilBlast137 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleOilBlast137Profile.CursorGlobal);
                current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), drawn.Batch) } };
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleOilBlast137Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleOilBlast137Controller.Write(current, address, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleOilBlast137Controller.Read(current, address), offset, advanced.Workspace));
            return new(current, advanced.Remaining);
        }

        NativeBattleOilBlast137Frame Deliver(NativeBattleOilBlast137Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleContextBodyPresentation.CaptureDepths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Oil Blast requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleOilBlast137Profile.CursorGlobal);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), batch) } };
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleOilBlast137Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffectArena ReadTemporary(NativeBattleOilBlast137Frame current, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Oil Blast requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        NativeBattleOilBlast137Frame Execute(NativeBattleOilBlast137Frame current, NativeBattleOilBlast137Operation operation)
        {
            switch (operation)
            {
                case NativeBattleOilBlast137InitializeAct initializeAct:
                    NativeBattleModulePoolTask task = NativeBattleOilBlast137Controller.Read(current, initializeAct.TaskAddress);
                    NativeBattleGfAnimationWorkspace initialized = NativeBattleGfAnimationWorkspace.Borrow(task, initializeAct.WorkspaceOffset, initializeAct.WorkspaceBytes).Initialize7016B0(initializeAct.Source, initializeAct.WorkspaceBytes);
                    return NativeBattleOilBlast137Controller.Write(current, initializeAct.TaskAddress, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, initializeAct.WorkspaceOffset, initialized));
                case NativeBattleOilBlast137DrawCustom84 custom:
                    NativeBattleEffectArena customContext = ReadTemporary(current, custom.Temporary84, 84);
                    NativeBattleOilBlast137MeshStep customDraw = NativeBattleOilBlast137Mesh.DrawPresentation(current.Memory, resources.CustomMesh, customContext, Projection(), pilotIdentity: custom.PilotIdentity);
                    return Deliver(current with { Memory = customDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, custom.Temporary84, customDraw.Context84, 84) }, customDraw.Batch);
                case NativeBattleOilBlast137DrawStatic88 mesh:
                    if (mesh.SourceAddress != resources.StaticMesh.Address)
                    {
                        throw new NotSupportedException("Oil Blast selected an unsupported static mesh.");
                    }
                    NativeBattleStaticMeshContext88Step meshDraw = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, resources.StaticMesh, Projection(), ReadTemporary(current, mesh.Temporary88, 88), colors, pilotIdentity: mesh.PilotIdentity);
                    return Deliver(current with { Memory = meshDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, mesh.Temporary88, meshDraw.Context, 88) }, meshDraw.Batch);
                case NativeBattleOilBlast137Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Oil Blast sound lost its process memory.") };
                case NativeBattleOilBlast137Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(contact.Index)) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Oil Blast contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleOilBlast137DrawSprite180 sprite:
                    if (sprite.SourceAddress != resources.Sprite.Address)
                    {
                        throw new NotSupportedException("Oil Blast selected an unsupported sprite source.");
                    }
                    NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, ReadTemporary(current, sprite.Temporary180, 180), address => address == resources.Sprite.Address ? resources.Sprite : throw new NotSupportedException("Oil Blast selected an unsupported sprite source."), Projection(), Camera().Roll, pilotIdentity: sprite.PilotIdentity);
                    return Deliver(current with { Memory = spriteDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, sprite.Temporary180, spriteDraw.Temporary180, 180) }, spriteDraw.Batch);
                default:
                    throw new NotSupportedException("Oil Blast operation has no proved implementation.");
            }
        }
    }
}
