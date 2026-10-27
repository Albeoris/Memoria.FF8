using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSonic139(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Sonic Wave requires its selected original effect.");
        NativeBattleSonic139Resources resources = effect.Binding.Sonic139Resources ?? throw new NotSupportedException("Sonic Wave requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Sonic Wave requires its current process memory.");
        NativeBattleSonic139Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Sonic Wave requires its current CRT stream."), memory.Sonic139 ?? throw new NotSupportedException("Sonic Wave requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Sonic Wave requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Sonic Wave requires its color arithmetic.");
        NativeBattleSonic139Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, address) => graphics.Models[Actor(address)], (_, address) => (graphics.Models[Actor(address)].Transform ?? throw new NotSupportedException("Sonic Wave requires its actual actor heading.")).Angle36, Emitter, Anchor, AdvanceAct, Execute);
        frame = initialize ? NativeBattleSonic139Controller.Initialize(frame, resources, inputs) : NativeBattleSonic139Controller.Advance(frame, resources, inputs);
        if (!initialize && frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Sonic Wave requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Sonic Wave requires its current projection.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Sonic Wave requires its actual current actor skeleton.");
        static int Actor(uint address)
        {
            uint relative = unchecked(address - 0x1D972C0);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Sonic Wave actor pointer leaves its actual model bank.");
            }
            return (int)(relative / 156);
        }

        void Publish(NativeBattleSonic139Frame current)
        {
            NativeEffectMemory = current.Memory with { Sonic139 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Sonic Wave lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleSonic139Frame Import(NativeBattleSonic139Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Sonic Wave external service lost its process memory.");
            return current with { Memory = latest, State = latest.Sonic139 ?? throw new InvalidDataException("Sonic Wave external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Sonic Wave external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Sonic Wave external service lost its CRT stream.") };
        }

        NativeBattleSonic139PointStep Emitter(NativeBattleSonic139Frame current, NativeBattleEffectArena local)
        {
            int caster = Actor(current.State.ReadDword(0x2517B50));
            byte[] point = local.Read(0, 8);
            int bone = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6));
            NativeBattleGraphicsModel model = graphics.Models[caster];
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, Pose(caster));
            if ((uint)bone >= bones.Length)
            {
                throw new InvalidDataException("Sonic Wave emitter leaves its actual current body bone publication.");
            }
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Sonic Wave requires its actual actor world matrix.");
            (NativeBattleEffectMemory updated, NativeBattleVector position) = NativeBattleComposedAnchorTransform.Transform(current.Memory, NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]), point.ToImmutableArray());
            return new(current with { Memory = updated }, Point(position));
        }

        NativeBattleSonic139PointStep Anchor(NativeBattleSonic139Frame current, uint address)
        {
            int actor = Actor(address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, Pose(actor), selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, Point(new(anchor.X, anchor.Y, anchor.Z)).Write(6, BitConverter.GetBytes((model.Transform ?? throw new NotSupportedException("Sonic Wave requires its actual actor heading.")).Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleSonic139ActStep AdvanceAct(NativeBattleSonic139Frame current, uint address, NativeBattleGfAnimationSource source, NativeBattleSonic139ActContext context)
        {
            int size = source.Address switch
            {
                NativeBattleSonic139Profile.MainActAddress => 0x4AC,
                NativeBattleSonic139Profile.TargetActAddress => 0x278,
                _ => throw new NotSupportedException("Sonic Wave selected an unsupported ACT workspace source.")
            };
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(NativeBattleSonic139Controller.ReadBytes(current, address, size), 0, size);
            NativeBattleEffectArena caller = Packed(context.Matrix).Write(32, BitConverter.GetBytes(context.DepthBias)).Write(36, BitConverter.GetBytes(context.MorphAddress));
            NativeBattleActContext40Policy policy = NativeBattleActContext40Policy.Sonic139;
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleDefaultActContext40.Visible(node))
                {
                    return next;
                }
                (current, uint temporary) = NativeBattleSonic139Controller.Allocate(current, 88);
                NativeBattleDefaultActContext40Step drawn = NativeBattleDefaultActContext40.Draw(current.Memory, source, node, caller, NativeBattleSonic139Controller.Context(current, temporary, 88), Projection(), colors, context.MorphAddress, policy);
                current = NativeBattleSonic139Controller.PublishContext(current with { Memory = drawn.Memory }, temporary, drawn.Temporary88, 88);
                current = Deliver(current, drawn.Batch);
                current = NativeBattleSonic139Controller.Release(current, 88);
                return next;
            });
            current = NativeBattleSonic139Controller.WriteBytes(current, address, advanced.Workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), advanced.Workspace.Length);
            return new(current, advanced.Remaining);
        }

        static NativeBattleEffectArena Packed(NativeBattlePackedMatrix32 matrix)
        {
            matrix.Validate();
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, matrix.Bytes.AsSpan());
            for (int index = 0; index < matrix.Known.Length; index++)
            {
                if (!matrix.Known[index])
                {
                    result = result.MarkUnknown(index, 1);
                }
            }
            return result;
        }

        NativeBattleSonic139Frame Deliver(NativeBattleSonic139Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Sonic Wave requires its pending ordering table.");
            uint cursor = current.State.ReadDword(0x2520BF4);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(0x2520BF4, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleSonic139Frame Execute(NativeBattleSonic139Frame current, NativeBattleSonic139Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSonic139Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleSonic139WriteActorColor color:
                    int actor = Actor(color.ActorAddress);
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Color44 = color.Color }) };
                    return current;
                case NativeBattleSonic139SetActorFlags flags:
                    int selected = Actor(flags.ActorAddress);
                    NativeBattleGraphicsModel model = graphics.Models[selected];
                    graphics = graphics with { Models = graphics.Models.SetItem(selected, model with { Flags0 = unchecked((ushort)((model.Flags0 & ~flags.Clear) | flags.Set)) }) };
                    return current;
                case NativeBattleSonic139ContactForActor contact:
                    Publish(current);
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[(current.Effect.Parameters ?? throw new NotSupportedException("Sonic Wave requires its captured parameters.")).HeaderIndex7C];
                    int count = header.ReadByte(16);
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Sonic Wave requires its actual contacts.");
                    var pending = graphics.PendingContacts;
                    for (int index = 0; index < count; index++)
                    {
                        NativeBattleContactPacket packet = nativeActionPackets.ReadContact(first + index);
                        if (packet.Primary.ActorIndex == Actor(contact.ActorAddress))
                        {
                            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [packet] });
                        }
                    }
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleSonic139RequestCommonCompletion release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.SourceAddress, release.FlagAddress, (address, value) => current = current with { Effect = NativeBattleSonic139Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleSonic139AppendPacket packet:
                    return AppendPacket(current, packet);
                default:
                    throw new NotSupportedException("Sonic Wave emitted an unsupported native operation.");
            }
        }

        NativeBattleSonic139Frame AppendPacket(NativeBattleSonic139Frame current, NativeBattleSonic139AppendPacket packet)
        {
            if (packet.Allocation < packet.Written || packet.Written < 4 || packet.Data.Length != packet.Known.Length || packet.Data.Length < packet.Written || packet.Data.Length > packet.Allocation || packet.WriteMask.Length != packet.Data.Length || (packet.Allocation & 3) != 0 || (packet.Written & 3) != 0)
            {
                throw new InvalidDataException("Sonic Wave packet lacks its actual aligned reservation and write mask.");
            }
            int offset = checked((int)(packet.Address - NativeBattleEffectNodeMemory.ArenaAddress));
            byte[] bytes = new byte[packet.Allocation];
            packet.Data.CopyTo(bytes, 0);
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, packet.Allocation).ToArray();
            for (int index = 0; index < packet.Data.Length; index++)
            {
                mask[index] = packet.WriteMask[index];
                if (mask[index] == NativeBattleGfPacketByteState.Known && !packet.Known[index])
                {
                    throw new InvalidDataException("Sonic Wave packet write mask claims an unresolved source byte is known.");
                }
            }
            if (packet.Known.Skip(3).Take(packet.Written - 3).Contains(false))
            {
                throw new NotSupportedException("Sonic Wave packet commands require their actual written bytes.");
            }
            ImmutableArray<uint> words = Enumerable.Range(0, packet.Written / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive polygon = new(packet.Bucket, words, packet.Flags == 0 ? [0, 0, 0, 0] : []);
            NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), polygon)], packet.Allocation);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Sonic Wave requires its pending ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, offset, primitive.Pending, batch, packet.Flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory };
        }
    }
}
