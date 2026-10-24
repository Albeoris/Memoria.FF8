using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect322(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect322 requires its current effect.");
        NativeBattleEffect322Resources resources = effect.Binding.Effect322Resources ?? throw new NotSupportedException("Effect322 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect322 requires current process memory.");
        NativeBattleEffect322Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect322 requires its current CRT stream."), memory.Effect322 ?? throw new NotSupportedException("Effect322 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect322Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect322 requires its color tables."), _ => nativeActionPackets, (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect322Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect322 requires its current projection."), _ => Camera().Roll, Point, Anchor, (current, controls, segment, parameter, retained) => NativeBattleEffect322Trajectory.Sample(current, controls, segment, parameter, retained, definition.NativeGraphics!.Rules.SummonColors!), SelectIo, (current, operation) => ExecuteNativeEffect322Operation(ref graphics, current, operation, resources));
        frame = initialize ? NativeBattleEffect322Controller.Initialize(frame, resources, inputs) : NativeBattleEffect322Controller.Advance(frame, resources, inputs);
        PublishNativeEffect322Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect322 requires its current camera.");

        NativeBattleEffect322PointStep Point(NativeBattleEffect322Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect322Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect322 requires its current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect322 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        NativeBattleEffect322PointStep Anchor(NativeBattleEffect322Frame current, uint address, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect322Actor(graphics, address)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect322 requires its current target transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect322PointStep first = Point(current, address, 0xF0, 0, retained);
            NativeBattleEffect322PointStep second = Point(first.Frame, address, 0xF1, 0, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        NativeBattleEffect322IoCompletionStep SelectIo(NativeBattleEffect322Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect322File(current, resources, request) : current, !poll.Busy);
        }
    }

    private NativeBattleEffect322Frame QueueNativeEffect322File(NativeBattleEffect322Frame current, NativeBattleEffect322Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect322 requires its current file destination arena.");
        current = current with { Memory = current.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return current;
    }

    private static int NativeEffect322Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect322 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect322Frame(NativeBattleEffect322Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect322 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect322 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect322Frame ImportNativeEffect322Frame(NativeBattleEffect322Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect322 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect322 ?? throw new NotSupportedException("Effect322 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect322 lost its CRT stream.") };
    }
}
