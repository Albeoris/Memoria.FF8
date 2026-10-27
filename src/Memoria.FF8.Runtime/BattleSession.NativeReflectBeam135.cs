using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeReflectBeam135(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Reflect Beam requires its selected original effect.");
        NativeBattleReflectBeam135Resources resources = effect.Binding.ReflectBeam135Resources ?? throw new NotSupportedException("Reflect Beam requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Reflect Beam requires its actual process memory.");
        NativeBattleReflectBeam135Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Reflect Beam requires its CRT stream."), memory.ReflectBeam135 ?? throw new NotSupportedException("Reflect Beam requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Reflect Beam requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Reflect Beam requires its original interpolation constants.");
        NativeBattleReflectBeam135Inputs inputs = new(nativeActionPackets, colors, actor => graphics.Models[actor], _ => Camera().CapturePackedView1D97778(), Anchor, Bone, ReadCurrentBone, AdvanceAct, Execute);
        frame = initialize ? NativeBattleReflectBeam135Controller.Initialize(frame, resources, inputs) : NativeBattleReflectBeam135Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Reflect Beam requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Reflect Beam requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Reflect Beam requires its current actor skeleton.");

        void Publish(NativeBattleReflectBeam135Frame current)
        {
            NativeEffectMemory = current.Memory with { ReflectBeam135 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Reflect Beam lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleReflectBeam135Frame ObserveBone(NativeBattleReflectBeam135Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleReflectBeam135AnchorStep Anchor(NativeBattleReflectBeam135Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Reflect Beam requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Reflect Beam requires the actual actor heading.")).Angle36 });
        }

        NativeBattleReflectBeam135PointStep Bone(NativeBattleReflectBeam135Frame current, int actor, short selector, int distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattlePackedMatrix32 ReadCurrentBone(NativeBattleReflectBeam135Frame current, int actor, short selector)
        {
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], PoseSource(actor));
            if (selector < 0 || selector >= bones.Length)
            {
                throw new InvalidDataException("Reflect Beam requires its actual selected current bone row.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(bones[selector], null);
        }

        NativeBattleReflectBeam135ActStep AdvanceAct(NativeBattleReflectBeam135Frame current, uint address, NativeBattleGfAnimationSource source, int offset, int size, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(NativeBattleReflectBeam135Controller.Read(current, address), offset, size);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleReflectBeam135ActContext16.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("ReflectBeam135 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleReflectBeam135Profile.WorkingGlobal) + NativeBattleReflectBeam135Profile.MorphOffset);
                NativeBattleReflectBeam135ActContext16Step drawn = NativeBattleReflectBeam135ActContext16.Draw(current.Memory, source, node, context, Camera().CapturePackedView1D97778(), temporary, Projection(), colors, morph, NativeBattleReflectBeam135Profile.MorphBytes);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("ReflectBeam135 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleReflectBeam135Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleReflectBeam135Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleReflectBeam135Controller.Write(current, address, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleReflectBeam135Controller.Read(current, address), offset, advanced.Workspace));
            return new(current, advanced.Remaining);
        }

        NativeBattleReflectBeam135Frame Deliver(NativeBattleReflectBeam135Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Reflect Beam requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleReflectBeam135Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleReflectBeam135Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffectArena ReadTemporary(NativeBattleReflectBeam135Frame current, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Reflect Beam requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

        NativeBattleReflectBeam135Frame Execute(NativeBattleReflectBeam135Frame current, NativeBattleReflectBeam135Operation operation)
        {
            switch (operation)
            {
                case NativeBattleReflectBeam135Stage stage:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Reflect Beam requires its current stage colors.");
                    NativeStage = stageState with { Groups = stageState.Groups.Select(group => group with { ColorDepth = stage.Depth, ColorFlags = stage.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleReflectBeam135ComposeCaster compose:
                    NativeBattleGraphicsModel caster = graphics.Models[compose.Actor];
                    NativeBattleActionMatrix world = caster.GeometryWorldMatrix ?? throw new NotSupportedException("Reflect Beam requires its retained caster world matrix.");
                    graphics = graphics with { Models = graphics.Models.SetItem(compose.Actor, NativeBattleBodyBoneMatrices.Compose5095B0(caster, world)) };
                    return current;
                case NativeBattleReflectBeam135RestoreCaster restore:
                    graphics = graphics with { Models = graphics.Models.SetItem(restore.Actor, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[restore.Actor], PoseSource(restore.Actor))) };
                    return current;
                case NativeBattleReflectBeam135DrawStatic88 mesh:
                    if (mesh.SourceAddress != resources.StaticMesh.Address)
                    {
                        throw new NotSupportedException("Reflect Beam selected an unsupported static source.");
                    }
                    NativeBattleStaticMeshContext88Step meshDraw = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, resources.StaticMesh, Projection(), ReadTemporary(current, mesh.TemporaryAddress, 88), colors);
                    return Deliver(current with { Memory = meshDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, mesh.TemporaryAddress, meshDraw.Context, 88) }, meshDraw.Batch);
                case NativeBattleReflectBeam135Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Reflect Beam sound lost its process memory.") };
                case NativeBattleReflectBeam135Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(checked((nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C].FirstContactIndex ?? throw new InvalidDataException("Reflect Beam requires its current first contact.")) + contact.Index))) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Reflect Beam contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleReflectBeam135DrawSprite180 sprite:
                    NativeBattleModuleResource spriteSource = new[] { resources.SpriteA, resources.SpriteB, resources.SpriteC }.SingleOrDefault(source => source.Address == sprite.SourceAddress) ?? throw new NotSupportedException("Reflect Beam selected an unsupported sprite source.");
                    NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, ReadTemporary(current, sprite.TemporaryAddress, 180), address => address == spriteSource.Address ? spriteSource : throw new NotSupportedException("Reflect Beam selected an unsupported sprite source."), Projection(), Camera().Roll, 17);
                    return Deliver(current with { Memory = spriteDraw.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, sprite.TemporaryAddress, spriteDraw.Temporary180, 180) }, spriteDraw.Batch);
                default:
                    throw new NotSupportedException("Reflect Beam operation has no proved implementation.");
            }
        }
    }
}
