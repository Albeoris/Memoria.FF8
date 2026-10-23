using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeAdditional181(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Additional effect181 requires its selected original effect.");
        NativeBattleAdditional181Resources resources = effect.Binding.Additional181Resources ?? throw new NotSupportedException("Additional effect181 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Additional effect181 requires its actual process memory.");
        NativeBattleAdditional181Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Additional effect181 requires its actual CRT stream."), memory.Additional181 ?? throw new NotSupportedException("Additional effect181 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Additional effect181 requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Additional effect181 requires its interpolation constants.");
        NativeBattleAdditional181Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Additional effect181 requires its current projection."), (_, actor) => graphics.Models[actor], Bone, Anchor, Execute);
        frame = initialize ? NativeBattleAdditional181Controller.Initialize(frame, resources, inputs) : NativeBattleAdditional181Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Additional effect181 requires its actual camera.");

        void Publish(NativeBattleAdditional181Frame current)
        {
            NativeEffectMemory = current.Memory with { Additional181 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Additional effect181 lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleAdditional181Frame Import(NativeBattleAdditional181Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Additional effect181 external service lost its process memory.");
            return current with { Memory = latest, State = latest.Additional181 ?? throw new InvalidDataException("Additional effect181 external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Additional effect181 external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Additional effect181 external service lost its CRT stream.") };
        }

        NativeBattleAdditional181PointStep Bone(NativeBattleAdditional181Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Additional effect181 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Additional effect181 bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleAdditional181PointStep Anchor(NativeBattleAdditional181Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Additional effect181 anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleAdditional181PointStep first = Bone(current, actor, 0xF0, 0);
            NativeBattleAdditional181PointStep second = Bone(first.Frame, actor, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleAdditional181Frame Deliver(NativeBattleAdditional181Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Additional effect181 requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleAdditional181Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleAdditional181Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleAdditional181Frame Execute(NativeBattleAdditional181Frame current, NativeBattleAdditional181Operation operation)
        {
            switch (operation)
            {
                case NativeBattleAdditional181Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleAdditional181Contact506690 contact:
                    Publish(current);
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleAdditional181ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = unchecked((ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleAdditional181Flash5712C0 flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(checked((short)flash.Arg0), checked((short)flash.Arg1), checked((short)flash.Arg2), checked((short)flash.Arg3)) };
                    return current;
                case NativeBattleAdditional181DrawBody128 body:
                    Publish(current);
                    var drawnBody = DrawNativeAdditional181Body(current, graphics, checked((byte)body.Actor), body.TemporaryAddress);
                    graphics = drawnBody.Graphics;
                    return drawnBody.Frame;
                case NativeBattleAdditional181DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)));
                case NativeBattleAdditional181DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)));
                default:
                    throw new NotSupportedException("Additional effect181 emitted an unsupported native operation.");
            }
        }

        NativeBattleAdditional181Frame DrawSprite(NativeBattleAdditional181Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Additional effect181 sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Additional effect181 sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Additional effect181 sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Additional effect181 sprite requires its projection."), Camera().Roll, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleAdditional181Frame DrawMesh(NativeBattleAdditional181Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Additional effect181 mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Additional effect181 requires its current projection."), temporary, colors, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
