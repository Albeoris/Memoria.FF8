using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeNeedlesFamily(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Needles requires its selected original effect.");
        NativeBattleNeedlesFamilyResources resources = effect.Binding.NeedlesFamilyResources ?? throw new NotSupportedException("Needles requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Needles requires its process memory.");
        NativeBattleNeedlesFamilyState state = SelectState(memory) ?? throw new NotSupportedException("Needles requires its retained native globals; migrate the selected older snapshot explicitly.");
        NativeBattleNeedlesFamilyFrame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Needles requires its CRT stream."), state) { Staging = NativeFileStaging ?? throw new NotSupportedException("Needles requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Needles requires its original interpolation constants.");
        NativeBattleNeedlesFamilyInputs inputs = new(nativeActionPackets, actor => checked((byte)PoseSource(actor).BoneCount), Bone, _ => Camera().CapturePackedView1D97778(), _ => graphics.Flags, Execute);
        frame = initialize ? NativeBattleNeedlesFamilyController.Initialize(frame, resources, inputs) : NativeBattleNeedlesFamilyController.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Needles requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Needles requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Needles requires its current actor skeleton.");
        NativeBattleNeedlesFamilyState? SelectState(NativeBattleEffectMemory current) => resources.Effect == 177 ? current.ThousandNeedles177 : current.TenThousandNeedles178;

        void Publish(NativeBattleNeedlesFamilyFrame current)
        {
            NativeEffectMemory = resources.Effect == 177 ? current.Memory with { ThousandNeedles177 = current.State } : current.Memory with { TenThousandNeedles178 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Needles lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleNeedlesFamilyFrame Import(NativeBattleNeedlesFamilyFrame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Needles external service lost its process memory.");
            return current with { Memory = latest, State = SelectState(latest) ?? throw new InvalidDataException("Needles external service lost its module owner."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Needles external service lost its CRT stream.") };
        }

        NativeBattleNeedlesFamilyPointStep Bone(NativeBattleNeedlesFamilyFrame current, byte actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleNeedlesFamilyFrame Deliver(NativeBattleNeedlesFamilyFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Needles requires its current ordering table.");
            NativeBattleNeedlesFamilyPacketWrite written = NativeBattleNeedlesFamilyPacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleNeedlesFamilyFrame DrawNeedle(NativeBattleNeedlesFamilyFrame current, NativeBattleNeedlesFamilyDrawNeedle draw)
        {
            NativeBattleEffectArena context = Temporary(current, draw.Temporary220, 220);
            NativeBattleEffectArena slot = current.State.ReadView(current.State.Profile.NeedleBank + checked((uint)(draw.Index * 56)), 56);
            NativeBattleNeedlesPrepareStep prepared = NativeBattleNeedlesFamilyDrawing.PrepareNeedle(current.Memory, context, slot, Packed(context, 0x2C));
            NativeBattleNeedlesDrawStep drawn = NativeBattleNeedlesFamilyDrawing.DrawNeedle(prepared.Memory, prepared.Context220, Projection(), colors, pilotIdentity: draw.PilotIdentity);
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, draw.Temporary220, drawn.Context220, 220);
            return Deliver(current with { Memory = drawn.Memory, Staging = staging }, drawn.Batch);
        }

        NativeBattleNeedlesFamilyFrame DrawSpark(NativeBattleNeedlesFamilyFrame current, NativeBattleNeedlesFamilyDrawSpark draw)
        {
            NativeBattleNeedlesFamilyProfile profile = current.State.Profile;
            NativeBattleEffectArena record = current.State.ReadView(profile.SparkBank + checked((uint)(draw.Index * 24)), 24);
            short life = BinaryPrimitives.ReadInt16LittleEndian(record.Read(4, 2));
            if ((uint)life >= resources.SparkDepthOffsets.Length)
            {
                throw new InvalidDataException("Needles spark lifetime leaves its original depth table.");
            }
            NativeBattleVector anchor = new(BinaryPrimitives.ReadInt16LittleEndian(record.Read(8, 2)), BinaryPrimitives.ReadInt16LittleEndian(record.Read(10, 2)), BinaryPrimitives.ReadInt16LittleEndian(record.Read(12, 2)));
            (byte[] wBytes, bool[] wKnown) = record.ReadSnapshot(14, 2);
            short? anchorW = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
            NativeBattleNeedlesSparkStep prepared = NativeBattleNeedlesFamilyDrawing.PrepareSpark(current.Memory, Packed(current.State.ReadView(profile.MatrixAddress, 32), 0), Camera().CapturePackedView1D97778(), anchor, anchorW, 256, 0, Camera().FocalLength ?? throw new NotSupportedException("Needles requires its actual signed focal shadow."), Projection());
            byte[] bytes = prepared.Matrix.Bytes.ToArray();
            int y = unchecked(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)) + resources.SparkDepthOffsets[life]);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), y);
            NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().Write(0, bytes);
            for (int index = 0; index < 32; index++)
            {
                if (!prepared.Matrix.Known[index])
                {
                    matrix = matrix.MarkUnknown(index, 1);
                }
            }
            NativeBattleGeometryRegisters geometry = prepared.Memory.GeometryRegisters ?? throw new InvalidDataException("Needles spark preparation lost its geometry controls.");
            geometry = geometry with { Translation = [BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)), y, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28))] };
            current = current with { Memory = prepared.Memory with { GeometryRegisters = geometry }, State = current.State.WriteView(profile.MatrixAddress, matrix, 32) };
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, Temporary(current, draw.Temporary180, 180), ReadSource, Projection(), Camera().Roll, pilotIdentity: draw.PilotIdentity);
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, draw.Temporary180, drawn.Temporary180, 180);
            return Deliver(current with { Memory = drawn.Memory, Staging = staging }, drawn.Batch);

            NativeBattleModuleResource ReadSource(uint address)
            {
                if (address != draw.Sprite40)
                {
                    throw new NotSupportedException("Needles spark source leaves its actual temporary resource.");
                }
                return new(0, address, 0, 0, current.Staging!.Read(address, 40).ToImmutableArray());
            }
        }

        NativeBattleNeedlesFamilyFrame Execute(NativeBattleNeedlesFamilyFrame current, NativeBattleNeedlesFamilyOperation operation)
        {
            switch (operation)
            {
                case NativeBattleNeedlesFamilyRegisterCamera camera:
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Needles requires its texture scheduler.")).Register5099A0(camera.Camera) } };
                case NativeBattleNeedlesFamilyEnqueueTim tim:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Needles requires its texture scheduler.")).EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Needles requires its initialized texture arena."), tim.ArenaOffset).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleNeedlesFamilySound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return Import(current);
                case NativeBattleNeedlesFamilyContact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleNeedlesFamilyDrawNeedle needle:
                    return DrawNeedle(current, needle);
                case NativeBattleNeedlesFamilyDrawSpark spark:
                    return DrawSpark(current, spark);
                default:
                    throw new NotSupportedException("Needles operation has no proved implementation.");
            }
        }
    }

    private static NativeBattleEffectArena Temporary(NativeBattleNeedlesFamilyFrame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(frame.Staging ?? throw new NotSupportedException("Needles requires its addressed temporary memory."), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
