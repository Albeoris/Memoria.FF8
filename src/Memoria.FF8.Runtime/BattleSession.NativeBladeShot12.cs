using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBladeShot12(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("BladeShot12 requires its selected original effect.");
        NativeBattleBladeShot12Resources resources = effect.Binding.BladeShot12Resources ?? throw new NotSupportedException("BladeShot12 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("BladeShot12 requires its actual process memory.");
        NativeBattleBladeShot12Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("BladeShot12 requires its actual CRT stream."), (memory.BladeShot12 ?? throw new NotSupportedException("BladeShot12 requires its retained executable storage.")).EnsureVertexSources(resources)) { Staging = NativeFileStaging ?? throw new NotSupportedException("BladeShot12 requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("BladeShot12 requires its interpolation constants.");
        NativeBattleBladeShot12Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("BladeShot12 requires its current projection."), (_, pointer) => graphics.Models[Actor(pointer)], Bone, Anchor, Execute);
        frame = initialize ? NativeBattleBladeShot12Controller.Initialize(frame, resources, inputs) : NativeBattleBladeShot12Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("BladeShot12 requires its actual camera.");

        void Publish(NativeBattleBladeShot12Frame current)
        {
            NativeEffectMemory = current.Memory with { BladeShot12 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("BladeShot12 lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBladeShot12Frame Import(NativeBattleBladeShot12Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("BladeShot12 external service lost its process memory.");
            return current with { Memory = latest, State = latest.BladeShot12 ?? throw new InvalidDataException("BladeShot12 external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("BladeShot12 external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("BladeShot12 external service lost its CRT stream.") };
        }

        NativeBattleBladeShot12PointStep Bone(NativeBattleBladeShot12Frame current, uint pointer, int selector, int distance)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Blade Shot12 bone query requires its actual model transform.");
                return new(current, Point(transform.Position28));
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("BladeShot12 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("BladeShot12 bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleBladeShot12PointStep Anchor(NativeBattleBladeShot12Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("BladeShot12 anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleBladeShot12PointStep first = Bone(current, pointer, 0xF0, 0);
            NativeBattleBladeShot12PointStep second = Bone(first.Frame, pointer, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleBladeShot12Frame Deliver(NativeBattleBladeShot12Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("BladeShot12 requires its pending ordering table.");
            NativeBattleBladeShot12PacketWrite written = NativeBattleBladeShot12PacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleBladeShot12Frame Execute(NativeBattleBladeShot12Frame current, NativeBattleBladeShot12Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBladeShot12Sound5013A0 sound:
                    Publish(current);
                    PublishNativeModuleSound(sound.Packed, sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(4, 2)), 0));
                    return Import(current);
                case NativeBattleBladeShot12Contact506690 contact:
                    Publish(current);
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleBladeShot12ActorMeshMask124 mask:
                    int actor = Actor(mask.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Blade Shot12 requires its current sequence mesh mask.");
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { SequenceState = sequence with { MeshMask124 = (sequence.MeshMask124 & ~mask.ClearMask) | mask.SetMask } }) };
                    return current;
                case NativeBattleBladeShot12DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)));
                case NativeBattleBladeShot12DrawCustomMesh88 selectedMesh:
                    return DrawCustomMesh(current, selectedMesh.TemporaryAddress, selectedMesh.SourceAddress, selectedMesh.TableBaseBucket, unchecked((byte)(selectedMesh.DepthShift & 31)));
                case NativeBattleBladeShot12DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)));
                default:
                    throw new NotSupportedException("BladeShot12 emitted an unsupported native operation.");
            }
        }

        NativeBattleBladeShot12Frame DrawSprite(NativeBattleBladeShot12Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("BladeShot12 sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("BladeShot12 sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("BladeShot12 sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("BladeShot12 sprite requires its projection."), Camera().Roll, bucket, shift);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleBladeShot12Frame DrawMesh(NativeBattleBladeShot12Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("BladeShot12 mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("BladeShot12 requires its current projection."), temporary, colors, bucket, shift, readInlineVertex: sourceAddress is 0x1362E3C or 0x136372C ? offset => ReadInline(current, sourceAddress, offset) : null);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleBladeShot12Frame DrawCustomMesh(NativeBattleBladeShot12Frame current, uint address, uint sourceAddress, int bucket, byte shift)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Blade Shot12 custom mesh requires its addressed temporary.");
            NativeBattleBladeShot12MeshContext88Step drawn = NativeBattleBladeShot12MeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Blade Shot12 requires its current projection."), Temporary(staging, address, 88), colors, bucket, shift);
            return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context88, 88) }, drawn.Batch);
        }

        int Actor(uint pointer)
        {
            long offset = (long)pointer - 0x1D972C0;
            if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
            {
                throw new InvalidDataException("Blade Shot12 requires its actual current model pointer.");
            }
            return checked((int)(offset / 156));
        }

        (NativeBattleVector Vertex, short? W) ReadInline(NativeBattleBladeShot12Frame current, uint sourceAddress, int offset)
        {
            if (sourceAddress is not (0x1362E3C or 0x136372C) || offset < 0 || (offset & 7) != 0 || offset > 392 - 8)
            {
                throw new InvalidDataException("Blade Shot12 mutable vertex reads require one current addressed49-vertex owner.");
            }
            NativeBattleEffectArena view = current.State.ReadView(checked(sourceAddress + 8 + (uint)offset), 8);
            byte[] xyz = view.Read(0, 6);
            (byte[] bytes, bool[] known) = view.ReadSnapshot(6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
