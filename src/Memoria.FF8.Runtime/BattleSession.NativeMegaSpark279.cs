using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMegaSpark279(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Mega Spark requires its selected original effect.");
        NativeBattleMegaSpark279Resources resources = effect.Binding.MegaSpark279Resources ?? throw new NotSupportedException("Mega Spark requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Mega Spark requires its actual process memory.");
        NativeBattleMegaSpark279Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Mega Spark requires its actual CRT stream."), memory.MegaSpark279 ?? throw new NotSupportedException("Mega Spark requires its retained executable storage."))
        {
            Staging = NativeFileStaging ?? throw new NotSupportedException("Mega Spark requires its current temporary memory."),
            StackBytes = NativeBattleStackBytes.Unknown("mega-spark-current-root", -0x7C, 0x3C)
        };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Mega Spark requires its original arithmetic constants.");
        NativeBattleMegaSpark279Inputs inputs = new(nativeActionPackets, colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], (_, pointer) => PoseSource(pointer).BoneCount, ReadCurrentBoneMatrix, ReadBone502170, Anchor571400, _ => nativeActionPackets, Execute)
        {
            ReadRowPointW = _ => ReadNativeMegaSpark279RowPointW(graphics.EntryLifecycle)
        };
        frame = initialize ? NativeBattleMegaSpark279Controller.Initialize(frame, resources, inputs) : NativeBattleMegaSpark279Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Mega Spark requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Mega Spark requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Mega Spark requires its actual current skeleton.");


        NativeBattleActionMatrix World(uint pointer) => graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Mega Spark requires its actual current model world.");

        NativeBattlePackedMatrix32 ReadCurrentBoneMatrix(NativeBattleMegaSpark279Frame current, uint row100, int bone)
        {
            NativeBattleGraphicsModel[] owners = graphics.Models.Where(model => (model.ActiveResourceRow100 is not null || model.ActiveStagedResourceRow100 is not null) && NativeBattleModelResourceRow.Address100(model) == row100).ToArray();
            if (owners.Length != 1)
            {
                throw new NotSupportedException("Mega Spark requires the actual unique current CPU bone-row owner.");
            }
            NativeBattleGraphicsModel owner = owners[0];
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(owner, PoseSource(checked(0x1D972C0u + (uint)owner.Index * 156)));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Mega Spark bone selector leaves its current CPU publication.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Mega Spark requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattleMegaSpark279PointStep ReadBone502170(NativeBattleMegaSpark279Frame current, uint pointer, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Mega Spark bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(pointer), checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Mega Spark bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleMegaSpark279PointStep Anchor571400(NativeBattleMegaSpark279Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Mega Spark anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleMegaSpark279PointStep first = ReadBone502170(current, pointer, 0xF0, 0);
            NativeBattleMegaSpark279PointStep second = ReadBone502170(first.Frame, pointer, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        void Publish(NativeBattleMegaSpark279Frame current)
        {
            NativeEffectMemory = current.Memory with { MegaSpark279 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Mega Spark lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleMegaSpark279Frame Import(NativeBattleMegaSpark279Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Mega Spark service lost its process memory.");
            return current with { Memory = latest, State = latest.MegaSpark279 ?? throw new InvalidDataException("Mega Spark service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Mega Spark service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Mega Spark service lost its CRT state.") };
        }

        NativeBattleMegaSpark279Frame Deliver(NativeBattleMegaSpark279Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal = NativeBattleMegaSpark279Profile.CursorGlobal, ushort flags = 0)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Mega Spark requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Mega Spark requires its current ordinary COMMON packet owner.");
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, batch, flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (cursorGlobal != NativeBattleMegaSpark279Profile.CursorGlobal)
            {
                throw new InvalidDataException("Mega Spark selected an unsupported native packet cursor.");
            }
            uint address = current.State.ReadDword(cursorGlobal);
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(address, 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Mega Spark requires its actual effect packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(NativeBattleMegaSpark279Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleMegaSpark279Frame Execute(NativeBattleMegaSpark279Frame current, NativeBattleMegaSpark279Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMegaSpark279ComposeCaster5095B0 compose:
                    int composedActor = Actor(compose.ModelAddress);
                    NativeBattleGraphicsModel composed = graphics.Models[composedActor];
                    ImmutableArray<NativeBattleActionMatrix> localBones = NativeBattleBodyBoneMatrices.Require(composed, PoseSource(compose.ModelAddress));
                    NativeBattleActionMatrix lastInput = localBones[^1];
                    NativeBattleActionMatrix world = World(compose.ModelAddress);
                    ulong storedZ = NativeBattleGfMatrices.StoredZ56C600(world, lastInput.X, lastInput.Y, lastInput.Z);
                    NativeBattleStackBytes stack = (current.StackBytes ?? throw new NotSupportedException("Mega Spark composition requires its current execution stack.")).StoreUInt64(-0x7C, storedZ, 0x56C6E5, "compose-last-bone-z");
                    composed = NativeBattleBodyBoneMatrices.Compose5095B0(composed, World(compose.ModelAddress));
                    graphics = graphics with { Models = graphics.Models.SetItem(composedActor, composed) };
                    return current with { StackBytes = stack };
                case NativeBattleMegaSpark279RestoreCaster508C90 restore:
                    int restoredActor = Actor(restore.ModelAddress);
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Mega Spark body restoration requires its actual temporary memory."), 88);
                    NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[restoredActor], PoseSource(restore.ModelAddress), NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
                    NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
                    graphics = graphics with { Models = graphics.Models.SetItem(restoredActor, prepared.Model) };
                    return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
                case NativeBattleMegaSpark279Contact506BA0 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(contact.FirstContactIndex, contact.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleMegaSpark279Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleMegaSpark279ModelFields fields:
                    int actor = Actor(fields.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    model = model with { Flags0 = unchecked((ushort)((model.Flags0 & ~fields.ClearFlags0) | fields.SetFlags0)), Rotation12 = fields.Rotation12 ?? model.Rotation12 };
                    if (fields.PositionZ is short z)
                    {
                        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Mega Spark requires its current position owner.");
                        model = model with { Transform = transform with { Position28 = transform.Position28 with { Z = z } } };
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                    return current;
                case NativeBattleMegaSpark279Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Arg0), checked((short)flash.Arg1), checked((short)flash.Arg2), checked((short)flash.Arg3)) };
                    return current;
                case NativeBattleMegaSpark279DeliverBatch delivered:
                    return Deliver(current, delivered.Batch, delivered.CursorGlobal, delivered.Flags);
                case NativeBattleMegaSpark279DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleMegaSpark279DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Mega Spark emitted an unsupported native operation.");
            }
        }

        NativeBattleMegaSpark279Frame DrawMesh(NativeBattleMegaSpark279Frame current, NativeBattleMegaSpark279DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Mega Spark mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Mega Spark mesh selected pointer differs from its actual context.");
            }
            NativeBattleMegaSpark279MeshContext88Step drawn = NativeBattleMegaSpark279MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Mega Spark selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleMegaSpark279Profile.ArenaAddress + NativeBattleMegaSpark279Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Mega Spark morph requires its current arena."), NativeBattleMegaSpark279Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Mega Spark selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleMegaSpark279Frame DrawSprite(NativeBattleMegaSpark279Frame current, NativeBattleMegaSpark279DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Mega Spark sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Mega Spark sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Mega Spark sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift));
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
