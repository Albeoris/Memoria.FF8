using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeStare45(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Stare45 requires its selected original effect.");
        NativeBattleStare45Resources resources = effect.Binding.Stare45Resources ?? throw new NotSupportedException("Stare45 requires its original node resources and controls.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Stare45 requires its actual process memory.");
        NativeBattleStare45Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Stare45 requires its actual CRT stream."), (memory.Stare45 ?? throw new NotSupportedException("Stare45 requires its retained executable storage."))) { Staging = NativeFileStaging ?? throw new NotSupportedException("Stare45 requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Stare45 requires its original arithmetic constants.");
        NativeBattleStare45Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], Bone, _ => nativeActionPackets, Execute);
        frame = initialize ? NativeBattleStare45Controller.Initialize(frame, resources, inputs) : NativeBattleStare45Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize && ((effect.Parameters ?? throw new NotSupportedException("Stare45 requires its native parameters.")).Flags79 & 1) == 0)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Stare45 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Stare45 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Stare45 requires its actual current actor skeleton.");

        void Publish(NativeBattleStare45Frame current)
        {
            NativeEffectMemory = current.Memory with { Stare45 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Stare45 lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleStare45Frame Import(NativeBattleStare45Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Stare45 service lost its process memory.");
            return current with { Memory = latest, State = latest.Stare45 ?? throw new InvalidDataException("Stare45 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Stare45 service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Stare45 service lost its CRT state.") };
        }

        NativeBattleStare45Frame Observe(NativeBattleStare45Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Stare45 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleStare45PointStep Bone(NativeBattleStare45Frame current, int actor, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Stare45 disabled bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = Observe(current, producer);
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new InvalidDataException("Stare45 bone query lost its current arithmetic registers.");
            return new(current, Point(new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value))));
        }

        NativeBattleStare45Frame Delivered(NativeBattleStare45Frame current, NativeBattleStare45PacketStep step)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Stare45 requires its actual ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = step.Ordering } };
            return current with { Memory = step.Memory, State = step.State };
        }

        NativeBattleLogicalOrdering Ordering() => graphics.PrimitiveFrame?.Pending ?? throw new NotSupportedException("Stare45 requires its current pending ordering table.");

        NativeBattleStare45Frame Execute(NativeBattleStare45Frame current, NativeBattleStare45Operation operation)
        {
            switch (operation)
            {
                case NativeBattleStare45UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleStare45Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    return NativeBattleStare45Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleStare45UpdateTargetBounds8DC610_8DC870 target:
                    NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleStare45Controller.Read(current, target.TaskAddress), graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = PoseSource(actor);
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Stare45 target bounds require its actual current pose."), source));
                    return NativeBattleStare45Controller.Write(current, target.TaskAddress, task);
                case NativeBattleStare45Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleStare45Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleStare45StageColorDepth stage:
                    NativeBattleStageState retainedDepth = NativeStage ?? throw new NotSupportedException("Stare requires its actual stage group owner.");
                    if ((uint)stage.Group >= 4 || retainedDepth.Groups.Length < 4)
                    {
                        throw new InvalidDataException("Stare stage depth targets one of its four native group rows.");
                    }
                    NativeStage = retainedDepth with { Groups = retainedDepth.Groups.SetItem(stage.Group, retainedDepth.Groups[stage.Group] with { ColorDepth = stage.Depth }) };
                    return current;
                case NativeBattleStare45StageRgb rgb:
                    NativeBattleStageState retainedRgb = NativeStage ?? throw new NotSupportedException("Stare requires its actual stage RGB owner.");
                    if ((uint)rgb.Group >= 4 || retainedRgb.Groups.Length < 4)
                    {
                        throw new InvalidDataException("Stare stage RGB targets one of its four native group rows.");
                    }
                    uint color = (uint)(rgb.R | rgb.G << 8 | rgb.B << 16);
                    NativeStage = retainedRgb with { Groups = retainedRgb.Groups.SetItem(rgb.Group, retainedRgb.Groups[rgb.Group] with { ColorFlags = (retainedRgb.Groups[rgb.Group].ColorFlags & 0xFF000000u) | color }) };
                    return current;
                case NativeBattleStare45DrawMesh104 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleStare45DeliverBatch batch:
                    return Delivered(current, NativeBattleStare45PacketDelivery.Deliver(current.Memory, current.State, Ordering(), batch.Batch, batch.CursorGlobal, batch.Flags));
                default:
                    throw new NotSupportedException("Stare45 emitted an unsupported native operation.");
            }
        }

        NativeBattleStare45Frame DrawMesh(NativeBattleStare45Frame current, NativeBattleStare45DrawMesh104 operation)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Stare45 mesh requires its addressed temporary memory.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 104), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
            uint shapes = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
            if (operation.VertexAddress != 0x2655428 && !Enumerable.Range(0, checked((int)shapes)).Any(shape => operation.VertexAddress == checked(mesh.Address + 12 + (uint)(shape * count * 8))))
            {
                throw new InvalidDataException("Stare45 vertex source is not an actual selected geometry shape or its mutable scratch.");
            }
            NativeBattleStare45MeshContext104Step drawn = NativeBattleStare45MeshContext104.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), operation.VertexAddress, ReadVertex);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context104, 104) };
            return Delivered(current, NativeBattleStare45PacketDelivery.Deliver(current.Memory, current.State, Ordering(), drawn.Batch, 0x1D8E054, 0));

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Stare45 selected vertex leaves its actual geometry count.");
                }
                uint address = checked(operation.VertexAddress + (uint)offset);
                NativeBattleEffectArena view;
                if (operation.VertexAddress == 0x2655428)
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
