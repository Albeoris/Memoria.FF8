using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect251(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect251 requires its current effect.");
        NativeBattleEffect251Resources resources = effect.Binding.Effect251Resources ?? throw new NotSupportedException("Effect251 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect251 requires its current process memory.");
        NativeBattleEffect251Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect251 requires its current CRT stream."), memory.Effect251 ?? throw new NotSupportedException("Effect251 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect251Inputs? inputs = null;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect251 requires its color tables."), ReadPhysical, (_, address) => CaptureNativeEffect301Model(graphics.Models[NativeEffect301Actor(graphics, address)]), Point, Anchor, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect251 requires its current projection."), _ => Camera().Roll, PollIo, (current, operation) => ExecuteNativeEffect251Operation(ref graphics, current, operation, resources, inputs!))
        {
            BoneWorld5FC0 = Bone,
            Bounds6690 = (current, address) => ReadNativeEffect251Bounds(ref graphics, current, address)
        };
        frame = initialize ? NativeBattleEffect251Controller.Initialize(frame, resources, inputs) : NativeBattleEffect251Controller.Advance(frame, resources, inputs);
        PublishNativeEffect251Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect251 requires its current camera.");

        NativeBattleEffect251PointStep Point(NativeBattleEffect251Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect301Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect251 point queries require their current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect251 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect251PollStep PollIo(NativeBattleEffect251Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(current.Memory);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect251File(current, resources, request) : current, poll.Busy);
        }

        NativeBattleEffect251BoneStep Bone(NativeBattleEffect251Frame current, uint address, int selector, NativeBattleEffectArena direction, NativeBattleEffectArena position)
        {
            int actor = NativeEffect301Actor(graphics, address);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect251 requires its current physical rig memory.");
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect251 requires its current physical rig.");
            NativeBattleEffect251BoneWorldOutput result = NativeBattleEffect251ModelServices.BoneWorld5FC0(graphics.Models[actor], source, selector, direction, position);
            return new(current with { Staging = NativeFileStaging }, result.Direction12, result.Position8);
        }

        NativeBattleEffectArena ReadPhysical(NativeBattleEffect251Frame current, uint address, int length)
        {
            if (address >= 0x1D972C0 && (ulong)address + (uint)length <= 0x1D972C0ul + (uint)graphics.Models.Length * 156)
            {
                int actor = checked((int)((address - 0x1D972C0) / 156));
                int offset = checked((int)((address - 0x1D972C0) % 156));
                if (length > 156 - offset)
                {
                    throw new InvalidDataException("Effect251 physical read crosses a current model row.");
                }
                return NativeBattleEffectNodeContextMemory.Copy(CaptureNativeEffect301Model(graphics.Models[actor]), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
            return ReadNativeEffect251ActionBytes(current, address, length);
        }

        NativeBattleEffect251PointStep Anchor(NativeBattleEffect251Frame current, uint address, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect301Actor(graphics, address)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect251 requires its current anchor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect251PointStep first = Point(current, address, 0xF0, 0, retained);
            NativeBattleEffect251PointStep second = Point(first.Frame, address, 0xF1, 0, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
    }

    private NativeBattleEffectArena ReadNativeEffect251ActionBytes(NativeBattleEffect251Frame frame, uint address, int length)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect251 requires its current action parameters.");
        if (address >= 0x1D99A78 && (ulong)address + (uint)length <= 0x1D99A80)
        {
            NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, [parameters.Actor78, parameters.Flags79]).Write(2, BitConverter.GetBytes(parameters.TargetMask7A)).Write(4, BitConverter.GetBytes(checked(0x1D280C4u + (uint)parameters.HeaderIndex7C * 20)));
            return NativeBattleEffectNodeContextMemory.Copy(bytes, checked((int)(address - 0x1D99A78)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        if (address >= 0x1D280C4 && address < 0x1D28344)
        {
            uint relative = address - 0x1D280C4;
            int offset = checked((int)(relative % 20));
            if (length > 20 - offset)
            {
                throw new InvalidDataException("Effect251 action read crosses a current header row.");
            }
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[checked((int)(relative / 20))];
            return NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(offset, length).Select(header.ReadByte).ToArray());
        }
        if (address >= 0x1D28344 && (ulong)address + (uint)length <= 0x1D28344ul + NativeBattleActionPacketsState.ContactCapacity * 24)
        {
            uint relative = address - 0x1D28344;
            int offset = checked((int)(relative % 24));
            if (length > 24 - offset)
            {
                throw new InvalidDataException("Effect251 action read crosses a current contact row.");
            }
            byte[] contact = nativeActionPackets.ReadContact(checked((int)(relative / 24))).Encode();
            return NativeBattleEffectArena.Clear().Write(0, contact.AsSpan(offset, length));
        }
        return NativeBattleEffect251Controller.ReadBytes(frame, address, length);
    }

    private void PublishNativeEffect251Frame(NativeBattleEffect251Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect251 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect251 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect251Frame ImportNativeEffect251Frame(NativeBattleEffect251Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect251 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect251 ?? throw new NotSupportedException("Effect251 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect251 lost its CRT stream.") };
    }
}
