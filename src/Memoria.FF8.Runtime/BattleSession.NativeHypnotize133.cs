using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeHypnotize133(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Hypnotize requires its selected original effect.");
        NativeBattleHypnotize133Resources resources = effect.Binding.Hypnotize133Resources ?? throw new NotSupportedException("Hypnotize requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Hypnotize requires its actual process memory.");
        NativeBattleHypnotize133Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Hypnotize requires its CRT stream."), memory.Hypnotize133 ?? throw new NotSupportedException("Hypnotize requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Hypnotize requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Hypnotize requires its original interpolation constants.");
        NativeBattleHypnotize133Inputs inputs = new(nativeActionPackets, actor => checked((byte)PoseSource(actor).BoneCount), Bone, PrepareContext, AdvanceAct, Execute);
        frame = initialize ? NativeBattleHypnotize133Controller.Initialize(frame, resources, inputs) : NativeBattleHypnotize133Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Hypnotize requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Hypnotize requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Hypnotize requires its current actor skeleton.");

        void Publish(NativeBattleHypnotize133Frame current)
        {
            NativeEffectMemory = current.Memory with { Hypnotize133 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Hypnotize lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleHypnotize133Frame ObserveBone(NativeBattleHypnotize133Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleHypnotize133PointStep Bone(NativeBattleHypnotize133Frame current, int actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveBone(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleHypnotize133ContextStep PrepareContext(NativeBattleHypnotize133Frame current, uint address, NativeBattleHypnotize133ContextKind kind)
        {
            NativeBattleModulePoolTask task = NativeBattleHypnotize133Controller.Read(current, address);
            NativeBattlePackedMatrix32 camera = Camera().CapturePackedView1D97778();
            uint morph = checked(current.State.ReadDword(NativeBattleHypnotize133Profile.WorkingGlobal) + 0xE04);
            NativeBattleHypnotize133ContextMemoryStep prepared;
            switch (kind)
            {
                case NativeBattleHypnotize133ContextKind.Main:
                    int caster = Caster(current);
                    if ((graphics.Models[caster].Flags0 & 2) != 0)
                    {
                        foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                        {
                            current = ObserveBone(current, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[caster], PoseSource(caster), selector, 0));
                        }
                    }
                    NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, caster) with { W = (graphics.Models[caster].Transform ?? throw new NotSupportedException("Hypnotize requires the actual caster heading.")).Angle36 };
                    prepared = NativeBattleHypnotize133Contexts.Screen(current.Memory, camera, new(anchor.X, anchor.Y, anchor.Z), anchor.W, Camera().FocalLength ?? throw new NotSupportedException("Hypnotize requires its actual signed focal shadow."), morph, Projection());
                    break;
                case NativeBattleHypnotize133ContextKind.Anchored:
                    int actor = Caster(current);
                    NativeBattleModuleAnchor local = new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)), unchecked((short)task.ReadWord(18)));
                    ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], PoseSource(actor));
                    if ((uint)local.W >= bones.Length)
                    {
                        throw new InvalidDataException("Hypnotize anchor leaves its actual current body bone publication.");
                    }
                    NativeBattleHypnotize133BonePointStep point = NativeBattleHypnotize133Contexts.BonePoint(current.Memory, graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Hypnotize requires its retained actor world matrix."), bones[local.W], local);
                    prepared = NativeBattleHypnotize133Contexts.Anchored(point.Memory, camera, point.Position, null, morph);
                    break;
                case NativeBattleHypnotize133ContextKind.Fading:
                    prepared = NativeBattleHypnotize133Contexts.Fading(current.Memory, camera, new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16))), unchecked((short)task.ReadWord(18)), morph, colors);
                    break;
                default:
                    throw new InvalidDataException("Hypnotize selected an unsupported ACT context.");
            }
            return new(current with { Memory = prepared.Memory }, prepared.Context40);
        }

        NativeBattleHypnotize133ActStep AdvanceAct(NativeBattleHypnotize133Frame current, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = new(NativeBattleHypnotize133Controller.Read(current, address).ReadBytes(offset, size).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleDefaultActContext40.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Hypnotize drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                NativeBattleDefaultActContext40Step drawn = NativeBattleDefaultActContext40.Draw(current.Memory, source, node, context, temporary, Projection(), colors, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(36, 4)), NativeBattleActContext40Policy.Hypnotize133);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                current = Deliver(current with { Memory = drawn.Memory, Staging = published }, drawn.Batch);
                current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, 88) };
                return next;
            });
            current = NativeBattleHypnotize133Controller.Write(current, address, NativeBattleHypnotize133Controller.Read(current, address).WriteBytes(offset, advanced.Workspace.Data.AsSpan()));
            return new(current, advanced.Remaining);
        }

        NativeBattleHypnotize133Frame Deliver(NativeBattleHypnotize133Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Hypnotize requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleHypnotize133Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleHypnotize133Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleHypnotize133Frame Execute(NativeBattleHypnotize133Frame current, NativeBattleHypnotize133Operation operation)
        {
            switch (operation)
            {
                case NativeBattleHypnotize133Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Hypnotize sound lost its process memory.") };
                case NativeBattleHypnotize133BulkContact bulk:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(bulk.First, bulk.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Hypnotize contact lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleHypnotize133DrawSprite180 sprite:
                    NativeBattleModulePoolTask task = NativeBattleHypnotize133Controller.Read(current, sprite.TaskAddress);
                    NativeBattleVector position = new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)));
                    short phase = unchecked((short)task.ReadWord(18));
                    NativeBattleEffectMemory prepared = NativeBattleModuleSpriteSetup.Setup571BC0(current.Memory, Camera().CapturePackedView1D97778(), position, phase, 1024, -256);
                    NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(current.Staging!, sprite.Temporary180, resources.Sprite.Address, 0).Write(sprite.Temporary180 + 4, BitConverter.GetBytes(phase));
                    NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, sprite.Temporary180, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
                    NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(prepared, temporary, source => source == resources.Sprite.Address ? resources.Sprite : throw new NotSupportedException("Hypnotize selected an unsupported sprite source."), Projection(), Camera().Roll);
                    return Deliver(current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, sprite.Temporary180, drawn.Temporary180, 180) }, drawn.Batch);
                default:
                    throw new NotSupportedException("Hypnotize operation has no proved implementation.");
            }
        }

        int Caster(NativeBattleHypnotize133Frame current)
        {
            uint pointer = current.State.ReadDword(NativeBattleHypnotize133Profile.CasterGlobal);
            if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new InvalidDataException("Hypnotize caster pointer leaves its actual model bank.");
            }
            return checked((int)((pointer - 0x1D972C0) / 156));
        }
    }
}
