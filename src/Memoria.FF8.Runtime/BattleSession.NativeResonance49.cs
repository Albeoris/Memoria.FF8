using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeResonance49(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Resonance49 requires its selected original effect.");
        NativeBattleResonance49Resources resources = effect.Binding.Resonance49Resources ?? throw new NotSupportedException("Resonance49 requires its original programmes and controls.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Resonance49 requires its actual process memory.");
        NativeBattleResonance49Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Resonance49 requires its actual CRT stream."), memory.Resonance49 ?? throw new NotSupportedException("Resonance49 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Resonance49 requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Resonance49 requires its original arithmetic constants.");
        NativeBattleResonance49Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], Bone, Environment, current => current.Memory.PrimitiveProcess?.Buffer80 ?? throw new NotSupportedException("Resonance49 requires its actual primitive buffer selector."), Execute);
        frame = initialize ? NativeBattleResonance49Controller.Initialize(frame, resources, inputs) : NativeBattleResonance49Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize && ((effect.Parameters ?? throw new NotSupportedException("Resonance49 requires its native parameters.")).Flags79 & 1) == 0)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Resonance49 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Resonance49 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Resonance49 requires its actual current actor skeleton.");

        void Publish(NativeBattleResonance49Frame current)
        {
            NativeEffectMemory = current.Memory with { Resonance49 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Resonance49 lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleResonance49Frame Import(NativeBattleResonance49Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Resonance49 service lost its process memory.");
            return current with { Memory = latest, State = latest.Resonance49 ?? throw new InvalidDataException("Resonance49 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Resonance49 service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Resonance49 service lost its CRT state.") };
        }

        NativeBattleResonance49Frame Observe(NativeBattleResonance49Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Resonance49 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleResonance49PointStep Bone(NativeBattleResonance49Frame current, int actor, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Resonance49 disabled bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = Observe(current, producer);
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new InvalidDataException("Resonance49 bone query lost its current arithmetic registers.");
            return new(current, Point(new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value))));
        }

        NativeBattleEffectArena Environment(NativeBattleResonance49Frame current, int parity)
        {
            NativeSoftwareDrawingEnvironmentBuffers buffers = current.Memory.PrimitiveProcess?.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Resonance49 requires its current physical drawing environments.");
            ImmutableArray<byte> bytes = parity switch { 0 => buffers.Buffer0, 1 => buffers.Buffer1, _ => throw new InvalidDataException("Resonance49 drawing environment index leaves its actual two rows.") };
            if (bytes.IsDefault || bytes.Length != 92)
            {
                throw new NotSupportedException("Resonance49 requires the complete current ninety-two-byte drawing environment.");
            }
            return NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan());
        }

        NativeBattleResonance49Frame Delivered(NativeBattleResonance49Frame current, NativeBattleResonance49PacketStep step)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Resonance49 requires its actual ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = step.Ordering } };
            return current with { Memory = step.Memory, State = step.State };
        }

        NativeBattleLogicalOrdering Ordering() => graphics.PrimitiveFrame?.Pending ?? throw new NotSupportedException("Resonance49 requires its current pending ordering table.");

        NativeBattleResonance49Frame Execute(NativeBattleResonance49Frame current, NativeBattleResonance49Operation operation)
        {
            switch (operation)
            {
                case NativeBattleResonance49UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleResonance49Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    return NativeBattleResonance49Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleResonance49UpdateTargetBounds8DC610_8DC870 target:
                    NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleResonance49Controller.Read(current, target.TaskAddress), graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = PoseSource(actor);
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Resonance49 target bounds require its actual current pose."), source));
                    return NativeBattleResonance49Controller.Write(current, target.TaskAddress, task);
                case NativeBattleResonance49SelectSequence505CE0 selection:
                    if (((graphics.Models[selection.Actor].ResourceFlags44 ?? throw new NotSupportedException("Resonance49 sequence selection requires its current resource flags.")) & 0x140) != 0)
                    {
                        graphics = SelectNativeHeaderSequence(graphics, checked((byte)selection.Actor), checked((byte)selection.Sequence));
                    }
                    return current;
                case NativeBattleResonance49Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleResonance49Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleResonance49CommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.DestinationAddress, load.CompletionAddress, (address, value) => current = current with { Effect = WriteResonance49CompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleResonance49DrawMesh104 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleResonance49DeliverBatch batch:
                    return Delivered(current, NativeBattleResonance49PacketDelivery.Deliver(current.Memory, current.State, Ordering(), batch.Batch, batch.CursorGlobal, batch.Flags));
                case NativeBattleResonance49StorePacketBatch packet:
                    return Delivered(current, NativeBattleResonance49PacketDelivery.Store(current.Memory, current.State, Ordering(), packet.Batch, packet.DestinationAddress, packet.Flags));
                case NativeBattleResonance49DeliverScreenPair pair:
                    return Delivered(current, NativeBattleResonance49PacketDelivery.ScreenPair(current.Memory, current.State, Ordering(), pair.Quad, pair.Mode));
                default:
                    throw new NotSupportedException("Resonance49 emitted an unsupported native operation.");
            }
        }

        NativeBattleResonance49Frame DrawMesh(NativeBattleResonance49Frame current, NativeBattleResonance49DrawMesh104 operation)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Resonance49 mesh requires its addressed temporary memory.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 104), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
            uint shapes = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
            if (operation.VertexAddress != 0x2647D20 && !Enumerable.Range(0, checked((int)shapes)).Any(shape => operation.VertexAddress == checked(mesh.Address + 12 + (uint)(shape * count * 8))))
            {
                throw new InvalidDataException("Resonance49 vertex source is not an actual selected geometry shape or its mutable scratch.");
            }
            NativeBattleResonance49MeshContext104Step drawn = NativeBattleResonance49MeshContext104.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), operation.VertexAddress, ReadVertex, pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context104, 104) };
            return Delivered(current, NativeBattleResonance49PacketDelivery.Deliver(current.Memory, current.State, Ordering(), drawn.Batch, 0x26464DC, 0));

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Resonance49 selected vertex leaves its actual geometry count.");
                }
                uint address = checked(operation.VertexAddress + (uint)offset);
                NativeBattleEffectArena view;
                if (operation.VertexAddress == 0x2647D20)
                {
                    view = current.State.ReadView(address, 8);
                }
                else
                {
                    int sourceOffset = checked((int)(address - mesh.Address));
                    view = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(sourceOffset, 8));
                }
                byte[] xyz = view.Read(0, 6);
                (byte[] bytes, bool[] known) = view.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
            }
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    }
}
