using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSnipeLaser302(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Snipe Laser requires its selected original effect.");
        NativeBattleSnipeLaser302Resources resources = effect.Binding.SnipeLaser302Resources ?? throw new NotSupportedException("Snipe Laser requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Snipe Laser requires its actual process memory.");
        NativeBattleSnipeLaser302Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Snipe Laser requires its actual CRT stream."), memory.SnipeLaser302 ?? throw new NotSupportedException("Snipe Laser requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Snipe Laser requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Snipe Laser requires its original arithmetic constants.");
        NativeBattleSnipeLaser302Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], ReadBoneMatrix, (_, pointer) => NativeBattlePackedMatrix32.FromMatrix(World(pointer), null), ReadBone502170, Anchor571400, _ => nativeActionPackets, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleSnipeLaser302Controller.Initialize(frame, resources, inputs) : NativeBattleSnipeLaser302Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Snipe Laser requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Snipe Laser requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Snipe Laser requires its actual current skeleton.");
        NativeBattleActionMatrix World(uint pointer) => graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Snipe Laser requires its actual published model world matrix.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Snipe Laser requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleSnipeLaser302Frame current, uint pointer, int bone)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, PoseSource(pointer));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Snipe Laser bone selector leaves its current CPU publication.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        NativeBattleSnipeLaser302PointStep ReadBone502170(NativeBattleSnipeLaser302Frame current, uint pointer, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Snipe Laser bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(pointer), checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Snipe Laser bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleSnipeLaser302PointStep Anchor571400(NativeBattleSnipeLaser302Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Snipe Laser anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleSnipeLaser302PointStep first = ReadBone502170(current, pointer, 0xF0, 0);
            NativeBattleSnipeLaser302PointStep second = ReadBone502170(first.Frame, pointer, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        void Publish(NativeBattleSnipeLaser302Frame current)
        {
            NativeEffectMemory = current.Memory with { SnipeLaser302 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Snipe Laser lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleSnipeLaser302Frame Import(NativeBattleSnipeLaser302Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Snipe Laser service lost its process memory.");
            return current with { Memory = latest, State = latest.SnipeLaser302 ?? throw new InvalidDataException("Snipe Laser service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Snipe Laser service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Snipe Laser service lost its CRT state.") };
        }

        NativeBattleSnipeLaser302Frame Deliver(NativeBattleSnipeLaser302Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Snipe Laser requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleSnipeLaser302Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleSnipeLaser302Profile.CursorGlobal, checked(NativeBattleSnipeLaser302Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleSnipeLaser302Frame Execute(NativeBattleSnipeLaser302Frame current, NativeBattleSnipeLaser302Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSnipeLaser302Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleSnipeLaser302Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleSnipeLaser302DrawStatic88 mesh:
                    return DrawStatic(current, mesh);
                case NativeBattleSnipeLaser302DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleSnipeLaser302DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Snipe Laser emitted an unsupported native operation.");
            }
        }

        NativeBattleSnipeLaser302Frame DrawStatic(NativeBattleSnipeLaser302Frame current, NativeBattleSnipeLaser302DrawStatic88 operation)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadStatic(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Snipe Laser requires its actual static-mesh temporary memory.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleSnipeLaser302StaticMeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context, 88) }, drawn.Batch);
        }

        NativeBattleSnipeLaser302Frame DrawMesh(NativeBattleSnipeLaser302Frame current, NativeBattleSnipeLaser302DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Snipe Laser mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Snipe Laser mesh selected pointer differs from its actual context.");
            }
            NativeBattleSnipeLaser302MeshContext88Step drawn = NativeBattleSnipeLaser302MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Snipe Laser selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleSnipeLaser302Profile.ArenaAddress + NativeBattleSnipeLaser302Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Snipe Laser morph requires its current arena."), NativeBattleSnipeLaser302Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Snipe Laser selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleSnipeLaser302Frame DrawSprite(NativeBattleSnipeLaser302Frame current, NativeBattleSnipeLaser302DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Snipe Laser sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Snipe Laser sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Snipe Laser sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
