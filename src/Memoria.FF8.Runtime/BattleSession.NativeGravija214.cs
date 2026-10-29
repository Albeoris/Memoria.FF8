using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool TryWriteNativeGravija214Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleGravija214Profile.Entry } effect || address != NativeBattleGravija214Profile.ArenaAddress + 0x700 + 18)
        {
            return false;
        }
        updated = graphics with { OriginalEffect = NativeBattleGravija214Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }

    private NativeBattleGraphicsState RunNativeGravija214(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Gravija214 requires its selected original effect.");
        NativeBattleGravija214Resources resources = effect.Binding.Gravija214Resources ?? throw new NotSupportedException("Gravija214 requires its original node resources and controls.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Gravija214 requires its actual process memory.");
        NativeBattleGravija214Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Gravija214 requires its actual CRT stream."), (memory.Gravija214 ?? throw new NotSupportedException("Gravija214 requires its retained executable storage."))) { Staging = NativeFileStaging ?? throw new NotSupportedException("Gravija214 requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Gravija214 requires its original arithmetic constants.");
        NativeBattleGravija214Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], Anchor, _ => nativeActionPackets, Execute) { ReadOrderingTable = _ => OrderingTable(), CapturePresentation = true };
        frame = initialize ? NativeBattleGravija214Controller.Initialize(frame, resources, inputs) : NativeBattleGravija214Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Gravija214 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Gravija214 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gravija214 requires its actual current actor skeleton.");

        void Publish(NativeBattleGravija214Frame current)
        {
            NativeEffectMemory = current.Memory with { Gravija214 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gravija214 lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleGravija214Frame Import(NativeBattleGravija214Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Gravija214 service lost its process memory.");
            return current with { Memory = latest, State = latest.Gravija214 ?? throw new InvalidDataException("Gravija214 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Gravija214 service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Gravija214 service lost its CRT state.") };
        }

        NativeBattleGravija214Frame Observe(NativeBattleGravija214Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gravija214 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleGravija214PointStep Bone(NativeBattleGravija214Frame current, uint pointer, int selector, int distance)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Gravija214 disabled bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = Observe(current, producer);
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new InvalidDataException("Gravija214 bone query lost its current arithmetic registers.");
            return new(current, Point(new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value))));
        }

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Gravija requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattleGravija214PointStep Anchor(NativeBattleGravija214Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Gravija anchor requires its actual transform.")).Position28));
            }
            NativeBattleGravija214PointStep first = Bone(current, pointer, 0xF0, 0);
            NativeBattleGravija214PointStep second = Bone(first.Frame, pointer, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))));
        }

        uint OrderingTable()
        {
            byte buffer = graphics.PrimitiveFrame?.Pending.TableBuffer ?? throw new NotSupportedException("Gravija requires its current ordering-table bank.");
            if (buffer > 1)
            {
                throw new InvalidDataException("Gravija ordering-table bank exceeds its physical pair.");
            }
            return checked(0x1D8E058u + 17544u * buffer);
        }

        NativeBattleGravija214Frame Deliver(NativeBattleGravija214Frame current, NativeBattleGfGeometryBatch batch, ushort flags = 0, bool advanceCursor = true)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Gravija requires its current ordering table.");
            uint address = current.State.ReadDword(NativeBattleGravija214Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(address, 0), primitive.Pending, batch, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            current = current with { Memory = written.Memory };
            return advanceCursor ? current with { State = current.State.WriteDword(NativeBattleGravija214Profile.CursorGlobal, checked(NativeBattleGravija214Profile.ArenaAddress + (uint)written.Cursor)) } : current;
        }

        NativeBattleGravija214Frame Execute(NativeBattleGravija214Frame current, NativeBattleGravija214Operation operation)
        {
            switch (operation)
            {
                case NativeBattleGravija214Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleGravija214ContactsAll506BA0 contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleGravija214CommonTextureLoad508630 completion:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(completion.DestinationAddress, completion.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleGravija214Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleGravija214Clear45BEE0 clear:
                    return current with { Memory = NativeBattleGravija214Environment.Clear45BEE0(current.Memory, clear.Rectangle, clear.R, clear.G, clear.B) };
                case NativeBattleGravija214SetOrigin45B5E0 origin:
                    return current with { Memory = NativeBattleGravija214Environment.SetOrigin45B5E0(current.Memory, origin.X, origin.Y) };
                case NativeBattleGravija214DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleGravija214DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                case NativeBattleGravija214DeliverBatch delivery:
                    return Deliver(current, delivery.Batch, delivery.Flags, false);
                default:
                    throw new NotSupportedException("Gravija emitted an unsupported native operation.");
            }
        }

        NativeBattleGravija214Frame DrawMesh(NativeBattleGravija214Frame current, NativeBattleGravija214DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gravija mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Gravija mesh selected pointer differs from its actual context.");
            }
            NativeBattleGravija214MeshContext88Step drawn = NativeBattleGravija214MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Gravija selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleGravija214Profile.ArenaAddress + NativeBattleGravija214Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Gravija morph requires its current arena."), NativeBattleGravija214Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Gravija selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleGravija214Frame DrawSprite(NativeBattleGravija214Frame current, NativeBattleGravija214DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gravija sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Gravija sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Gravija sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    }
}
