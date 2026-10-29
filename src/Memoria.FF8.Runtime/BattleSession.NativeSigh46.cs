using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSigh46(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Sigh46 requires its selected original effect.");
        NativeBattleSigh46Resources resources = effect.Binding.Sigh46Resources ?? throw new NotSupportedException("Sigh46 requires its original node resources and controls.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Sigh46 requires its actual process memory.");
        NativeBattleSigh46Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Sigh46 requires its actual CRT stream."), (memory.Sigh46 ?? throw new NotSupportedException("Sigh46 requires its retained executable storage.")).EnsureSources(resources)) { Staging = NativeFileStaging ?? throw new NotSupportedException("Sigh46 requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Sigh46 requires its original arithmetic constants.");
        NativeBattleSigh46Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, actor) => graphics.Models[actor], Bone, _ => Camera().Roll, _ => nativeActionPackets, Execute);
        frame = initialize ? NativeBattleSigh46Controller.Initialize(frame, resources, inputs) : NativeBattleSigh46Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize && ((effect.Parameters ?? throw new NotSupportedException("Sigh46 requires its native parameters.")).Flags79 & 1) == 0)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Sigh46 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Sigh46 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Sigh46 requires its actual current actor skeleton.");

        void Publish(NativeBattleSigh46Frame current)
        {
            NativeEffectMemory = current.Memory with { Sigh46 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Sigh46 lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleSigh46Frame Import(NativeBattleSigh46Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Sigh46 service lost its process memory.");
            return current with { Memory = latest, State = latest.Sigh46 ?? throw new InvalidDataException("Sigh46 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Sigh46 service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Sigh46 service lost its CRT state.") };
        }

        NativeBattleSigh46Frame Observe(NativeBattleSigh46Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Sigh46 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleSigh46PointStep Bone(NativeBattleSigh46Frame current, int actor, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Sigh46 disabled bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = Observe(current, producer);
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new InvalidDataException("Sigh46 bone query lost its current arithmetic registers.");
            return new(current, Point(new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value))));
        }

        NativeBattleSigh46Frame Delivered(NativeBattleSigh46Frame current, NativeBattleSigh46PacketStep step)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Sigh46 requires its actual ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = step.Ordering } };
            return current with { Memory = step.Memory, State = step.State };
        }

        NativeBattleLogicalOrdering Ordering() => graphics.PrimitiveFrame?.Pending ?? throw new NotSupportedException("Sigh46 requires its current pending ordering table.");

        NativeBattleSigh46Frame Execute(NativeBattleSigh46Frame current, NativeBattleSigh46Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSigh46UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleSigh46Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    return NativeBattleSigh46Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleSigh46UpdateTargetBounds8DC610_8DC870 target:
                    NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleSigh46Controller.Read(current, target.TaskAddress), graphics.Models, PoseSource, producer => current = Observe(current, producer));
                    int actor = task.ReadByte(0x2D);
                    NativeBattlePoseSource source = PoseSource(actor);
                    task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Sigh46 target bounds require its actual current pose."), source));
                    return NativeBattleSigh46Controller.Write(current, target.TaskAddress, task);
                case NativeBattleSigh46Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleSigh46Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleSigh46StageColorDepth stage:
                    NativeBattleStageState retainedDepth = NativeStage ?? throw new NotSupportedException("Sigh requires its actual stage group owner.");
                    if ((uint)stage.Group >= 4 || retainedDepth.Groups.Length < 4)
                    {
                        throw new InvalidDataException("Sigh stage depth targets one of its four native group rows.");
                    }
                    NativeStage = retainedDepth with { Groups = retainedDepth.Groups.SetItem(stage.Group, retainedDepth.Groups[stage.Group] with { ColorDepth = stage.Depth }) };
                    return current;
                case NativeBattleSigh46StageRgb rgb:
                    NativeBattleStageState retainedRgb = NativeStage ?? throw new NotSupportedException("Sigh requires its actual stage RGB owner.");
                    if ((uint)rgb.Group >= 4 || retainedRgb.Groups.Length < 4)
                    {
                        throw new InvalidDataException("Sigh stage RGB targets one of its four native group rows.");
                    }
                    uint color = (uint)(rgb.R | rgb.G << 8 | rgb.B << 16);
                    NativeStage = retainedRgb with { Groups = retainedRgb.Groups.SetItem(rgb.Group, retainedRgb.Groups[rgb.Group] with { ColorFlags = (retainedRgb.Groups[rgb.Group].ColorFlags & 0xFF000000u) | color }) };
                    return current;
                case NativeBattleSigh46DrawMesh88 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleSigh46DeliverBatch batch:
                    return Delivered(current, NativeBattleSigh46PacketDelivery.Deliver(current.Memory, current.State, Ordering(), batch.Batch, batch.CursorGlobal, batch.Flags));
                case NativeBattleSigh46VramCopy505EB0 copy:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Sigh requires its current texture queue.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueCopy505EB0(copy.Copy.SourceX, copy.Copy.SourceY, copy.Copy.Width, copy.Copy.Height, copy.Copy.DestinationX, copy.Copy.DestinationY) } };
                case NativeBattleSigh46CommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.DestinationAddress, load.CompletionAddress, (address, value) => current = current with { Effect = WriteSigh46CompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleSigh46DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Sigh46 emitted an unsupported native operation.");
            }
        }

        NativeBattleSigh46Frame DrawMesh(NativeBattleSigh46Frame current, NativeBattleSigh46DrawMesh88 operation)
        {
            NativeBattleStaticMeshResource mesh = current.State.ReadMesh(resources, operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Sigh mesh requires its addressed temporary memory.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), readInlineVertex: ReadVertex, pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context, 88) };
            return Delivered(current, NativeBattleSigh46PacketDelivery.Deliver(current.Memory, current.State, Ordering(), drawn.Batch, NativeBattleSigh46Profile.PrimaryCursorGlobal, 0));

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Sigh current vertex leaves its original mesh.");
                }
                NativeBattleEffectArena point = current.State.ReadView(checked(mesh.Address + 8 + (uint)offset), 8);
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleSigh46Frame DrawSprite(NativeBattleSigh46Frame current, NativeBattleSigh46DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Sigh sprite requires its addressed temporary memory.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Sigh sprite arguments differ from its current physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, address => address == resource.Address ? resource : throw new InvalidDataException("Sigh sprite changed its original source."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Delivered(current, NativeBattleSigh46PacketDelivery.Deliver(current.Memory, current.State, Ordering(), drawn.Batch, NativeBattleSigh46Profile.PrimaryCursorGlobal, 0));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    }
}
