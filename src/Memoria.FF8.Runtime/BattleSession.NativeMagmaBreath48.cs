using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMagmaBreath48(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("MagmaBreath48 requires its selected original effect.");
        NativeBattleMagmaBreath48Resources resources = effect.Binding.MagmaBreath48Resources ?? throw new NotSupportedException("MagmaBreath48 requires its original node resources and controls.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("MagmaBreath48 requires its actual process memory.");
        NativeBattleMagmaBreath48Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("MagmaBreath48 requires its actual CRT stream."), (memory.MagmaBreath48 ?? throw new NotSupportedException("MagmaBreath48 requires its retained executable storage.")).EnsureSources(resources)) { Staging = NativeFileStaging ?? throw new NotSupportedException("MagmaBreath48 requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("MagmaBreath48 requires its original arithmetic constants.");
        NativeBattleMagmaBreath48Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], Bone, _ => Camera().Roll, Execute);
        frame = initialize ? NativeBattleMagmaBreath48Controller.Initialize(frame, resources, inputs) : NativeBattleMagmaBreath48Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize && ((effect.Parameters ?? throw new NotSupportedException("MagmaBreath48 requires its native parameters.")).Flags79 & 1) == 0)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("MagmaBreath48 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("MagmaBreath48 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("MagmaBreath48 requires its actual current actor skeleton.");

        void Publish(NativeBattleMagmaBreath48Frame current)
        {
            NativeEffectMemory = current.Memory with { MagmaBreath48 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("MagmaBreath48 lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleMagmaBreath48Frame Import(NativeBattleMagmaBreath48Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("MagmaBreath48 service lost its process memory.");
            return current with { Memory = latest, State = latest.MagmaBreath48 ?? throw new InvalidDataException("MagmaBreath48 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("MagmaBreath48 service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("MagmaBreath48 service lost its CRT state.") };
        }

        NativeBattleMagmaBreath48Frame Observe(NativeBattleMagmaBreath48Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("MagmaBreath48 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleMagmaBreath48PointStep Bone(NativeBattleMagmaBreath48Frame current, int actor, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("MagmaBreath48 disabled bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = Observe(current, producer);
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new InvalidDataException("MagmaBreath48 bone query lost its current arithmetic registers.");
            return new(current, Point(new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value))));
        }

        NativeBattleMagmaBreath48Frame Delivered(NativeBattleMagmaBreath48Frame current, NativeBattleMagmaBreath48PacketStep step)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("MagmaBreath48 requires its actual ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = step.Ordering } };
            return current with { Memory = step.Memory, State = step.State };
        }

        NativeBattleLogicalOrdering Ordering() => graphics.PrimitiveFrame?.Pending ?? throw new NotSupportedException("MagmaBreath48 requires its current pending ordering table.");

        NativeBattleMagmaBreath48Frame Execute(NativeBattleMagmaBreath48Frame current, NativeBattleMagmaBreath48Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMagmaBreath48UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleMagmaBreath48Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    return NativeBattleMagmaBreath48Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleMagmaBreath48UpdateTargetBounds8DC610_8DC870 target:
                    NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleMagmaBreath48Controller.Read(current, target.TaskAddress), graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = PoseSource(actor);
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("MagmaBreath48 target bounds require its actual current pose."), source));
                    return NativeBattleMagmaBreath48Controller.Write(current, target.TaskAddress, task);
                case NativeBattleMagmaBreath48Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleMagmaBreath48Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleMagmaBreath48StageColorDepth stage:
                    NativeBattleStageState retainedDepth = NativeStage ?? throw new NotSupportedException("Magma Breath requires its actual stage group owner.");
                    if ((uint)stage.Group >= 4 || retainedDepth.Groups.Length < 4)
                    {
                        throw new InvalidDataException("Magma Breath stage depth targets one of its four native group rows.");
                    }
                    NativeStage = retainedDepth with { Groups = retainedDepth.Groups.SetItem(stage.Group, retainedDepth.Groups[stage.Group] with { ColorDepth = stage.Depth }) };
                    return current;
                case NativeBattleMagmaBreath48StageRgb rgb:
                    NativeBattleStageState retainedRgb = NativeStage ?? throw new NotSupportedException("Magma Breath requires its actual stage RGB owner.");
                    if ((uint)rgb.Group >= 4 || retainedRgb.Groups.Length < 4)
                    {
                        throw new InvalidDataException("Magma Breath stage RGB targets one of its four native group rows.");
                    }
                    uint color = (uint)(rgb.R | rgb.G << 8 | rgb.B << 16);
                    NativeStage = retainedRgb with { Groups = retainedRgb.Groups.SetItem(rgb.Group, retainedRgb.Groups[rgb.Group] with { ColorFlags = (retainedRgb.Groups[rgb.Group].ColorFlags & 0xFF000000u) | color }) };
                    return current;
                case NativeBattleMagmaBreath48DrawMesh88 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleMagmaBreath48DeliverBatch batch:
                    return Delivered(current, NativeBattleMagmaBreath48PacketDelivery.Deliver(current.Memory, current.State, Ordering(), batch.Batch, batch.CursorGlobal, batch.Flags));
                case NativeBattleMagmaBreath48SubmitPackets packets:
                    return Delivered(current, NativeBattleMagmaBreath48PacketDelivery.Submit(current.Memory, current.State, Ordering(), packets.Batch));
                default:
                    throw new NotSupportedException("MagmaBreath48 emitted an unsupported native operation.");
            }
        }

        NativeBattleMagmaBreath48Frame DrawMesh(NativeBattleMagmaBreath48Frame current, NativeBattleMagmaBreath48DrawMesh88 operation)
        {
            if (operation.SourceAddress is not (0x15AE4FC or 0x15B0514))
            {
                throw new NotSupportedException("Magma Breath mesh requires one of its two original current geometry owners.");
            }
            NativeBattleStaticMeshResource mesh = current.State.ReadMesh(resources, operation.SourceAddress);
            int faceCursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < strides.Length; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(faceCursor, 4));
                faceCursor = checked(faceCursor + 4);
                if (count < 0 || count != 0 && group is not (6 or 7) || count > (mesh.Bytes.Length - faceCursor) / strides[group])
                {
                    throw new InvalidDataException("Magma Breath current faces leave its original two textured Gouraud groups.");
                }
                for (int face = 0; face < count; face++)
                {
                    byte opcode = mesh.Bytes[faceCursor + face * strides[group] + 3];
                    if (group == 6 ? opcode is not (0x34 or 0x36) : opcode is not (0x3C or 0x3E))
                    {
                        throw new NotSupportedException("Magma Breath current opcode changes its native depth-FIFO insertion shape.");
                    }
                }
                faceCursor = checked(faceCursor + count * strides[group]);
            }
            if (operation.TemporaryAddress != NativeBattleEffectNodeContextMemory.Address + 92)
            {
                throw new InvalidDataException("Magma Breath mesh temporary must alias its actual shared sprite-context tail.");
            }
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectNodeContextMemory.Capture(current.Memory), 92, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), readInlineVertex: ReadVertex);
            NativeBattleEffectArena shared = NativeBattleEffectNodeContextMemory.Copy(drawn.Context, 0, NativeBattleEffectNodeContextMemory.Capture(drawn.Memory), 92, 88);
            current = current with { Memory = NativeBattleEffectNodeContextMemory.Publish(drawn.Memory, shared) };
            return Delivered(current, NativeBattleMagmaBreath48PacketDelivery.Deliver(current.Memory, current.State, Ordering(), drawn.Batch, 0x26481B8, 0));

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                if (offset < 0 || (offset & 7) != 0 || offset > 182 * 8 - 8)
                {
                    throw new InvalidDataException("Magma Breath current vertex leaves its original mesh.");
                }
                NativeBattleEffectArena point = current.State.ReadView(checked(mesh.Address + 8 + (uint)offset), 8);
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    }
}
