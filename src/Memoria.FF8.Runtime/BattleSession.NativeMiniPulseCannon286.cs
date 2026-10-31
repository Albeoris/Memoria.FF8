using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMiniPulseCannon286(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Mini Pulse Cannon requires its selected original effect.");
        NativeBattleMiniPulseCannon286Resources resources = effect.Binding.MiniPulseCannon286Resources ?? throw new NotSupportedException("Mini Pulse Cannon requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Mini Pulse Cannon requires its actual process memory.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleMiniPulseCannon286Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Mini Pulse Cannon requires its actual CRT stream."), memory.MiniPulseCannon286 ?? throw new NotSupportedException("Mini Pulse Cannon requires its retained executable storage.")) { CapturePresentation = true, Staging = NativeFileStaging ?? throw new NotSupportedException("Mini Pulse Cannon requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Mini Pulse Cannon requires its original arithmetic constants.");
        NativeBattleMiniPulseCannon286Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], ReadBoneMatrix, (_, pointer) => NativeBattlePackedMatrix32.FromMatrix(World(pointer), null), Anchor571400, _ => nativeActionPackets, Execute);
        frame = initialize ? NativeBattleMiniPulseCannon286Controller.Initialize(frame, resources, inputs) : NativeBattleMiniPulseCannon286Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Mini Pulse Cannon requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Mini Pulse Cannon requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Mini Pulse Cannon requires its actual current skeleton.");
        NativeBattleActionMatrix World(uint pointer) => graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Mini Pulse Cannon requires its actual published model world matrix.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Mini Pulse Cannon requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleMiniPulseCannon286Frame current, uint pointer, int bone)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, PoseSource(pointer));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Mini Pulse Cannon bone selector leaves its current CPU publication.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        NativeBattleMiniPulseCannon286PointStep Bone(NativeBattleMiniPulseCannon286Frame current, uint pointer, int selector)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Mini Pulse Cannon bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(pointer), checked((byte)selector), 0);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Mini Pulse Cannon bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleMiniPulseCannon286PointStep Anchor571400(NativeBattleMiniPulseCannon286Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Mini Pulse Cannon anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleMiniPulseCannon286PointStep first = Bone(current, pointer, 0xF0);
            NativeBattleMiniPulseCannon286PointStep second = Bone(first.Frame, pointer, 0xF1);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        void Publish(NativeBattleMiniPulseCannon286Frame current)
        {
            NativeEffectMemory = current.Memory with { MiniPulseCannon286 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Mini Pulse Cannon lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleMiniPulseCannon286Frame Import(NativeBattleMiniPulseCannon286Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Mini Pulse Cannon service lost its process memory.");
            return current with { Memory = latest, State = latest.MiniPulseCannon286 ?? throw new InvalidDataException("Mini Pulse Cannon service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Mini Pulse Cannon service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Mini Pulse Cannon service lost its CRT state.") };
        }

        NativeBattleMiniPulseCannon286Frame Deliver(NativeBattleMiniPulseCannon286Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleActor32Presentation.Depths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Mini Pulse Cannon requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleMiniPulseCannon286Profile.CursorGlobal);
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(address, 0);
            var owned = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, batch);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Effect = current.Effect with { PresentationPackets = owned }, Memory = written.Memory, State = current.State.WriteDword(NativeBattleMiniPulseCannon286Profile.CursorGlobal, checked(NativeBattleMiniPulseCannon286Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleMiniPulseCannon286Frame Execute(NativeBattleMiniPulseCannon286Frame current, NativeBattleMiniPulseCannon286Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMiniPulseCannon286ComposeCaster5095B0 compose:
                    int composedActor = Actor(compose.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[composedActor];
                    _ = NativeBattleBodyBoneMatrices.Require(model, PoseSource(compose.ModelAddress));
                    model = NativeBattleBodyBoneMatrices.Compose5095B0(model, World(compose.ModelAddress));
                    graphics = graphics with { Models = graphics.Models.SetItem(composedActor, model) };
                    return current;
                case NativeBattleMiniPulseCannon286RestoreCaster508C90 restore:
                    int restoredActor = Actor(restore.ModelAddress);
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Mini Pulse Cannon bone preparation requires its actual temporary memory."), 88);
                    NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[restoredActor], PoseSource(restore.ModelAddress), NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
                    NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
                    graphics = graphics with { Models = graphics.Models.SetItem(restoredActor, prepared.Model) };
                    return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
                case NativeBattleMiniPulseCannon286Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleMiniPulseCannon286Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleMiniPulseCannon286ActorFlags flags:
                    int actor = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel flagged = graphics.Models[actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleMiniPulseCannon286DrawWindow108 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleMiniPulseCannon286DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Mini Pulse Cannon emitted an unsupported native operation.");
            }
        }

        NativeBattleMiniPulseCannon286Frame DrawMesh(NativeBattleMiniPulseCannon286Frame current, NativeBattleMiniPulseCannon286DrawWindow108 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Mini Pulse Cannon mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 108), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 108), 0, 108);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Mini Pulse Cannon mesh selected pointer differs from its actual context.");
            }
            NativeBattleMiniPulseCannon286MeshContext108Step drawn = NativeBattleMiniPulseCannon286MeshContext108.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, NativeBattleActor32Presentation.Resource(current.PilotIdentity, operation.SourceAddress, operation.VertexAddress));
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context108, 108) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Mini Pulse Cannon selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleMiniPulseCannon286Profile.ArenaAddress + NativeBattleMiniPulseCannon286Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Mini Pulse Cannon morph requires its current arena."), NativeBattleMiniPulseCannon286Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Mini Pulse Cannon selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleMiniPulseCannon286Frame DrawSprite(NativeBattleMiniPulseCannon286Frame current, NativeBattleMiniPulseCannon286DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Mini Pulse Cannon sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Mini Pulse Cannon sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Mini Pulse Cannon sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: NativeBattleActor32Presentation.Resource(current.PilotIdentity, operation.SourceAddress));
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
