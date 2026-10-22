using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect101(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect101 requires its selected original effect.");
        NativeBattleEffect101Resources resources = effect.Binding.Effect101Resources ?? throw new NotSupportedException("Effect101 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect101 requires its actual process memory.");
        NativeBattleEffect101Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect101 requires its CRT stream."), memory.Effect101 ?? throw new NotSupportedException("Effect101 requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect101 requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect101 requires its original interpolation constants.");
        NativeBattleEffect101Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], Position8, Bone0X, AdvanceAct, Blend, Execute, colors);
        frame = initialize ? NativeBattleEffect101Controller.Initialize(frame, resources, inputs) : NativeBattleEffect101Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect101 requires its current camera.");
        NativeBattleModelTransform Transform(int actor) => graphics.Models[actor].Transform ?? throw new NotSupportedException("Effect101 requires its actual actor position WORDs.");

        void Publish(NativeBattleEffect101Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect101 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect101 lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect101Frame Import(NativeBattleEffect101Frame current)
        {
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Effect101 lost its process memory."), Staging = NativeFileStaging };
        }

        NativeBattleEffect101PositionStep Position8(NativeBattleEffect101Frame current, int actor)
        {
            NativeBattleModelTransform transform = Transform(actor);
            NativeBattleEffectArena position = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Vertical34));
            return new(current, position);
        }

        NativeBattleEffect101WordStep Bone0X(NativeBattleEffect101Frame current, int actor)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect101 requires its target skeleton.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], pose);
            return new(current, unchecked((short)bones[0].X));
        }

        NativeBattleEffect101PointStep Blend(NativeBattleEffect101Frame current, int actor, NativeBattleEffectArena saved, short firstFactor, short secondFactor)
        {
            NativeBattleVector first = Transform(actor).Position28;
            NativeBattleVector second = new(BinaryPrimitives.ReadInt16LittleEndian(saved.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(saved.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(saved.Read(4, 2)));
            NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(current.Memory, first, second, firstFactor, secondFactor, colors);
            return new(current with { Memory = blended.Memory }, blended.Position);
        }

        NativeBattleEffect101ActStep AdvanceAct(NativeBattleEffect101Frame current, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context)
        {
            NativeBattleGfAnimationWorkspace workspace = new(NativeBattleEffect101Controller.Read(current, address).ReadBytes(offset, size).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleAnchorDepthAct16.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Effect101 drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = checked(current.State.ReadDword(NativeBattleEffect101Profile.WorkingGlobal) + NativeBattleEffect101Profile.MorphOffset);
                NativeBattleAnchorDepthAct16Step drawn = NativeBattleAnchorDepthAct16.Draw(current.Memory, source, node, context, Camera().CapturePackedView1D97778(), temporary, Camera().Projection ?? throw new NotSupportedException("Effect101 requires its current projection."), colors, morph, NativeBattleEffect101Profile.MorphBytes);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect101 requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleEffect101Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEffect101Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleEffect101Controller.Write(current, address, NativeBattleEffect101Controller.Read(current, address).WriteBytes(offset, advanced.Workspace.Data.AsSpan()));
            return new(current, advanced.Remaining);
        }

        NativeBattleEffect101Frame Execute(NativeBattleEffect101Frame current, NativeBattleEffect101Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect101ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.Mask) | (flags.Bits & flags.Mask))) }) };
                    return current;
                case NativeBattleEffect101PositionXYZ position:
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, graphics.Models[position.Actor] with { Transform = Transform(position.Actor) with { Position28 = position.Position } }) };
                    return current;
                case NativeBattleEffect101PositionY position:
                    NativeBattleModelTransform yTransform = Transform(position.Actor);
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, graphics.Models[position.Actor] with { Transform = yTransform with { Position28 = yTransform.Position28 with { Y = position.Y } } }) };
                    return current;
                case NativeBattleEffect101Position8 position:
                    NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(position.Data.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(position.Data.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(position.Data.Read(4, 2)));
                    short w = BinaryPrimitives.ReadInt16LittleEndian(position.Data.Read(6, 2));
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, graphics.Models[position.Actor] with { Transform = Transform(position.Actor) with { Position28 = xyz, Vertical34 = w } }) };
                    return current;
                case NativeBattleEffect101Color44 color:
                    graphics = graphics with { Models = graphics.Models.SetItem(color.Actor, graphics.Models[color.Actor] with { Color44 = color.Value }) };
                    return current;
                case NativeBattleEffect101ScalePointer scale:
                    graphics = graphics with { Models = graphics.Models.SetItem(scale.Actor, graphics.Models[scale.Actor] with { ScaleVectorAddress48 = scale.Address }) };
                    return current;
                case NativeBattleEffect101WorldR9TXYZ world:
                    NativeBattleGraphicsModel worldModel = graphics.Models[world.Actor];
                    NativeBattleActionMatrix prior = worldModel.GeometryWorldMatrix ?? throw new NotSupportedException("Effect101 requires its retained target matrix.");
                    graphics = graphics with { Models = graphics.Models.SetItem(world.Actor, worldModel with { GeometryWorldMatrix = prior with { Rotation = world.Matrix.Rotation, X = world.Matrix.X, Y = world.Matrix.Y, Z = world.Matrix.Z } }) };
                    return current;
                case NativeBattleEffect101Stage stage:
                    NativeBattleStageState previous = NativeStage ?? throw new NotSupportedException("Effect101 requires its current stage groups.");
                    NativeStage = previous with { Groups = previous.Groups.Select(group => group with { ColorDepth = stage.Depth, ColorFlags = stage.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect101RingSequence sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return Import(current);
                case NativeBattleEffect101Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return Import(current);
                default:
                    throw new NotSupportedException("Effect101 operation has no proved implementation.");
            }
        }
    }
}
