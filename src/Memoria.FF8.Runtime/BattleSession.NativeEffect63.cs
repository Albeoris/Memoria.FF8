using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect63(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect63 requires its current effect.");
        NativeBattleEffect63Resources resources = effect.Binding.Effect63Resources ?? throw new NotSupportedException("Effect63 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect63 requires its current process memory.");
        NativeBattleEffect63Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect63 requires its current CRT stream."), memory.Effect63 ?? throw new NotSupportedException("Effect63 requires its retained state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect63 requires its current temporary memory.") };
        NativeBattleEffect63Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect63 requires its arithmetic constants."), _ => nativeActionPackets, (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect63Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect63 requires its current projection."), _ => Camera().Roll, Anchor, Point, Execute);
        frame = initialize ? NativeBattleEffect63Controller.Initialize(frame, resources, inputs) : NativeBattleEffect63Controller.Advance(frame, resources, inputs);
        PublishNativeEffect63Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect63 requires its current camera.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect63 requires its current physical skeleton.");

        NativeBattleEffect63PointStep Point(NativeBattleEffect63Frame current, uint address, NativeBattleEffectArena local, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect63Actor(graphics, address);
            NativeBattlePoseSource source = Pose(actor);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Effect63 requires its current pose temporary memory."), 88);
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(prepared.Model, source);
            short bone = BinaryPrimitives.ReadInt16LittleEndian(local.Read(6, 2));
            if ((uint)bone >= bones.Length)
            {
                throw new InvalidDataException("Effect63 local point selects a bone outside its current rig.");
            }
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(prepared.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect63 requires its current model world matrix."), bones[bone]);
            NativeBattleGeometryRegisterStep registers = new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect63 requires its current geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect63 requires its current arithmetic registers."));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed, true, true, null) };
            NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(local.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(local.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(local.Read(4, 2)));
            registers = NativeBattleGeometry.LoadVector(registers, 0, vector, bone);
            registers = NativeBattleGeometry.Transform(registers);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect63 local point did not publish its saturated IR output.")));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(value));
            }
            return new(current with { Memory = current.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, retained);
        }

        NativeBattleEffect63PointStep Bone(NativeBattleEffect63Frame current, int actor, byte selector, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect63 requires its current anchor temporary memory.");
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : Pose(actor);
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, 0);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(unchecked((short)point.X))).Write(2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(4, BitConverter.GetBytes(unchecked((short)point.Z)));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        NativeBattleEffect63PointStep Anchor(NativeBattleEffect63Frame current, uint address, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect63Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect63 requires its current target transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)transform.Position28.X))).Write(2, BitConverter.GetBytes(unchecked((short)transform.Position28.Y))).Write(4, BitConverter.GetBytes(unchecked((short)transform.Position28.Z))).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect63PointStep first = Bone(current, actor, 0xF0, retained);
            NativeBattleEffect63PointStep second = Bone(first.Frame, actor, 0xF1, retained);
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = axis * 2;
                short average = unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(offset, 2))) / 2));
                retained = retained.Write(offset, BitConverter.GetBytes(average));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        NativeBattleEffect63Frame Execute(NativeBattleEffect63Frame current, NativeBattleEffect63Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect63UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, NativeBattleEffect63Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect63 requires its texture scheduler.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect63RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect63 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect63Stage5713E0 color:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect63 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)color.Amount), ColorFlags = color.Color }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect63Sound501330 sound:
                    PublishNativeEffect63Frame(current);
                    PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                    return ImportNativeEffect63Frame(current);
                case NativeBattleEffect63Contact506690 contact:
                    PublishNativeEffect63Frame(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.AbsoluteContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return ImportNativeEffect63Frame(current);
                case NativeBattleEffect63DrawBatch draw when draw.CursorGlobal == CursorGlobal:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect63 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect63 requires its current packet arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect63 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static int NativeEffect63Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect63 requires an actual current physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect63Frame(NativeBattleEffect63Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect63 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect63 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect63Frame ImportNativeEffect63Frame(NativeBattleEffect63Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect63 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect63 ?? throw new NotSupportedException("Effect63 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect63 lost its current CRT stream.") };
    }
}
