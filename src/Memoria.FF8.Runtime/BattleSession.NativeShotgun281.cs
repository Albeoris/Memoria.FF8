using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeShotgun281(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Shotgun requires its selected original effect.");
        NativeBattleShotgun281Resources resources = effect.Binding.Shotgun281Resources ?? throw new NotSupportedException("Shotgun requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Shotgun requires its actual process memory.");
        NativeBattleShotgun281Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Shotgun requires its actual CRT stream."), memory.Shotgun281 ?? throw new NotSupportedException("Shotgun requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Shotgun requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Shotgun requires its original arithmetic constants.");
        NativeBattleShotgun281Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], (_, pointer) => PoseSource(pointer).BoneCount, ReadParent, ReadAlias, ReadBoneMatrix, (_, pointer) => NativeBattlePackedMatrix32.FromMatrix(World(pointer), null), _ => nativeActionPackets, Execute);
        frame = initialize ? NativeBattleShotgun281Controller.Initialize(frame, resources, inputs) : NativeBattleShotgun281Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Shotgun requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Shotgun requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Shotgun requires its actual current skeleton.");
        NativeBattleActionMatrix World(uint pointer) => graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Shotgun requires its actual published model world matrix.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Shotgun requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        short ReadParent(NativeBattleShotgun281Frame current, uint pointer, int bone)
        {
            NativeBattlePoseSource pose = PoseSource(pointer);
            if ((uint)bone >= pose.BoneCount)
            {
                throw new InvalidDataException("Shotgun parent selector leaves its current skeleton.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        }

        byte ReadAlias(NativeBattleShotgun281Frame current, uint pointer, int selector)
        {
            NativeBattlePoseSource pose = PoseSource(pointer);
            int offset = checked(selector - 0xEC);
            if (selector < 0xF0 || (uint)offset >= pose.Skeleton.Length)
            {
                throw new InvalidDataException("Shotgun alias selector leaves its actual skeleton header.");
            }
            return pose.Skeleton[offset];
        }

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleShotgun281Frame current, uint pointer, int bone)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, PoseSource(pointer));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Shotgun bone selector leaves its current CPU publication.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        void Publish(NativeBattleShotgun281Frame current)
        {
            NativeEffectMemory = current.Memory with { Shotgun281 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Shotgun lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleShotgun281Frame Import(NativeBattleShotgun281Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Shotgun service lost its process memory.");
            return current with { Memory = latest, State = latest.Shotgun281 ?? throw new InvalidDataException("Shotgun service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Shotgun service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Shotgun service lost its CRT state.") };
        }

        NativeBattleShotgun281Frame Deliver(NativeBattleShotgun281Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Shotgun requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleShotgun281Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleShotgun281Profile.CursorGlobal, checked(NativeBattleShotgun281Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleShotgun281Frame Execute(NativeBattleShotgun281Frame current, NativeBattleShotgun281Operation operation)
        {
            switch (operation)
            {
                case NativeBattleShotgun281ComposeCaster5095B0 compose:
                    int composedActor = Actor(compose.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[composedActor];
                    _ = NativeBattleBodyBoneMatrices.Require(model, PoseSource(compose.ModelAddress));
                    model = NativeBattleBodyBoneMatrices.Compose5095B0(model, World(compose.ModelAddress));
                    graphics = graphics with { Models = graphics.Models.SetItem(composedActor, model) };
                    return current;
                case NativeBattleShotgun281RestoreCaster508C90 restore:
                    int restoredActor = Actor(restore.ModelAddress);
                    NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Shotgun bone preparation requires its actual temporary memory."), 88);
                    NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[restoredActor], PoseSource(restore.ModelAddress), NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
                    NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
                    graphics = graphics with { Models = graphics.Models.SetItem(restoredActor, prepared.Model) };
                    return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
                case NativeBattleShotgun281Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleShotgun281Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleShotgun281Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Arg0), checked((short)flash.Arg1), checked((short)flash.Arg2), checked((short)flash.Arg3)) };
                    return current;
                case NativeBattleShotgun281DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleShotgun281DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Shotgun emitted an unsupported native operation.");
            }
        }

        NativeBattleShotgun281Frame DrawMesh(NativeBattleShotgun281Frame current, NativeBattleShotgun281DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Shotgun mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Shotgun mesh selected pointer differs from its actual context.");
            }
            NativeBattleShotgun281MeshContext88Step drawn = NativeBattleShotgun281MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Shotgun selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleShotgun281Profile.ArenaAddress + NativeBattleShotgun281Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Shotgun morph requires its current arena."), NativeBattleShotgun281Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Shotgun selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleShotgun281Frame DrawSprite(NativeBattleShotgun281Frame current, NativeBattleShotgun281DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Shotgun sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Shotgun sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Shotgun sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift));
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
