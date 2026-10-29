using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeAcid170(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Acid170 requires its selected original effect.");
        NativeBattleAcid170Resources resources = effect.Binding.Acid170Resources ?? throw new NotSupportedException("Acid170 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Acid170 requires its actual process memory.");
        NativeBattleAcid170Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Acid170 requires its actual CRT stream."), memory.Acid170 ?? throw new NotSupportedException("Acid170 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Acid170 requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Acid170 requires its interpolation constants.");
        NativeBattleAcid170Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Acid170 requires its current projection."), (_, actor) => graphics.Models[actor], Bone, Anchor, Execute);
        frame = initialize ? NativeBattleAcid170Controller.Initialize(frame, resources, inputs) : NativeBattleAcid170Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Acid170 requires its actual camera.");

        void Publish(NativeBattleAcid170Frame current)
        {
            NativeEffectMemory = current.Memory with { Acid170 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Acid170 lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleAcid170Frame Import(NativeBattleAcid170Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Acid170 external service lost its process memory.");
            return current with { Memory = latest, State = latest.Acid170 ?? throw new InvalidDataException("Acid170 external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Acid170 external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Acid170 external service lost its CRT stream.") };
        }

        NativeBattleAcid170PointStep Bone(NativeBattleAcid170Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Acid170 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Acid170 bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleAcid170PointStep Anchor(NativeBattleAcid170Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Acid170 anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleAcid170PointStep first = Bone(current, actor, 0xF0, 0);
            NativeBattleAcid170PointStep second = Bone(first.Frame, actor, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleAcid170Frame Deliver(NativeBattleAcid170Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Acid170 requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleAcid170Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleAcid170Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleAcid170Frame Execute(NativeBattleAcid170Frame current, NativeBattleAcid170Operation operation)
        {
            switch (operation)
            {
                case NativeBattleAcid170Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = checked((byte)sound.Pan) });
                    return Import(current);
                case NativeBattleAcid170Contact506690 contact:
                    Publish(current);
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleAcid170ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = unchecked((ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleAcid170DrawBody172 body:
                    Publish(current);
                    var drawnBody = DrawNativeAcid170Body(current, graphics, checked((byte)body.Actor), body.TemporaryAddress, body.PilotIdentity);
                    graphics = drawnBody.Graphics;
                    return drawnBody.Frame;
                case NativeBattleAcid170DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)), sprite.PilotIdentity);
                case NativeBattleAcid170DrawMesh92 selectedMesh:
                    return DrawSelectedMesh(current, selectedMesh.TemporaryAddress, selectedMesh.SourceAddress, selectedMesh.TableBaseBucket, unchecked((byte)(selectedMesh.DepthShift & 31)), selectedMesh.PilotIdentity);
                case NativeBattleAcid170DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)), mesh.PilotIdentity);
                default:
                    throw new NotSupportedException("Acid170 emitted an unsupported native operation.");
            }
        }

        NativeBattleAcid170Frame DrawSprite(NativeBattleAcid170Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Acid170 sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Acid170 sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Acid170 sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Acid170 sprite requires its projection."), Camera().Roll, bucket, shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleAcid170Frame DrawMesh(NativeBattleAcid170Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Acid170 mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Acid170 requires its current projection."), temporary, colors, bucket, shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleAcid170Frame DrawSelectedMesh(NativeBattleAcid170Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Acid170 selected mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 92);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0x1C, 4));
            uint? selected = (flags & 0x2000) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(4, 4)) : null;
            NativeBattleEffectMemory memory;
            NativeBattleGfGeometryBatch batch;
            if ((flags & 0x400) != 0)
            {
                NativeBattleAcid170MeshContext92Step drawn = NativeBattleAcid170MeshContext92.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Acid170 selected mesh requires its projection."), temporary, colors, bucket, shift, selected, selected is uint vertexAddress ? offset => SelectedVertex(current, vertexAddress, offset) : null, pilotIdentity: pilotIdentity);
                memory = drawn.Memory;
                temporary = drawn.Context92;
                batch = drawn.Batch;
            }
            else
            {
                NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Acid170 selected mesh requires its projection."), temporary, colors, bucket, shift, selected, selected is uint vertexAddress ? offset => SelectedVertex(current, vertexAddress, offset) : null, pilotIdentity: pilotIdentity);
                memory = drawn.Memory;
                temporary = drawn.Context;
                batch = drawn.Batch;
            }
            return Deliver(current with { Memory = memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, temporary, 92) }, batch);
        }

        (NativeBattleVector Vertex, short? W) SelectedVertex(NativeBattleAcid170Frame current, uint address, int offset)
        {
            if (address != 0x23B10F0 || offset < 0 || (offset & 7) != 0 || offset > 0x248 - 8)
            {
                throw new InvalidDataException("Acid170 selected vertex leaves its actual mutable owner.");
            }
            NativeBattleEffectArena vertices = current.State.ReadView(address, 0x248);
            byte[] xyz = vertices.Read(offset, 6);
            (byte[] bytes, bool[] known) = vertices.ReadSnapshot(offset + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
