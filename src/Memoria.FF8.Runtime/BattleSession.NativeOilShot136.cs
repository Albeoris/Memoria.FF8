using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeOilShot136(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Oil Shot requires its selected original effect.");
        NativeBattleOilShot136Resources resources = effect.Binding.OilShot136Resources ?? throw new NotSupportedException("Oil Shot requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Oil Shot requires its actual process memory.");
        NativeBattleOilShot136Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Oil Shot requires its CRT stream."), memory.OilShot136 ?? throw new NotSupportedException("Oil Shot requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Oil Shot requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Oil Shot requires its original interpolation constants.");
        NativeBattleOilShot136Inputs inputs = new(nativeActionPackets, colors, Anchor, Mouth, ModelYaw, _ => Camera().CapturePackedView1D97778(), AdvanceAct, Execute);
        frame = initialize ? NativeBattleOilShot136Controller.Initialize(frame, resources, inputs) : NativeBattleOilShot136Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Oil Shot requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Oil Shot requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Oil Shot requires its current actor skeleton.");

        void Publish(NativeBattleOilShot136Frame current)
        {
            NativeEffectMemory = current.Memory with { OilShot136 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Oil Shot lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        int Actor(uint pointer)
        {
            long offset = (long)pointer - 0x1D972C0;
            if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Oil Shot requires its actual current model pointer.");
            }
            return checked((int)(offset / 156));
        }

        short ModelYaw(uint pointer) => graphics.Models[Actor(pointer)].Rotation14 ?? throw new NotSupportedException("Oil Shot requires its actual current model rotation WORD14.");

        NativeBattleOilShot136AnchorStep Anchor(NativeBattleOilShot136Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor) with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("Oil Shot requires its actual model WORD36.")).Angle36 };
            return new(current, anchor);
        }

        NativeBattleOilShot136PointStep Mouth(NativeBattleOilShot136Frame current)
        {
            int caster = Actor(current.State.ReadDword(NativeBattleOilShot136Profile.CasterGlobal));
            var prepared = NativeBattleOilShot136Mouth.Prepare(current.Memory, graphics.Models[caster], PoseSource(caster), resources.BoneAnchor);
            graphics = graphics with { Models = graphics.Models.SetItem(caster, prepared.Model) };
            return new(current with { Memory = prepared.Memory }, prepared.Position);
        }

        NativeBattleOilShot136ActStep AdvanceAct(NativeBattleOilShot136Frame current, uint address, NativeBattleGfAnimationSource source, int offset, int size, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = new(NativeBattleOilShot136Controller.Read(current, address).ReadBytes(offset, size).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleOilShot136Act.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("OilShot136 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleOilShot136Profile.WorkingGlobal) + NativeBattleOilShot136Profile.MorphOffset);
                NativeBattleDefaultActContext40Step drawn = NativeBattleOilShot136Act.Draw(current.Memory, source, node, context, temporary, Projection(), colors, morph, current.State.ReadCurrentMatrix());
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("OilShot136 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleOilShot136Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleOilShot136Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleOilShot136Controller.Write(current, address, NativeBattleOilShot136Controller.Read(current, address).WriteBytes(offset, advanced.Workspace.Data.AsSpan()));
            return new(current, advanced.Remaining);
        }

        NativeBattleOilShot136Frame Deliver(NativeBattleOilShot136Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Oil Shot requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleOilShot136Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleOilShot136Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffectArena ReadTemporary(NativeBattleOilShot136Frame current, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Oil Shot requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        NativeBattleOilShot136Frame Execute(NativeBattleOilShot136Frame current, NativeBattleOilShot136Operation operation)
        {
            switch (operation)
            {
                case NativeBattleOilShot136DrawCustom92 mesh:
                    NativeBattleEffectArena context = ReadTemporary(current, mesh.TemporaryAddress, 92);
                    NativeBattleStaticMeshResource source = resources.InitialMesh with { Bytes = current.State.ReadMesh() };
                    NativeBattleWindowedMeshStep drawn = NativeBattleOilShot136Mesh.Draw(current.Memory, source, context, Projection());
                    return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, mesh.TemporaryAddress, drawn.Context, 92) }, drawn.Batch);
                case NativeBattleOilShot136Stage stage:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Oil Shot requires its current stage colors.");
                    NativeStage = stageState with { Groups = stageState.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Depth), ColorFlags = stage.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleOilShot136Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Oil Shot sound lost its process memory.") };
                case NativeBattleOilShot136Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(checked((nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Oil Shot requires its current first contact.")) + contact.Index))) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Oil Shot contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleOilShot136DrawSprite180 sprite:
                    NativeBattleModuleResource spriteSource = resources.Sprite(sprite.SourceAddress);
                    NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, ReadTemporary(current, sprite.TemporaryAddress, 180), address => address == spriteSource.Address ? spriteSource : throw new NotSupportedException("Oil Shot selected an unsupported sprite source."), Projection(), Camera().Roll, 17);
                    return Deliver(current with { Memory = spriteDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, sprite.TemporaryAddress, spriteDraw.Temporary180, 180) }, spriteDraw.Batch);
                default:
                    throw new NotSupportedException("Oil Shot operation has no proved implementation.");
            }
        }
    }
}
