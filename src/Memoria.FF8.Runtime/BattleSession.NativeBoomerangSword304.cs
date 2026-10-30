using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBoomerangSword304(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Boomerang Sword requires its selected original effect.");
        NativeBattleBoomerangSword304Resources resources = effect.Binding.BoomerangSword304Resources ?? throw new NotSupportedException("Boomerang Sword requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Boomerang Sword requires its actual process memory.");
        NativeBattleBoomerangSword304Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Boomerang Sword requires its actual CRT stream."), memory.BoomerangSword304 ?? throw new NotSupportedException("Boomerang Sword requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Boomerang Sword requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Boomerang Sword requires its original arithmetic constants.");
        NativeBattleBoomerangSword304Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], ReadBone502170, Anchor571400, _ => nativeActionPackets, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleBoomerangSword304Controller.Initialize(frame, resources, inputs) : NativeBattleBoomerangSword304Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Boomerang Sword requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Boomerang Sword requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Boomerang Sword requires its actual current skeleton.");


        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Boomerang Sword requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattleBoomerangSword304PointStep ReadBone502170(NativeBattleBoomerangSword304Frame current, uint pointer, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Boomerang Sword bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(pointer), checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Boomerang Sword bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleBoomerangSword304PointStep Anchor571400(NativeBattleBoomerangSword304Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Boomerang Sword anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleBoomerangSword304PointStep first = ReadBone502170(current, pointer, 0xF0, 0);
            NativeBattleBoomerangSword304PointStep second = ReadBone502170(first.Frame, pointer, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        void Publish(NativeBattleBoomerangSword304Frame current)
        {
            NativeEffectMemory = current.Memory with { BoomerangSword304 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Boomerang Sword lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBoomerangSword304Frame Import(NativeBattleBoomerangSword304Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Boomerang Sword service lost its process memory.");
            return current with { Memory = latest, State = latest.BoomerangSword304 ?? throw new InvalidDataException("Boomerang Sword service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Boomerang Sword service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Boomerang Sword service lost its CRT state.") };
        }

        NativeBattleBoomerangSword304Frame Deliver(NativeBattleBoomerangSword304Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleBoomerangSword304Presentation.PreserveNativeDepths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Boomerang Sword requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleBoomerangSword304Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBoomerangSword304Profile.CursorGlobal, checked(NativeBattleBoomerangSword304Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleBoomerangSword304Frame Execute(NativeBattleBoomerangSword304Frame current, NativeBattleBoomerangSword304Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBoomerangSword304Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleBoomerangSword304Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleBoomerangSword304ActorMeshMask124 mask:
                    int actor = Actor(mask.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Boomerang Sword requires its current sequence mesh mask.");
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { SequenceState = sequence with { MeshMask124 = (sequence.MeshMask124 & ~mask.ClearMask) | mask.SetMask } }) };
                    return current;
                case NativeBattleBoomerangSword304DrawStatic88 mesh:
                    return DrawStatic(current, mesh);
                case NativeBattleBoomerangSword304DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleBoomerangSword304DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Boomerang Sword emitted an unsupported native operation.");
            }
        }

        NativeBattleBoomerangSword304Frame DrawStatic(NativeBattleBoomerangSword304Frame current, NativeBattleBoomerangSword304DrawStatic88 operation)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadStatic(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Boomerang Sword requires its actual static-mesh temporary memory.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleBoomerangSword304StaticMeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context, 88) }, drawn.Batch);
        }

        NativeBattleBoomerangSword304Frame DrawMesh(NativeBattleBoomerangSword304Frame current, NativeBattleBoomerangSword304DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Boomerang Sword mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Boomerang Sword mesh selected pointer differs from its actual context.");
            }
            NativeBattleBoomerangSword304MeshContext88Step drawn = NativeBattleBoomerangSword304MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Boomerang Sword selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleBoomerangSword304Profile.ArenaAddress + NativeBattleBoomerangSword304Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Boomerang Sword morph requires its current arena."), NativeBattleBoomerangSword304Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Boomerang Sword selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleBoomerangSword304Frame DrawSprite(NativeBattleBoomerangSword304Frame current, NativeBattleBoomerangSword304DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Boomerang Sword sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Boomerang Sword sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Boomerang Sword sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
