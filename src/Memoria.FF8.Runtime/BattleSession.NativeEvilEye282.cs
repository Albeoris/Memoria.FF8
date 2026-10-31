using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEvilEye282(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Evil-Eye requires its selected original effect.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEvilEye282Resources resources = effect.Binding.EvilEye282Resources ?? throw new NotSupportedException("Evil-Eye requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Evil-Eye requires its actual process memory.");
        NativeBattleEvilEye282Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Evil-Eye requires its actual CRT stream."), memory.EvilEye282 ?? throw new NotSupportedException("Evil-Eye requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Evil-Eye requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Evil-Eye requires its original arithmetic constants.");
        NativeBattleEvilEye282Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], (_, pointer) => PoseSource(pointer).BoneCount, ReadParent, ReadAlias, ReadBoneMatrix, (_, pointer) => NativeBattlePackedMatrix32.FromMatrix(World(pointer), null), ReadBone502170, _ => nativeActionPackets, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEvilEye282Controller.Initialize(frame, resources, inputs) : NativeBattleEvilEye282Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Evil-Eye requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Evil-Eye requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Evil-Eye requires its actual current skeleton.");
        NativeBattleActionMatrix World(uint pointer) => graphics.Models[Actor(pointer)].GeometryWorldMatrix ?? throw new NotSupportedException("Evil-Eye requires its actual published model world matrix.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Evil-Eye requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        short ReadParent(NativeBattleEvilEye282Frame current, uint pointer, int bone)
        {
            NativeBattlePoseSource pose = PoseSource(pointer);
            if ((uint)bone >= pose.BoneCount)
            {
                throw new InvalidDataException("Evil-Eye parent selector leaves its current skeleton.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        }

        byte ReadAlias(NativeBattleEvilEye282Frame current, uint pointer, int selector)
        {
            NativeBattlePoseSource pose = PoseSource(pointer);
            int offset = checked(selector - 0xEC);
            if (selector < 0xF0 || (uint)offset >= pose.Skeleton.Length)
            {
                throw new InvalidDataException("Evil-Eye alias selector leaves its actual skeleton header.");
            }
            return pose.Skeleton[offset];
        }

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleEvilEye282Frame current, uint pointer, int bone)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, PoseSource(pointer));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Evil-Eye bone selector leaves its current CPU publication.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        NativeBattleEvilEye282PointStep ReadBone502170(NativeBattleEvilEye282Frame current, uint pointer, int selector, int distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[Actor(pointer)], PoseSource(pointer), checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Evil-Eye bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, packed, 0);
        }

        void Publish(NativeBattleEvilEye282Frame current)
        {
            NativeEffectMemory = current.Memory with { EvilEye282 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Evil-Eye lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEvilEye282Frame Import(NativeBattleEvilEye282Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Evil-Eye service lost its process memory.");
            return current with { Memory = latest, State = latest.EvilEye282 ?? throw new InvalidDataException("Evil-Eye service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Evil-Eye service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Evil-Eye service lost its CRT state.") };
        }

        NativeBattleEvilEye282Frame Deliver(NativeBattleEvilEye282Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleCinematicActorPresentation.NativeDepths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Evil-Eye requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleEvilEye282Profile.CursorGlobal);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), batch) } };
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEvilEye282Profile.CursorGlobal, checked(NativeBattleEvilEye282Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEvilEye282Frame Execute(NativeBattleEvilEye282Frame current, NativeBattleEvilEye282Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEvilEye282ContactsAll506BA0 contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleEvilEye282Stage5713E0 stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Evil-Eye requires its retained stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Value), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattleEvilEye282Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEvilEye282Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Arg0), checked((short)flash.Arg1), checked((short)flash.Arg2), checked((short)flash.Arg3)) };
                    return current;
                case NativeBattleEvilEye282DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleEvilEye282DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Evil-Eye emitted an unsupported native operation.");
            }
        }

        NativeBattleEvilEye282Frame DrawMesh(NativeBattleEvilEye282Frame current, NativeBattleEvilEye282DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Evil-Eye mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Evil-Eye mesh selected pointer differs from its actual context.");
            }
            NativeBattleEvilEye282MeshContext88Step drawn = NativeBattleEvilEye282MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Evil-Eye selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleEvilEye282Profile.ArenaAddress + NativeBattleEvilEye282Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Evil-Eye morph requires its current arena."), NativeBattleEvilEye282Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Evil-Eye selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleEvilEye282Frame DrawSprite(NativeBattleEvilEye282Frame current, NativeBattleEvilEye282DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Evil-Eye sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Evil-Eye sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Evil-Eye sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

    }
}
