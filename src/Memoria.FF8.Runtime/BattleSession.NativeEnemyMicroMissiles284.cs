using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEnemyMicroMissiles284(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Enemy Micro Missiles requires its selected original effect.");
        NativeBattleEnemyMicroMissiles284Resources resources = effect.Binding.EnemyMicroMissiles284Resources ?? throw new NotSupportedException("Enemy Micro Missiles requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Enemy Micro Missiles requires its actual process memory.");
        NativeBattleEnemyMicroMissiles284Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Enemy Micro Missiles requires its actual CRT stream."), memory.EnemyMicroMissiles284 ?? throw new NotSupportedException("Enemy Micro Missiles requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Enemy Micro Missiles requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Enemy Micro Missiles requires its original arithmetic constants.");
        NativeBattleEnemyMicroMissiles284Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], (_, pointer) => PoseSource(pointer).BoneCount, ReadParent, ReadAlias, ReadBoneMatrix, (_, pointer) => NativeBattlePackedMatrix32.FromMatrix(World(pointer), null), _ => nativeActionPackets, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEnemyMicroMissiles284Controller.Initialize(frame, resources, inputs) : NativeBattleEnemyMicroMissiles284Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Enemy Micro Missiles requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Enemy Micro Missiles requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Enemy Micro Missiles requires its actual current skeleton.");
        NativeBattleActionMatrix World(uint pointer) => graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Enemy Micro Missiles requires its actual published model world matrix.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Enemy Micro Missiles requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        short ReadParent(NativeBattleEnemyMicroMissiles284Frame current, uint pointer, int bone)
        {
            NativeBattlePoseSource pose = PoseSource(pointer);
            if ((uint)bone >= pose.BoneCount)
            {
                throw new InvalidDataException("Enemy Micro Missiles parent selector leaves its current skeleton.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        }

        byte ReadAlias(NativeBattleEnemyMicroMissiles284Frame current, uint pointer, int selector)
        {
            NativeBattlePoseSource pose = PoseSource(pointer);
            int offset = checked(selector - 0xEC);
            if (selector < 0xF0 || (uint)offset >= pose.Skeleton.Length)
            {
                throw new InvalidDataException("Enemy Micro Missiles alias selector leaves its actual skeleton header.");
            }
            return pose.Skeleton[offset];
        }

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleEnemyMicroMissiles284Frame current, uint pointer, int bone)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, PoseSource(pointer));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Enemy Micro Missiles bone selector leaves its current CPU publication.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        void Publish(NativeBattleEnemyMicroMissiles284Frame current)
        {
            NativeEffectMemory = current.Memory with { EnemyMicroMissiles284 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Enemy Micro Missiles lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEnemyMicroMissiles284Frame Import(NativeBattleEnemyMicroMissiles284Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Enemy Micro Missiles service lost its process memory.");
            return current with { Memory = latest, State = latest.EnemyMicroMissiles284 ?? throw new InvalidDataException("Enemy Micro Missiles service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Enemy Micro Missiles service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Enemy Micro Missiles service lost its CRT state.") };
        }

        NativeBattleEnemyMicroMissiles284Frame Deliver(NativeBattleEnemyMicroMissiles284Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Enemy Micro Missiles requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleEnemyMicroMissiles284Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEnemyMicroMissiles284Profile.CursorGlobal, checked(NativeBattleEnemyMicroMissiles284Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEnemyMicroMissiles284Frame Execute(NativeBattleEnemyMicroMissiles284Frame current, NativeBattleEnemyMicroMissiles284Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEnemyMicroMissiles284Reaction505C00 reaction:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)Actor(reaction.ModelAddress)), reaction.Sequence);
                    return Import(current);
                case NativeBattleEnemyMicroMissiles284Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEnemyMicroMissiles284Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEnemyMicroMissiles284DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleEnemyMicroMissiles284DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Enemy Micro Missiles emitted an unsupported native operation.");
            }
        }

        NativeBattleEnemyMicroMissiles284Frame DrawMesh(NativeBattleEnemyMicroMissiles284Frame current, NativeBattleEnemyMicroMissiles284DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Enemy Micro Missiles mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Enemy Micro Missiles mesh selected pointer differs from its actual context.");
            }
            NativeBattleEnemyMicroMissiles284MeshContext88Step drawn = NativeBattleEnemyMicroMissiles284MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Enemy Micro Missiles selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleEnemyMicroMissiles284Profile.ArenaAddress + NativeBattleEnemyMicroMissiles284Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Enemy Micro Missiles morph requires its current arena."), NativeBattleEnemyMicroMissiles284Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Enemy Micro Missiles selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleEnemyMicroMissiles284Frame DrawSprite(NativeBattleEnemyMicroMissiles284Frame current, NativeBattleEnemyMicroMissiles284DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Enemy Micro Missiles sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Enemy Micro Missiles sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Enemy Micro Missiles sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
