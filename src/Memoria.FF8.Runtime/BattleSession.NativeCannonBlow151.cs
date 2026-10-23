using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeCannonBlow151(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Cannon Blow requires its selected original effect.");
        NativeBattleCannonBlow151Resources resources = effect.Binding.CannonBlow151Resources ?? throw new NotSupportedException("Cannon Blow requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Cannon Blow requires its actual process memory.");
        NativeBattleCannonBlow151Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Cannon Blow requires its actual CRT stream."), memory.CannonBlow151 ?? throw new NotSupportedException("Cannon Blow requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Cannon Blow requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Cannon Blow requires its interpolation constants.");
        NativeBattleCannonBlow151Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Cannon Blow requires its current projection."), (_, actor) => graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Cannon Blow requires its actual retained model world matrix."), Bone, TargetCenter, Execute);
        frame = initialize ? NativeBattleCannonBlow151Controller.Initialize(frame, resources, inputs) : NativeBattleCannonBlow151Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Cannon Blow requires its actual camera.");

        void Publish(NativeBattleCannonBlow151Frame current)
        {
            NativeEffectMemory = current.Memory with { CannonBlow151 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Cannon Blow lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleCannonBlow151Frame Import(NativeBattleCannonBlow151Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Cannon Blow external service lost its process memory.");
            return current with { Memory = latest, State = latest.CannonBlow151 ?? throw new InvalidDataException("Cannon Blow external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Cannon Blow external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Cannon Blow external service lost its CRT stream.") };
        }

        NativeBattleCannonBlow151PointStep Bone(NativeBattleCannonBlow151Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Cannon Blow requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Cannon Blow bone query requires its actual temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleCannonBlow151PointStep TargetCenter(NativeBattleCannonBlow151Frame current, ushort mask)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Cannon Blow requires every selected physical target position.")).ToArray());
            return new(current, Point(center));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleCannonBlow151Frame Deliver(NativeBattleCannonBlow151Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Cannon Blow requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleCannonBlow151Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleCannonBlow151Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleCannonBlow151Frame Execute(NativeBattleCannonBlow151Frame current, NativeBattleCannonBlow151Operation operation)
        {
            switch (operation)
            {
                case NativeBattleCannonBlow151Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleCannonBlow151ContactsAll contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleCannonBlow151AdjustShake shake:
                    NativeCamera = Camera().AddViewTranslationY61B8D5(shake.Delta);
                    return current;
                case NativeBattleCannonBlow151ReleaseCommon release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.SourceAddress, checked(release.TaskAddress + 14), (address, value) => current = current with { Effect = NativeBattleCannonBlow151Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleCannonBlow151DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)));
                case NativeBattleCannonBlow151DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)));
                case NativeBattleCannonBlow151DrawScrolling124 scrolling:
                    return DrawScrolling(current, scrolling.TemporaryAddress, scrolling.SourceAddress, scrolling.TableBaseBucket, unchecked((byte)(scrolling.DepthShift & 31)));
                default:
                    throw new NotSupportedException("Cannon Blow emitted an unsupported native operation.");
            }
        }

        NativeBattleCannonBlow151Frame DrawSprite(NativeBattleCannonBlow151Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Cannon Blow sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Cannon Blow sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Cannon Blow sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Cannon Blow sprite requires its projection."), Camera().Roll, bucket, shift);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) }, drawn.Batch);
        }

        NativeBattleCannonBlow151Frame DrawMesh(NativeBattleCannonBlow151Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Cannon Blow mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Cannon Blow mesh requires its current geometry registers.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Cannon Blow mesh requires its current primitive process.");
            short? padding = geometry.Rotation20[18] is byte low && geometry.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null;
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(current.Memory, mesh, NativeBattleGeometry.CaptureMatrix(geometry), Camera().Projection ?? throw new NotSupportedException("Cannon Blow requires its current projection."), temporary, colors, matrixPadding18: padding, tableBaseBucket: bucket, depthShift: shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleCannonBlow151Frame DrawScrolling(NativeBattleCannonBlow151Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Cannon Blow scrolling mesh requires its actual temporary memory.");
            NativeBattleScrollingMeshContext124Step drawn = NativeBattleScrollingMeshContext124.DrawLoaded(current.Memory, resources.ReadMesh(sourceAddress), Camera().Projection ?? throw new NotSupportedException("Cannon Blow scrolling mesh requires its current projection."), Temporary(staging, address, 124), colors, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context124, 124) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
