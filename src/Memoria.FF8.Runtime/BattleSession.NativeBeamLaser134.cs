using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBeamLaser134(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Beam Laser requires its selected original effect.");
        NativeBattleBeamLaser134Resources resources = effect.Binding.BeamLaser134Resources ?? throw new NotSupportedException("Beam Laser requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Beam Laser requires its actual process memory.");
        NativeBattleBeamLaser134Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Beam Laser requires its CRT stream."), memory.BeamLaser134 ?? throw new NotSupportedException("Beam Laser requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Beam Laser requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Beam Laser requires its original interpolation constants.");
        NativeBattleBeamLaser134Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], Bone, Anchor, RotatePoint, ChildContext, WorldContext, AdvanceAct, Execute);
        frame = initialize ? NativeBattleBeamLaser134Controller.Initialize(frame, resources, inputs) : NativeBattleBeamLaser134Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Beam Laser requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Beam Laser requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Beam Laser requires its current actor skeleton.");

        void Publish(NativeBattleBeamLaser134Frame current)
        {
            NativeEffectMemory = current.Memory with { BeamLaser134 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Beam Laser lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBeamLaser134Frame ObserveBone(NativeBattleBeamLaser134Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleBeamLaser134PointStep Bone(NativeBattleBeamLaser134Frame current, int actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        uint Morph(NativeBattleBeamLaser134Frame current) => checked(current.State.ReadDword(NativeBattleBeamLaser134Profile.WorkingGlobal) + 0xC60);

        NativeBattleBeamLaser134AnchorStep Anchor(NativeBattleBeamLaser134Frame current, int actor)
        {
            if ((graphics.Models[actor].Flags0 & 2) != 0)
            {
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    current = ObserveBone(current, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0));
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (graphics.Models[actor].Transform ?? throw new NotSupportedException("Beam Laser requires its actual anchor WORD owner.")).Angle36 });
        }

        NativeBattleBeamLaser134RotateStep RotatePoint(NativeBattleBeamLaser134Frame current, NativeBattlePackedMatrix32 rotation, NativeBattleVector local, short? localW, NativeBattleVector anchor)
        {
            NativeBattleBeamLaser134PointMemoryStep rotated = NativeBattleBeamLaser134Contexts.RotatePoint(current.Memory, rotation, local, localW, anchor, Camera().CapturePackedView1D97778(), Morph(current));
            return new(current with { Memory = rotated.Memory }, rotated.Context40, rotated.UpdatedLocal);
        }

        NativeBattleBeamLaser134ContextStep ChildContext(NativeBattleBeamLaser134Frame current, uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleBeamLaser134Controller.Read(current, address);
            ImmutableArray<bool> known = Enumerable.Range(12, 32).Select(offset => task.ReadUnknownPayloadRanges() is not { } ranges || !ranges.Any(range => offset >= range.Offset && offset < range.Offset + range.Length)).ToImmutableArray();
            NativeBattlePackedMatrix32 matrix = new(task.ReadRawPayloadSpan().Slice(0, 32).ToArray().ToImmutableArray(), known);
            return new(current, NativeBattleBeamLaser134Contexts.Child(Camera().CapturePackedView1D97778(), matrix, Morph(current)));
        }

        NativeBattleBeamLaser134ContextStep WorldContext(NativeBattleBeamLaser134Frame current, NativeBattlePackedMatrix32 world, int depth)
        {
            NativeBattleEffectArena context = NativeBattleBeamLaser134Contexts.Child(Camera().CapturePackedView1D97778(), world, Morph(current)).Write(32, BitConverter.GetBytes(depth));
            return new(current, context);
        }

        NativeBattleBeamLaser134ActStep AdvanceAct(NativeBattleBeamLaser134Frame current, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = new(NativeBattleBeamLaser134Controller.Read(current, address).ReadBytes(offset, size).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleDefaultActContext40.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Beam Laser drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                NativeBattleDefaultActContext40Step drawn = NativeBattleDefaultActContext40.Draw(current.Memory, source, node, context, temporary, Projection(), colors, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(36, 4)), NativeBattleActContext40Policy.BeamLaser134);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                current = Deliver(current with { Memory = drawn.Memory, Staging = published }, drawn.Batch);
                current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, 88) };
                return next;
            });
            current = NativeBattleBeamLaser134Controller.Write(current, address, advanced.Workspace.CopyTo(NativeBattleBeamLaser134Controller.Read(current, address), offset, retainUnknown: false));
            return new(current, advanced.Remaining);
        }

        NativeBattleBeamLaser134Frame Deliver(NativeBattleBeamLaser134Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Beam Laser requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleBeamLaser134Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBeamLaser134Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleBeamLaser134Frame Execute(NativeBattleBeamLaser134Frame current, NativeBattleBeamLaser134Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBeamLaser134ActorFlags flags:
                    NativeBattleGraphicsModel model = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, model with { Flags0 = unchecked((ushort)((model.Flags0 & ~flags.Mask) | (flags.Bits & flags.Mask))) }) };
                    return current;
                case NativeBattleBeamLaser134DrawStatic88 mesh:
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Beam Laser requires its physical mesh temporary."), 88);
                    NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88).Write(0, BitConverter.GetBytes(resources.Mesh.Address)).Write(0x1C, BitConverter.GetBytes(mesh.FadeCue == 0 ? 0x30u : 0xF0u));
                    if (mesh.FadeCue != 0)
                    {
                        context = context.Write(8, new byte[3]).Write(12, BitConverter.GetBytes(mesh.FadeCue));
                    }
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(Camera().CapturePackedView1D97778().ReadMatrix(), mesh.World.ReadMatrix());
                    NativeBattleStaticMeshContext88Step drawnMesh = NativeBattleStaticMeshContext88.Draw(current.Memory, resources.Mesh, matrix, Projection(), context, colors);
                    current = Deliver(current with { Memory = drawnMesh.Memory, Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawnMesh.Context, 88) }, drawnMesh.Batch);
                    return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, 88) };
                case NativeBattleBeamLaser134Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Beam Laser sound lost its process memory.") };
                case NativeBattleBeamLaser134Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = ImmutableArray.Create(nativeActionPackets.ReadContact(contact.Index)) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Beam Laser contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleBeamLaser134DrawSprite180 sprite:
                    NativeBattleModulePoolTask task = NativeBattleBeamLaser134Controller.Read(current, sprite.TaskAddress);
                    NativeBattleModuleResource source = sprite.Moving ? resources.SpriteB : resources.SpriteA;
                    NativeBattleVector position = new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)));
                    short phase = unchecked((short)task.ReadWord(18));
                    NativeBattleEffectMemory prepared = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, Camera().CapturePackedView1D97778(), position, phase, (short)(sprite.Moving ? 3072 : 4096), (short)(sprite.Moving ? -768 : -1024));
                    NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(current.Staging!, sprite.Temporary180, source.Address, 0).Write(sprite.Temporary180 + 4, BitConverter.GetBytes(phase));
                    NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, sprite.Temporary180, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
                    NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(prepared, temporary, address => address == source.Address ? source : throw new NotSupportedException("Beam Laser selected an unsupported sprite source."), Projection(), Camera().Roll);
                    return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, sprite.Temporary180, drawn.Temporary180, 180) }, drawn.Batch);
                default:
                    throw new NotSupportedException("Beam Laser operation has no proved implementation.");
            }
        }
    }
}
