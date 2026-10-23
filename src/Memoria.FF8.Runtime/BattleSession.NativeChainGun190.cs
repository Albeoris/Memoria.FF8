using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeChainGun190(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Chain Gun requires its selected original effect.");
        NativeBattleChainGun190Resources resources = effect.Binding.ChainGun190Resources ?? throw new NotSupportedException("Chain Gun requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Chain Gun requires its actual process memory.");
        NativeBattleChainGun190Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Chain Gun requires its actual CRT stream."), memory.ChainGun190 ?? throw new NotSupportedException("Chain Gun requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Chain Gun requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Chain Gun requires its interpolation constants.");
        NativeBattleChainGun190Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Chain Gun requires its current projection."), (_, actor) => graphics.Models[actor], (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Chain Gun requires its current model world matrix."), null), (_, actor) => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Chain Gun requires its actual current skeleton byte owner.")).BoneCount, Bone, Execute);
        frame = initialize ? NativeBattleChainGun190Controller.Initialize(frame, resources, inputs) : NativeBattleChainGun190Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Chain Gun requires its actual camera.");

        void Publish(NativeBattleChainGun190Frame current)
        {
            NativeEffectMemory = current.Memory with { ChainGun190 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Chain Gun lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleChainGun190Frame Import(NativeBattleChainGun190Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Chain Gun external service lost its process memory.");
            return current with { Memory = latest, State = latest.ChainGun190 ?? throw new InvalidDataException("Chain Gun external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Chain Gun external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Chain Gun external service lost its CRT stream.") };
        }

        NativeBattleChainGun190PointStep Bone(NativeBattleChainGun190Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Chain Gun requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Chain Gun bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleChainGun190Frame Deliver(NativeBattleChainGun190Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Chain Gun requires its pending ordering table.");
            NativeBattleChainGun190PacketWrite written = NativeBattleChainGun190PacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleChainGun190Frame Execute(NativeBattleChainGun190Frame current, NativeBattleChainGun190Operation operation)
        {
            switch (operation)
            {
                case NativeBattleChainGun190Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleChainGun190Contacts506BA0 contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleChainGun190DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)));
                case NativeBattleChainGun190DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)));
                default:
                    throw new NotSupportedException("Chain Gun emitted an unsupported native operation.");
            }
        }

        NativeBattleChainGun190Frame DrawSprite(NativeBattleChainGun190Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Chain Gun sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Chain Gun sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Chain Gun sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Chain Gun sprite requires its projection."), Camera().Roll, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleChainGun190Frame DrawMesh(NativeBattleChainGun190Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Chain Gun mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Chain Gun requires its current projection."), temporary, colors, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
