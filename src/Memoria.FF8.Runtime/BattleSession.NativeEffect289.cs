using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect289(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect289 requires its selected original effect.");
        NativeBattleEffect289Resources resources = effect.Binding.Effect289Resources ?? throw new NotSupportedException("Effect289 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect289 requires its current process memory.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEffect289Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect289 requires its current CRT stream."), memory.Effect289 ?? throw new NotSupportedException("Effect289 requires its retained executable storage.")) { CapturePresentation = true, Staging = NativeFileStaging ?? throw new NotSupportedException("Effect289 requires its temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect289 requires its arithmetic constants.");
        NativeBattleEffect289Inputs? inputs = null;
        inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], _ => nativeActionPackets, Anchor, CasterPoint, Execute, (current, source, node, caller) => NativeBattleEffect289Controller.DrawProgrammeNode6780F0(current, source, node, caller, resources, inputs!));
        frame = initialize ? NativeBattleEffect289Controller.Initialize(frame, resources, inputs) : NativeBattleEffect289Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect289 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect289 requires its current projection.");
        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect289 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }
        void Publish(NativeBattleEffect289Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect289 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect289 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect289Frame Import(NativeBattleEffect289Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect289 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect289 ?? throw new InvalidDataException("Effect289 service lost its executable storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect289 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect289 service lost its temporary memory.") };
        }
        NativeBattleEffect289PointStep Bone(NativeBattleEffect289Frame current, int actor, byte selector)
        {
            Publish(current);
            graphics = graphics with { OriginalEffect = current.Effect };
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect289 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, selector, 0);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }
        NativeBattleEffect289PointStep Anchor(NativeBattleEffect289Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect289 requires its current model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect289PointStep first = Bone(current, actor, 0xF0);
            NativeBattleEffect289PointStep second = Bone(first.Frame, actor, 0xF1);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
        NativeBattleEffect289PointStep CasterPoint(NativeBattleEffect289Frame current, NativeBattleEffectArena source8, NativeBattleEffectArena retained8)
        {
            Publish(current);
            graphics = graphics with { OriginalEffect = current.Effect };
            int actor = Actor(current.State.ReadDword(NativeBattleEffect289Profile.CasterGlobal));
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect289 requires its current caster skeleton.");
            var bones = NativeBattleBodyBoneMatrices.Require(model, source);
            short selector = BinaryPrimitives.ReadInt16LittleEndian(source8.Read(6, 2));
            if ((uint)selector >= (uint)bones.Length)
            {
                throw new InvalidDataException("Effect289 point selector leaves its current caster rig.");
            }
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect289 requires its current model world matrix.");
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bones[selector]);
            NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometry.LoadMatrix(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect289 requires its current geometry registers."), composed, true, true, null), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect289 requires its current arithmetic registers."));
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(source8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(source8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(source8.Read(4, 2)));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, selector));
            for (int axis = 0; axis < 3; axis++)
            {
                retained8 = retained8.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect289 point did not publish its current IR result.")))));
            }
            return new(current with { Memory = current.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, retained8);
        }
        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        NativeBattleEffect289Frame Deliver(NativeBattleEffect289Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal = NativeBattleEffect289Profile.CursorGlobal, ushort flags = 0)
        {
            if (cursorGlobal != NativeBattleEffect289Profile.CursorGlobal)
            {
                throw new InvalidDataException("Effect289 selected an unsupported packet cursor.");
            }
            batch = NativeBattleActor32Presentation.Depths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect289 requires its current ordering table.");
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect289 requires its actual COMMON arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            var owned = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, resolved);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Effect = current.Effect with { PresentationPackets = owned }, Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(NativeBattleEffect289Profile.ArenaAddress + (uint)written.Cursor)) };
        }
        NativeBattleEffect289Frame DrawMesh(NativeBattleEffect289Frame current, uint temporary, uint source, uint vertices, int table, byte shift, bool custom)
        {
            int length = custom ? 108 : 88;
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect289 mesh requires its temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, temporary, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != vertices)
            {
                throw new InvalidDataException("Effect289 selected vertices differ from the current mesh context.");
            }
            NativeBattleStaticMeshResource mesh = custom ? resources.ReadMesh(current.State, source) : resources.ReadGeometry(source);
            NativeBattleGfGeometryBatch batch;
            if (custom)
            {
                NativeBattleEffect289CustomMesh108Step drawn = NativeBattleEffect289CustomMesh108.DrawLoaded(current.Memory, mesh, Projection(), context, colors, ReadVertex, table, shift, NativeBattleActor32Presentation.Resource(current.PilotIdentity, source, vertices));
                current = current with { Memory = drawn.Memory };
                context = drawn.Context108;
                batch = drawn.Batch;
            }
            else
            {
                NativeBattleEffect289MeshContext88Step drawn = NativeBattleEffect289MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, ReadVertex, table, shift, NativeBattleActor32Presentation.Resource(current.PilotIdentity, source, vertices));
                current = current with { Memory = drawn.Memory };
                context = drawn.Context88;
                batch = drawn.Batch;
            }
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(staging, temporary, context, length) };
            return Deliver(current, batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Effect289 selected vertex leaves its actual geometry.");
                }
                uint address = checked(vertices + (uint)offset);
                NativeBattleEffectArena point = IsNativeEffectArenaAddress(address) ? NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Effect289 requires its current scratch arena."), NativeBattleWaterExecutionFields.ArenaOffset(address, 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8) : current.State.ReadStatic(resources, address, 8);
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }
        NativeBattleEffect289Frame Execute(NativeBattleEffect289Frame current, NativeBattleEffect289Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect289Contact506690:
                    Publish(current);
                    var pending = graphics.PendingContacts;
                    int header = current.Effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("Effect289 requires its current action header.");
                    int contact = nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect289 requires its current first contact.");
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect289Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect289DrawCustom108 custom:
                    return DrawMesh(current, custom.TemporaryAddress, custom.SourceAddress, custom.VertexAddress, custom.TableBaseBucket, checked((byte)custom.DepthShift), true);
                case NativeBattleEffect289DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.VertexAddress, mesh.TableBaseBucket, checked((byte)mesh.DepthShift), false);
                case NativeBattleEffect289DrawSprite180 sprite:
                    NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect289 sprite requires its temporary owner.");
                    NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, sprite.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)) != sprite.SourceAddress)
                    {
                        throw new InvalidDataException("Effect289 sprite source differs from its actual context.");
                    }
                    NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, context, resources.ReadSprite, Projection(), Camera().Roll, sprite.TableBaseBucket, checked((byte)sprite.DepthShift), pilotIdentity: NativeBattleActor32Presentation.Resource(current.PilotIdentity, sprite.SourceAddress));
                    current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, sprite.TemporaryAddress, drawn.Temporary180, 180) };
                    return Deliver(current, drawn.Batch);
                case NativeBattleEffect289DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                default:
                    throw new NotSupportedException("Effect289 emitted an unsupported native operation.");
            }
        }
    }
}
